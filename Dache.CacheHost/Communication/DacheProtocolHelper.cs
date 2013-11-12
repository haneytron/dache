using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Dache.CacheHost.Communication
{
    /// <summary>
    /// Assists in communicating via the Dache protocol.
    /// </summary>
    internal static class DacheProtocolHelper
    {
        // The communication encoding
        public static readonly Encoding CommunicationEncoding = Encoding.UTF8;
        // The communication protocol control byte default - 1 control byte for message type
        public static readonly byte[] ControlByteDefault = new byte[] { 0 };
        // The byte that represents a space
        public static readonly byte[] SpaceByte = CommunicationEncoding.GetBytes(" ");
        // The absolute expiration format
        public const string AbsoluteExpirationFormat = "yyMMddhhmmss";

        public enum MessageType
        {
            /// <summary>
            /// No repeated items.
            /// </summary>
            Literal = 0,

            /// <summary>
            /// Repeating cache keys.
            /// </summary>
            RepeatingCacheKeys,

            /// <summary>
            /// Repeating cache objects.
            /// </summary>
            RepeatingCacheObjects,

            /// <summary>
            /// Repeating cache keys and objects in pairs.
            /// </summary>
            RepeatingCacheKeysAndObjects
        }

        public static void Write(this MemoryStream memoryStream, string value)
        {
            var bytes = CommunicationEncoding.GetBytes(value);
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this MemoryStream memoryStream, string format, params object[] args)
        {
            var bytes = CommunicationEncoding.GetBytes(string.Format(format, args));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void Write(this MemoryStream memoryStream, byte[] bytes)
        {
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteBase64(this MemoryStream memoryStream, byte[] value)
        {
            var bytes = CommunicationEncoding.GetBytes(Convert.ToBase64String(value));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteControlBytePlaceHolder(this MemoryStream memoryStream)
        {
            memoryStream.Write(ControlByteDefault, 0, ControlByteDefault.Length);
        }

        public static void WriteSpace(this MemoryStream memoryStream)
        {
            memoryStream.Write(SpaceByte, 0, SpaceByte.Length);
        }

        public static void SetControlByte(this byte[] command, MessageType messageType)
        {
            // Set message type
            command[0] = Convert.ToByte((int)messageType);
        }

        public static void ExtractControlByte(this byte[] command, out MessageType messageType)
        {
            messageType = (MessageType)command[0];
        }
    }
}
