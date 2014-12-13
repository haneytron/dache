using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace Dache.Core.Communication
{
    /// <summary>
    /// Assists in communicating via the Dache protocol.
    /// </summary>
    public static class DacheProtocolHelper
    {
        /// <summary>
        /// The communication encoding.
        /// </summary>
        public static readonly Encoding CommunicationEncoding = Encoding.UTF8;

        /// <summary>
        /// The absolute expiration format.
        /// </summary>
        public const string AbsoluteExpirationFormat = "yyMMddHHmmss";

        /// <summary>
        /// Writes a value to the memory stream, prefixing it with Little Endian bytes.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="value">The value.</param>
        public static void Write(this MemoryStream memoryStream, string value)
        {
            memoryStream.Write(CommunicationEncoding.GetBytes(value));
        }

        /// <summary>
        /// Writes bytes to the memory stream with a Little Endian prefix.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="bytes">The bytes.</param>
        public static void Write(this MemoryStream memoryStream, byte[] bytes)
        {
            var length = bytes.Length;
            memoryStream.WriteByte((byte)length);
            memoryStream.WriteByte((byte)((length >> 8) & 0xFF));
            memoryStream.WriteByte((byte)((length >> 16) & 0xFF));
            memoryStream.WriteByte((byte)((length >> 24) & 0xFF));
            
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Extracts a byte sequence from a buffer for a given position, by reading the Little Endian prefix.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <param name="position">The position to extract from. This will be set to the new position.</param>
        /// <returns>The extracted bytes.</returns>
        public static byte[] Extract(byte[] buffer, ref int position)
        {
            var length = (buffer[position + 3] << 24) | (buffer[position + 2] << 16) | (buffer[position + 1] << 8) | buffer[position];
            byte[] result = new byte[length];
            Buffer.BlockCopy(buffer, position + 4, result, 0, length);
            position = position + 4 + length;
            return result;
        }
    }
}
