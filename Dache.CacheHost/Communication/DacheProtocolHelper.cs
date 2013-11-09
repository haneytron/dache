using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Dache.CacheHost.Communication
{
    public static class DacheProtocolHelper
    {
        // The communication encoding
        public static readonly Encoding CommunicationEncoding = Encoding.UTF8;
        // The communication protocol control bytes default - 4 little endian bytes for message length + 4 little endian bytes for thread id + 1 control byte for message type
        public static readonly byte[] ControlBytesDefault = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        // The byte that represents a space
        public static readonly byte[] SpaceByte = CommunicationEncoding.GetBytes(" ");
        // The absolute expiration format
        public const string AbsoluteExpirationFormat = "yyMMddhhmmss";

        public static byte[] Combine(byte[] first, byte[] second)
        {
            return Combine(first, first.Length, second, second.Length);
        }

        public static byte[] Combine(byte[] first, int firstLength, byte[] second, int secondLength)
        {
            byte[] ret = new byte[firstLength + secondLength];
            Buffer.BlockCopy(first, 0, ret, 0, firstLength);
            Buffer.BlockCopy(second, 0, ret, firstLength, secondLength);
            return ret;
        }

        public class StateObject
        {
            public StateObject(int bufferSize)
            {
                Buffer = new byte[bufferSize];
            }

            public readonly byte[] Buffer = null;

            public Socket WorkSocket = null;
            public byte[] Data = new byte[0];
            public MessageType MessageType = MessageType.Literal;
            public int ThreadId = -1;
            public int TotalBytesToRead = -1;
        }

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

        public static void WriteControlBytesDefault(this MemoryStream memoryStream)
        {
            memoryStream.Write(ControlBytesDefault, 0, ControlBytesDefault.Length);
        }

        public static void WriteBase64(this MemoryStream memoryStream, byte[] value)
        {
            var bytes = CommunicationEncoding.GetBytes(Convert.ToBase64String(value));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteSpace(this MemoryStream memoryStream)
        {
            memoryStream.Write(SpaceByte, 0, SpaceByte.Length);
        }

        public static void SetControlBytes(this byte[] command, int threadId, MessageType delimiterType)
        {
            var length = command.Length - ControlBytesDefault.Length;
            // Set little endian message length
            command[0] = (byte)length;
            command[1] = (byte)((length >> 8) & 0xFF);
            command[2] = (byte)((length >> 16) & 0xFF);
            command[3] = (byte)((length >> 24) & 0xFF);

            // Set little endian thread id
            command[4] = (byte)threadId;
            command[5] = (byte)((threadId >> 8) & 0xFF);
            command[6] = (byte)((threadId >> 16) & 0xFF);
            command[7] = (byte)((threadId >> 24) & 0xFF);

            // Set message type
            command[8] = Convert.ToByte((int)delimiterType);
        }

        public static byte[] RemoveControlByteValues(this byte[] command, out int messageLength, out int threadId, out MessageType delimiterType)
        {
            messageLength = (command[3] << 24) | (command[2] << 16) | (command[1] << 8) | command[0];
            threadId = (command[7] << 24) | (command[6] << 16) | (command[5] << 8) | command[4];
            delimiterType = (MessageType)command[8];
            var result = new byte[command.Length - ControlBytesDefault.Length];
            Buffer.BlockCopy(command, 5, result, 0, result.Length);
            return result;
        }
    }
}
