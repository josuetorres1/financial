//------------------------------------------------------------------------------
// <copyright file="SqlInMemoryProvider.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Web;
using System.Web.SessionState;

namespace Microsoft.Web.SessionState
{
    public sealed class SqlInMemoryProvider : SessionStateStoreProviderBase
    {
        private static string s_connectionString;
        private static bool s_useIntegratedSecurity;
        private static bool s_compressionEnabled;
        private static object s_lock;
        private static int s_initialized;

        private HttpContext currentHttpContext;

        public SqlInMemoryProvider()
        {
        }

        static SqlInMemoryProvider()
        {
            s_lock = new object();
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            if (String.IsNullOrEmpty(name))
            {
                name = "SQL Server In-Memory Session State Store Provider";
            }

            base.Initialize(name, config);

            if (s_initialized == 0)
            {
                lock (s_lock)
                {
                    if (s_initialized == 0)
                    {
                        LazyInitialize(config);
                        Interlocked.Exchange(ref s_initialized, 1);
                    }
                }
            }
        }

        private void LazyInitialize(NameValueCollection config)
        {
            string connectionString = config["connectionString"];

            if (String.IsNullOrEmpty(connectionString))
            {
                throw new ConfigurationErrorsException(Resources.No_database_found_in_connectionString);
            }

            string compressionEnabled = config["compressionEnabled"];
            if (!String.IsNullOrEmpty(compressionEnabled))
            {
                s_compressionEnabled = Convert.ToBoolean(compressionEnabled, CultureInfo.InvariantCulture);
            }

            SqlConnectionStringBuilder connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);

            s_connectionString = connectionStringBuilder.ToString();
            s_useIntegratedSecurity = connectionStringBuilder.IntegratedSecurity;
        }

        public override SessionStateStoreData CreateNewStoreData(HttpContext context, int timeout)
        {
            return CreateLegitStoreData(context, timeout);
        }

        public override void CreateUninitializedItem(HttpContext context, string id, int timeout)
        {
            byte[] buffer;
            long itemLength;
            SessionStateStoreData item = CreateNewStoreData(context, timeout);

            Serialization.Serialize(item, s_compressionEnabled, out buffer, out itemLength);

            using (SqlStore store = GetStore())
            {
                const bool newItem = true;
                const bool initialized = false;
                store.ExecuteInsertOrUpdateItem(id, TimeSpan.FromMinutes(item.Timeout), 0, newItem, initialized, buffer, itemLength);
            }
        }

        public override void Dispose()
        {
        }

        public override void EndRequest(HttpContext context)
        {
        }

        public override SessionStateStoreData GetItem(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            const bool exclusive = false;
            return GetItem(context, id, exclusive, out locked, out lockAge, out lockId, out actions);
        }

        public override SessionStateStoreData GetItemExclusive(HttpContext context, string id, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            const bool exclusive = true;
            return GetItem(context, id, exclusive, out locked, out lockAge, out lockId, out actions);
        }

        private SessionStateStoreData GetItem(HttpContext context, string id, bool exclusive, out bool locked, out TimeSpan lockAge, out object lockId, out SessionStateActions actions)
        {
            byte[] buffer;
            long itemLength;
            bool requiresInitialization;
            SessionStateStoreData item = null;

            using (SqlStore store = GetStore())
            {
                int lockCookie;
                store.ExecuteGetItem(id, exclusive, out buffer, out itemLength, out locked, out lockAge, out lockCookie, out requiresInitialization);
                lockId = lockCookie;
            }

            if (requiresInitialization)
            {
                actions = SessionStateActions.InitializeItem;
            }
            else
            {
                actions = SessionStateActions.None;
            }

            if (buffer != null)
            {
                using (MemoryStream stream = new MemoryStream(buffer, 0, checked((int)itemLength), false))
                {
                    item = Serialization.Deserialize(context, stream, s_compressionEnabled);
                }
            }

            return item;
        }

        public override void InitializeRequest(HttpContext context)
        {
            this.currentHttpContext = context;
        }

        public override void ReleaseItemExclusive(HttpContext context, string id, object lockId)
        {
            int lockCookie = (int)lockId;

            using (SqlStore store = GetStore())
            {
                store.ExecuteReleaseItemExclusive(id, lockCookie);
            }
        }

        public override void RemoveItem(HttpContext context, string id, object lockId, SessionStateStoreData item)
        {
            int lockCookie = (int)lockId;

            using (SqlStore store = GetStore())
            {
                store.ExecuteRemoveItem(id, lockCookie);
            }
        }

        public override void ResetItemTimeout(HttpContext context, string id)
        {
            using (SqlStore store = GetStore())
            {
                store.ExecuteResetItemTimeout(id);
            }
        }

        public override bool SetItemExpireCallback(SessionStateItemExpireCallback expireCallback)
        {
            return false;
        }

        public override void SetAndReleaseItemExclusive(HttpContext context, string id, SessionStateStoreData item, object lockId, bool newItem)
        {
            byte[] buffer;
            long itemLength;
            int lockCookie = (lockId == null) ? 0 : (int)lockId;

            try
            {
                Serialization.Serialize(item, s_compressionEnabled, out buffer, out itemLength);
            }
            catch
            {
                if (!newItem)
                {
                    // Release the exclusiveness of the existing item.
                    ((SessionStateStoreProviderBase)this).ReleaseItemExclusive(context, id, lockId);
                }
                throw;
            }

            using (SqlStore store = GetStore())
            {
                const bool initialized = true;
                store.ExecuteInsertOrUpdateItem(id, TimeSpan.FromMinutes(item.Timeout), lockCookie, newItem, initialized, buffer, itemLength);
            }
        }

        private SqlStore GetStore()
        {
            return SqlStore.GetOrCreate(s_connectionString, CanUsePooling());
        }

        private bool CanUsePooling()
        {
            bool ret = false;

            if (!s_useIntegratedSecurity)
            {
                ret = true;
            }
            else
            {
                // It is possible to use pooling for integrated security if
                // and only if UNC is not being used and client impersonation
                // is not enabled for this request.
            }

            return ret;
        }

        private static SessionStateStoreData CreateLegitStoreData(HttpContext context, int timeout)
        {
            ISessionStateItemCollection sessionItems = new SessionStateItemCollection();
            HttpStaticObjectsCollection staticObjects = null;

            if (context != null)
            {
                staticObjects = SessionStateUtility.GetSessionStaticObjects(context);
            }

            return new SessionStateStoreData(sessionItems, staticObjects, timeout);
        }
    }
}