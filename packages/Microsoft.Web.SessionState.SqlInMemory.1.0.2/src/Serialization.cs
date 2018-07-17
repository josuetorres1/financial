//------------------------------------------------------------------------------
// <copyright file="Serialization.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Security.Permissions;
using System.Web;
using System.Web.SessionState;

namespace Microsoft.Web.SessionState
{
    internal static class Serialization
    {
        // Prevents truncation of streams due to trimming of last
        // zeros in the buffer. That can happen in SQL if ANSI_PADDING is OFF.
        private const byte EndOfStream = 0xFF;

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times",
            Justification = "DeflateStream leaves the stream opened.")]
        internal static void Serialize(SessionStateStoreData item, bool compressionEnabled, out byte[] buffer, out long length)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                SerializeTo(item, stream);

                if (compressionEnabled)
                {
                    byte[] serializedBuffer = stream.GetBuffer();
                    int serializedLength = (int)stream.Length;

                    stream.SetLength(0);
                    const bool leaveOpened = true;
                    using (DeflateStream compressedStream = new DeflateStream(stream, CompressionMode.Compress, leaveOpened))
                    {
                        compressedStream.Write(serializedBuffer, 0, serializedLength);
                    }

                    stream.WriteByte(EndOfStream);
                }

                buffer = stream.GetBuffer();
                length = stream.Length;
            }
        }

        internal static SessionStateStoreData Deserialize(HttpContext context, MemoryStream stream, bool compressionEnabled)
        {
            if (compressionEnabled)
            {
                // Apply the compression decorator on top of the stream.
                using (DeflateStream zipStream = new DeflateStream(stream, CompressionMode.Decompress, true))
                {
                    return Deserialize(context, zipStream);
                }
            }

            return Deserialize(context, stream);
        }

        [SecurityPermission(SecurityAction.Assert, SerializationFormatter = true)]
        internal static void SerializeTo(SessionStateStoreData item, Stream stream)
        {
            bool hasItems = true;
            bool hasStaticObjects = true;
            BinaryWriter writer = new BinaryWriter(stream);

            writer.Write(item.Timeout);
            if (item.Items == null || item.Items.Count == 0)
            {
                hasItems = false;
            }
            writer.Write(hasItems);

            if (item.StaticObjects == null || item.StaticObjects.NeverAccessed)
            {
                hasStaticObjects = false;
            }
            writer.Write(hasStaticObjects);

            if (hasItems)
            {
                ((SessionStateItemCollection)item.Items).Serialize(writer);
            }
            if (hasStaticObjects)
            {
                item.StaticObjects.Serialize(writer);
            }

            writer.Write(EndOfStream);
        }

        [SecurityPermission(SecurityAction.Assert, SerializationFormatter = true)]
        private static SessionStateStoreData Deserialize(HttpContext context, Stream stream)
        {
            int timeout;
            bool hasItems;
            bool hasStaticObjects;
            SessionStateItemCollection sessionItems;
            HttpStaticObjectsCollection staticObjects;

            try
            {
                BinaryReader reader = new BinaryReader(stream);
                timeout = reader.ReadInt32();
                hasItems = reader.ReadBoolean();
                hasStaticObjects = reader.ReadBoolean();

                if (hasItems)
                {
                    sessionItems = SessionStateItemCollection.Deserialize(reader);
                }
                else
                {
                    sessionItems = new SessionStateItemCollection();
                }

                if (hasStaticObjects)
                {
                    staticObjects = HttpStaticObjectsCollection.Deserialize(reader);
                }
                else
                {
                    staticObjects = SessionStateUtility.GetSessionStaticObjects(context);
                }

                if (reader.ReadByte() != EndOfStream)
                {
                    throw new HttpException(Resources.Invalid_session_state);
                }
            }
            catch (EndOfStreamException)
            {
                throw new HttpException(Resources.Invalid_session_state);
            }

            return new SessionStateStoreData(sessionItems, staticObjects, timeout);
        }
    }
}