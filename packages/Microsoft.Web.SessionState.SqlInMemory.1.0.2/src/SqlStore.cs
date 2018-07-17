//------------------------------------------------------------------------------
// <copyright file="SqlStore.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Web;
using Microsoft.SqlServer.Server;

namespace Microsoft.Web.SessionState
{
    internal sealed class SqlStore : IDisposable
    {
        private const int SQL_PRIMARY_KEY_VIOLATION = 2627;
        private const int SQL_WRITE_WRITE_CONFLICT = 41302;
        private const int SQL_COMMIT_DEPENDENCY_FAILURE = 41301;
        private const int SQL_REPEATABLE_READ_VALIDATION = 41305;
        private const int SQL_LOGON_FAILED = 18456;
        private const int SQL_LOGON_INVALID_CONNECT = 18452;
        private const int SQL_CANNOT_OPEN_DATABASE_FOR_LOGIN = 4060;
        private const int SQL_TIMEOUT_EXPIRED = -2;
        private const int ITEM_BLOCK_MAX_PARAMS = 9;
        private const int ITEM_1_BLOCK_LENGTH = 7000;
        private const int ITEM_9_BLOCKS_LENGTH = ITEM_1_BLOCK_LENGTH * ITEM_BLOCK_MAX_PARAMS;

        // Reserved for future use (partitioning).
        private const int APP_SUFFIX_LENGTH = 8;

        private const int SESSION_ID_LENGTH_LIMIT = 80;
        private const int SESSION_ID_LENGTH = SESSION_ID_LENGTH_LIMIT + APP_SUFFIX_LENGTH;

        private SqlConnection sqlConnection;
        private SqlCommand cmdInsertOrUpdateStateItem;
        private SqlCommand cmdInsertOrUpdateStateItemMedium;
        private SqlCommand cmdInsertOrUpdateStateItemLarge;
        private SqlCommand cmdGetStateItem;
        private SqlCommand cmdGetStateItemExclusive;
        private SqlCommand cmdReleaseStateItemExclusive;
        private SqlCommand cmdRemoveItem;
        private SqlCommand cmdResetItemTimeout;

        private static int clearPoolInProgress;

        private bool usePooling;
        private int poolTickCount;

        private static ConcurrentStack<SqlStore> s_pool;
        private static int s_poolMax;
        private static System.Threading.Timer s_poolScavengerTimer;

        // Look-aside for buffer of size 'ITEM_1_BLOCK_LENGTH'.
        private static ConcurrentStack<byte[]> s_itemBlockLookaside;

        static SqlStore()
        {
            s_pool = new ConcurrentStack<SqlStore>();
            s_poolMax = Math.Min(5000, Environment.ProcessorCount * 100);
            TimeSpan interval = TimeSpan.FromMinutes(1);
            s_poolScavengerTimer = new System.Threading.Timer(PoolScavengerCallback, interval, interval, interval);

            s_itemBlockLookaside = new ConcurrentStack<byte[]>();
        }

        private SqlStore(string connectionString)
        {
            TimeSpan backoffTime = TimeSpan.Zero;
            sqlConnection = new SqlConnection(connectionString);

            while (true)
            {
                try
                {
                    sqlConnection.Open();

                    // The operation succeeded, exit the loop.
                    break;
                }
                catch (SqlException e)
                {
                    if (e != null &&
                        (e.Number == SQL_LOGON_FAILED ||
                         e.Number == SQL_LOGON_INVALID_CONNECT))
                    {
                        HttpException outerException = new HttpException(
                                Resources.Login_failed_sql_session_database, e);

                        ClearConnectionAndThrow(outerException);
                    }

                    if (!CanRetry(e, ref backoffTime))
                    {
                        // Just throw, default to previous behavior.
                        ClearConnectionAndThrow(e);
                    }
                }
                catch (Exception e)
                {
                    // Just throw, we have a different Exception.
                    ClearConnectionAndThrow(e);
                }
            }
        }

        public void Dispose()
        {
            bool dispose = true;

            try
            {
                if (this.usePooling)
                {
                    if (TryReturnToPool(this))
                    {
                        dispose = false;
                    }
                    else
                    {
                        NotePoolFull();
                    }
                }
            }
            finally
            {
                if (dispose)
                {
                    DisposeAllCommands();

                    if (sqlConnection != null)
                    {
                        sqlConnection.Dispose();
                        sqlConnection = null;
                    }
                }
            }
        }

        public static SqlStore GetOrCreate(string connectionString, bool usePooling)
        {
            SqlStore store = null;

            if (usePooling)
            {
                store = TryGetFromPool();

                if (store != null && (store.sqlConnection.State & ConnectionState.Open) == 0)
                {
                    store.usePooling = false;
                    store.Dispose();
                    store = null;
                }
            }

            if (store == null)
            {
                store = new SqlStore(connectionString);
            }

            store.usePooling = usePooling;

            return store;
        }

        private static SqlStore TryGetFromPool()
        {
            SqlStore store = null;

            s_pool.TryPop(out store);

            return store;
        }

        private static bool TryReturnToPool(SqlStore store)
        {
            int extimatedCount = s_pool.Count;

            if (extimatedCount < s_poolMax)
            {
                store.poolTickCount = Environment.TickCount;
                s_pool.Push(store);

                return true;
            }

            return false;
        }

        private static void PoolScavengerCallback(object state)
        {
            TimeSpan idle = (TimeSpan)state;
            long idleMs = idle.Ticks /*nanoseconds*/ / 10000;
            int now = Environment.TickCount;
            int count = 0;

            // Close some idle connections.

            foreach (SqlStore store in s_pool)
            {
                if ((now - store.poolTickCount) >= idleMs)
                {
                    count += 1;
                }
            }

            while (count > 0)
            {
                SqlStore store;

                if (s_pool.TryPop(out store))
                {
                    try
                    {
                        store.usePooling = false;
                        store.Dispose();
                    }
                    catch
                    { }
                }
                else
                {
                    // Empty pool. Invalidate look-aside as well.
                    s_itemBlockLookaside = new ConcurrentStack<byte[]>();
                    break;
                }

                count -= 1;
            }
        }

        private void DisposeAllCommands()
        {
            DisposeCommand(cmdInsertOrUpdateStateItem);
            DisposeCommand(cmdInsertOrUpdateStateItemMedium);
            DisposeCommand(cmdInsertOrUpdateStateItemLarge);
            DisposeCommand(cmdGetStateItem);
            DisposeCommand(cmdGetStateItemExclusive);
            DisposeCommand(cmdReleaseStateItemExclusive);
            DisposeCommand(cmdRemoveItem);
            DisposeCommand(cmdResetItemTimeout);
        }

        private static void DisposeCommand(SqlCommand cmd)
        {
            if (cmd != null)
            {
                cmd.Dispose();
            }
        }

        private SqlCommand InsertOrUpdateStateItem
        {
            get
            {
                SqlCommand cmd = cmdInsertOrUpdateStateItem;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.InsertOrUpdateStateItem";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@NewItem", SqlDbType.Bit));
                    cmd.Parameters.Add(new SqlParameter("@Initialized", SqlDbType.Bit));
                    cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@Timeout", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@ItemSize", SqlDbType.BigInt));
                    cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));

                    cmdInsertOrUpdateStateItem = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand InsertOrUpdateStateItemMedium
        {
            get
            {
                SqlCommand cmd = cmdInsertOrUpdateStateItemMedium;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.InsertOrUpdateStateItemMedium";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@NewItem", SqlDbType.Bit));
                    cmd.Parameters.Add(new SqlParameter("@Initialized", SqlDbType.Bit));
                    cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@Timeout", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@ItemSize", SqlDbType.BigInt));

                    // Up to 63000 bytes.
                    cmd.Parameters.Add(new SqlParameter("@Item1", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item2", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item3", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item4", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item5", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item6", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item7", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item8", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@Item9", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));

                    cmdInsertOrUpdateStateItemMedium = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand InsertOrUpdateStateItemLarge
        {
            get
            {
                SqlCommand cmd = cmdInsertOrUpdateStateItemLarge;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.InsertOrUpdateStateItemLarge";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@NewItem", SqlDbType.Bit));
                    cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@Timeout", SqlDbType.Int));
                    cmd.Parameters.Add(new SqlParameter("@ItemSize", SqlDbType.BigInt));
                    cmd.Parameters.Add(new SqlParameter("@Items", SqlDbType.Structured));
                    cmd.Parameters[5].TypeName = "dbo.SessionItemsTable";

                    cmdInsertOrUpdateStateItemLarge = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand GetStateItem
        {
            get
            {
                SqlCommand cmd = cmdGetStateItem;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.GetStateItem";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlParameter parameter;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    parameter = cmd.Parameters.Add(new SqlParameter("@Locked", SqlDbType.Bit));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@LockAge", SqlDbType.Int));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@Initialized", SqlDbType.Bit));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    parameter.Direction = ParameterDirection.Output;

                    cmdGetStateItem = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand GetStateItemExclusive
        {
            get
            {
                SqlCommand cmd = cmdGetStateItemExclusive;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.GetStateItemExclusive";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlParameter parameter;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    parameter = cmd.Parameters.Add(new SqlParameter("@Locked", SqlDbType.Bit));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@LockAge", SqlDbType.Int));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@Initialized", SqlDbType.Bit));
                    parameter.Direction = ParameterDirection.Output;
                    parameter = cmd.Parameters.Add(new SqlParameter("@Item", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH));
                    parameter.Direction = ParameterDirection.Output;

                    cmdGetStateItemExclusive = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand ReleaseStateItemExclusive
        {
            get
            {
                SqlCommand cmd = cmdReleaseStateItemExclusive;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.ReleaseStateItemExclusive";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));

                    cmdReleaseStateItemExclusive = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand RemoveItem
        {
            get
            {
                SqlCommand cmd = cmdRemoveItem;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.RemoveItem";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));
                    cmd.Parameters.Add(new SqlParameter("@LockCookie", SqlDbType.Int));

                    cmdRemoveItem = cmd;
                }

                return cmd;
            }
        }

        private SqlCommand ResetItemTimeout
        {
            get
            {
                SqlCommand cmd = cmdResetItemTimeout;

                if (cmd == null)
                {
                    string storedProcedureName = "dbo.ResetItemTimeout";

                    cmd = new SqlCommand(storedProcedureName, sqlConnection);
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add(new SqlParameter("@SessionId", SqlDbType.NVarChar, SESSION_ID_LENGTH));

                    cmdResetItemTimeout = cmd;
                }

                return cmd;
            }
        }

        internal void ExecuteInsertOrUpdateItem(string id, TimeSpan timeout, int lockCookie, bool newItem, bool initialized, byte[] buffer, long itemLength)
        {
            ValidateIdLength(id);

            SqlCommand cmd;
            byte[] item = null;

            if (itemLength > ITEM_1_BLOCK_LENGTH)
            {
                if (itemLength <= ITEM_9_BLOCKS_LENGTH)
                {
                    cmd = InsertOrUpdateStateItemMedium;
                    cmd.Parameters[0].Value = id; // @SessionId
                    cmd.Parameters[1].Value = newItem; // @NewItem
                    cmd.Parameters[2].Value = initialized; // @Initialized
                    cmd.Parameters[3].Value = lockCookie; // @LockCookie
                    cmd.Parameters[4].Value = timeout.TotalMinutes; // @Timeout
                    cmd.Parameters[5].Value = itemLength; // @ItemSize

                    long remaining = itemLength;
                    long read = 0;
                    int startIndex = 6;
                    int i = 0;

                    for (; i < ITEM_BLOCK_MAX_PARAMS && remaining > 0; ++i)
                    {
                        int paramIndex = startIndex + i;

                        long copy = Math.Min(ITEM_1_BLOCK_LENGTH, remaining);

                        if (copy == ITEM_1_BLOCK_LENGTH)
                        {
                            s_itemBlockLookaside.TryPop(out item);
                        }

                        if (item == null)
                        {
                            item = new byte[copy];
                        }

                        Buffer.BlockCopy(buffer, (int)read, item, 0, (int)copy);
                        read += copy;
                        remaining -= copy;

                        cmd.Parameters[paramIndex].Value = item;
                        item = null;
                    }

                    for (; i < ITEM_BLOCK_MAX_PARAMS; ++i)
                    {
                        int paramIndex = startIndex + i;

                        cmd.Parameters[paramIndex].Value = DBNull.Value;
                    }
                }
                else
                {
                    cmd = InsertOrUpdateStateItemLarge;
                    cmd.Parameters[0].Value = id; // @SessionId
                    cmd.Parameters[1].Value = newItem; // @NewItem
                    cmd.Parameters[2].Value = lockCookie; // @LockCookie
                    cmd.Parameters[3].Value = timeout.TotalMinutes; // @Timeout
                    cmd.Parameters[4].Value = itemLength; // @ItemSize

                    // @Items
                    cmd.Parameters[5].Value = new BufferToSqlDataRecordEnumerator(buffer, itemLength);
                }
            }
            else
            {
                cmd = InsertOrUpdateStateItem;
                cmd.Parameters[0].Value = id; // @SessionId
                cmd.Parameters[1].Value = newItem; // @NewItem
                cmd.Parameters[2].Value = initialized; // @Initialized
                cmd.Parameters[3].Value = lockCookie; // @LockCookie
                cmd.Parameters[4].Value = timeout.TotalMinutes; // @Timeout
                cmd.Parameters[5].Value = itemLength; // @ItemSize

                if (buffer.Length == itemLength)
                {
                    item = buffer;
                }
                else
                {
                    item = new byte[itemLength];
                    Buffer.BlockCopy(buffer, 0, item, 0, (int)itemLength);
                }

                // @Item
                cmd.Parameters[6].Value = item;
            }

            SqlExecuteNonQueryWithRetry(cmd, newItem);
        }

        internal void ExecuteGetItem(string id, bool exclusive, out byte[] itemBuffer, out long itemLength, out bool locked, out TimeSpan lockAge, out int lockCookie, out bool requiresInitialization)
        {
            ValidateIdLength(id);

            SqlCommand cmd;

            if (exclusive)
            {
                cmd = GetStateItemExclusive;
            }
            else
            {
                cmd = GetStateItem;
            }

            cmd.Parameters[0].Value = id; // @SessionId
            cmd.Parameters[1].Value = Convert.DBNull; // @Locked
            cmd.Parameters[2].Value = Convert.DBNull; // @LockAge
            cmd.Parameters[3].Value = Convert.DBNull; // @LockCookie
            cmd.Parameters[4].Value = Convert.DBNull; // @Initialized
            cmd.Parameters[5].Value = Convert.DBNull; // @Item

            List<SessionItem> buffers = null;
            long bufferLength = 0;

            // Read the data first, then the output parameters.
            using (SqlDataReader reader = SqlExecuteReaderWithRetry(cmd, CommandBehavior.SequentialAccess))
            {
                try
                {
                    if (reader.Read())
                    {
                        buffers = new List<SessionItem>();

                        do
                        {
                            byte[] buffer = null;

                            if (!s_itemBlockLookaside.TryPop(out buffer))
                            {
                                buffer = new byte[ITEM_1_BLOCK_LENGTH];
                            }

                            int index = reader.GetInt32(0); // SessionItemId
                            long bytesRead = reader.GetBytes(1, 0, buffer, 0, buffer.Length);  // Item

                            var item = new SessionItem()
                            {
                                Id = index,
                                BufferLength = (int)bytesRead,
                                Buffer = buffer
                            };

                            buffers.Add(item);
                            bufferLength += item.BufferLength;
                        }
                        while (reader.Read());
                    }
                }
                catch (Exception e)
                {
                    throw CreateHttpException(e);
                }
            }

            // Read the output parameters.
            locked = (bool)cmd.Parameters[1].Value;
            lockAge = TimeSpan.FromSeconds((int)cmd.Parameters[2].Value);
            lockCookie = (int)cmd.Parameters[3].Value;
            bool initialized = (bool)cmd.Parameters[4].Value;
            requiresInitialization = !initialized;

            if (locked)
            {
                itemBuffer = null;
                itemLength = 0;
            }
            else if (bufferLength == 0)
            {
                // Item is returned from the output parameter.
                object value;

                value = cmd.Parameters[5].Value;

                if (Convert.IsDBNull(value))
                {
                    itemBuffer = null;
                    itemLength = 0;
                }
                else
                {
                    itemBuffer = (byte[])value;
                    itemLength = itemBuffer.LongLength;
                }
            }
            else
            {
                // Server does not return sorted rows due to performance.
                buffers.Sort();

                itemBuffer = new byte[bufferLength];
                itemLength = 0;

                for (int i = 0; i < buffers.Count; ++i)
                {
                    long len = buffers[i].BufferLength;

                    Buffer.BlockCopy(buffers[i].Buffer, 0, itemBuffer, (int)itemLength, (int)len);
                    itemLength += len;

                    s_itemBlockLookaside.Push(buffers[i].Buffer);
                }
            }
        }

        internal void ExecuteReleaseItemExclusive(string id, int lockCookie)
        {
            ValidateIdLength(id);

            SqlCommand cmd = ReleaseStateItemExclusive;

            cmd.Parameters[0].Value = id; // @SessionId
            cmd.Parameters[1].Value = lockCookie; // @LockCookie

            SqlExecuteNonQueryWithRetry(cmd, false);
        }

        internal void ExecuteRemoveItem(string id, int lockCookie)
        {
            ValidateIdLength(id);

            SqlCommand cmd = RemoveItem;

            cmd.Parameters[0].Value = id; // @SessionId
            cmd.Parameters[1].Value = lockCookie; // @LockCookie

            SqlExecuteNonQueryWithRetry(cmd, false);
        }

        internal void ExecuteResetItemTimeout(string id)
        {
            ValidateIdLength(id);

            SqlCommand cmd = ResetItemTimeout;

            cmd.Parameters[0].Value = id; // @SessionId

            SqlExecuteNonQueryWithRetry(cmd, false);
        }

        private SqlDataReader SqlExecuteReaderWithRetry(SqlCommand cmd, CommandBehavior cmdBehavior)
        {
            TimeSpan backoffTime = TimeSpan.Zero;

            while (true)
            {
                try
                {
                    // Connection may be closed after a throw.
                    if (cmd.Connection.State != ConnectionState.Open)
                    {
                        cmd.Connection.Open();
                    }

                    SqlDataReader reader = cmd.ExecuteReader(cmdBehavior);
                    return reader;
                }
                catch (SqlException e)
                {
                    if (!CanRetry(e, ref backoffTime))
                    {
                        // Just throw, default to previous behavior.
                        throw CreateHttpException(e);
                    }
                }
                catch (Exception e)
                {
                    // Just throw, we have a different Exception.
                    throw CreateHttpException(e);
                }
            }
        }

        private int SqlExecuteNonQueryWithRetry(SqlCommand cmd, bool ignoreInsertPKException)
        {
            TimeSpan backoffTime = TimeSpan.Zero;

            while (true)
            {
                try
                {
                    // Connection may be closed after a throw.
                    if (cmd.Connection.State != ConnectionState.Open)
                    {
                        cmd.Connection.Open();
                    }

                    int result = cmd.ExecuteNonQuery();

                    return result;
                }
                catch (SqlException e)
                {
                    // If specified, ignore primary key violations.
                    if (IsInsertPKException(e, ignoreInsertPKException))
                    {
                        return -1;
                    }

                    if (!CanRetry(e, ref backoffTime))
                    {
                        // Just throw, default to previous behavior.
                        throw CreateHttpException(e);
                    }
                }
                catch (Exception e)
                {
                    // Just throw, we have a different Exception.
                    throw CreateHttpException(e);
                }
            }
        }

        private static bool IsInsertPKException(SqlException ex, bool ignoreInsertPKException)
        {
            // If the severity is greater than 20, we have a serious error.
            // The server usually closes the connection in these cases.
            if (ex != null &&
                 ex.Number == SQL_PRIMARY_KEY_VIOLATION &&
                 ignoreInsertPKException)
            {
                // It's possible that two threads (from the same session) are creating the session
                // state, both failed to get it first, and now both tried to insert it.
                // One thread may lose with a Primary Key Violation error. If so, that thread will
                // just lose and exit gracefully.
                return true;
            }
            return false;
        }

        private void ClearConnectionAndThrow(Exception e)
        {
            SqlConnection connection = sqlConnection;
            sqlConnection = null;
            throw CreateHttpException(e);
        }

        private static void ValidateIdLength(string id)
        {
            if (id.Length > SESSION_ID_LENGTH_LIMIT)
            {
                throw new HttpException(Resources.Session_id_too_long);
            }
        }

        private bool CanRetry(SqlException ex, ref TimeSpan backoffTime)
        {
            if (ex == null)
            {
                return false;
            }

            // We will retry SQL operations for fatal exception or write / write
            // conflicts.
            if (!IsFatalSqlException(ex) && !IsSnapshotConflict(ex))
            {
                return false;
            }

            TimeSpan sleep = TimeSpan.Zero;

            // Do not retry if the back off was long enough.
            if (backoffTime.TotalMinutes >= 2.0)
            {
                return false;
            }

            if (IsSnapshotConflict(ex))
            {
                // It should not take too long to resolve a snapshot conflict.
                sleep = TimeSpan.FromMilliseconds(100);

                NoteShortBackoff();
            }
            else if (backoffTime == TimeSpan.Zero)
            {
                // We will retry for the first time. Clear the connection
                // pool to start fresh.

                if (System.Threading.Interlocked.Exchange(ref clearPoolInProgress, 1) == 0)
                {
                    try
                    {
                        SqlConnection.ClearPool(sqlConnection);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref clearPoolInProgress, 0);
                    }
                }

                // First time back off more since the failure may not
                // be resolved fast enough becuase we had a serious error.
                sleep = TimeSpan.FromSeconds(3);

                NoteLongBackoff();
            }
            else
            {
                // Additional back offs are just one second.
                sleep = TimeSpan.FromSeconds(1);

                NoteMediumBackoff();
            }

            if (sleep != TimeSpan.Zero)
            {
                System.Threading.Thread.Sleep(sleep);
            }

            backoffTime.Add(sleep);

            return true;
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void NoteShortBackoff()
        {
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void NoteLongBackoff()
        {
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void NoteMediumBackoff()
        {
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void NotePoolFull()
        {
        }

        private static bool IsSnapshotConflict(SqlException ex)
        {
            switch (ex.Number)
            {
                case SQL_WRITE_WRITE_CONFLICT:
                case SQL_COMMIT_DEPENDENCY_FAILURE:
                case SQL_REPEATABLE_READ_VALIDATION:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsFatalSqlException(SqlException ex)
        {
            if (ex.Class >= 20 ||
                 ex.Number == SQL_CANNOT_OPEN_DATABASE_FOR_LOGIN ||
                 ex.Number == SQL_TIMEOUT_EXPIRED)
            {
                return true;
            }

            return false;
        }

        private static Exception CreateHttpException(Exception e)
        {
            return new HttpException(Resources.Cant_connect_sql_session_database, e);
        }

        public sealed class BufferToSqlDataRecordEnumerator : IEnumerable<SqlDataRecord>, IEnumerator<SqlDataRecord>
        {
            private static SqlMetaData[] metadata;
            private readonly byte[] buffer;
            private readonly long length;
            private int id;
            private long index = -1;
            private long total;
            private long blockSize;
            private byte[] lastBufferBlock;
            private SqlDataRecord record;

            public BufferToSqlDataRecordEnumerator(byte[] buffer, long length)
            {
                this.length = length;
                this.buffer = buffer;
            }

            static BufferToSqlDataRecordEnumerator()
            {
                metadata = new SqlMetaData[2];
                metadata[0] = new SqlMetaData("SessionItemId", SqlDbType.Int);
                metadata[1] = new SqlMetaData("Item", SqlDbType.VarBinary, ITEM_1_BLOCK_LENGTH);
            }

            public IEnumerator<SqlDataRecord> GetEnumerator()
            {
                return this;
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                throw new NotImplementedException();
            }

            public bool MoveNext()
            {
                if (total == length)
                {
                    return false;
                }

                if (index == -1)
                {
                    // First time.
                    id = 1;
                    index = 0;
                    blockSize = Math.Min(length, ITEM_1_BLOCK_LENGTH);
                    total += blockSize;
                }
                else
                {
                    id += 1;
                    index = total;
                    blockSize = Math.Min(length - total, ITEM_1_BLOCK_LENGTH);
                    total += blockSize;
                }

                byte[] bufferBlock = null;

                if (blockSize == ITEM_1_BLOCK_LENGTH)
                {
                    // Reuse fixed size buffer.
                    bufferBlock = this.lastBufferBlock;
                }

                if (bufferBlock == null)
                {
                    bufferBlock = new byte[blockSize];
                }

                Buffer.BlockCopy(buffer, (int)index, bufferBlock, 0, (int)blockSize);

                record = new SqlDataRecord(metadata);
                record.SetInt32(0, id); // @SessionItemId
                record.SetSqlBinary(1, new SqlBinary(bufferBlock)); // @Item

                if (blockSize == ITEM_1_BLOCK_LENGTH)
                {
                    this.lastBufferBlock = bufferBlock;
                }

                return true;
            }

            public SqlDataRecord Current
            {
                get
                {
                    return record;
                }
            }

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    return null;
                }
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        internal sealed class SessionItem : IComparable<SessionItem>
        {
            public int Id { get; set; }
            public int BufferLength { get; set; }
            public byte[] Buffer { get; set; }

            public int CompareTo(SessionItem other)
            {
                return this.Id - other.Id;
            }
        }
    }
}
