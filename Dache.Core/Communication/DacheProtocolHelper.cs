using System;
using System.IO;
using System.Text;

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
        /// The communication protocol control byte default - 1 control byte for message type.
        /// </summary>
        public static readonly byte[] ControlByteDefault = new byte[] { 0 };

        /// <summary>
        /// The byte that represents a space.
        /// </summary>
        public static readonly byte[] SpaceByte = CommunicationEncoding.GetBytes(" ");

        /// <summary>
        /// The absolute expiration format.
        /// </summary>
        public const string AbsoluteExpirationFormat = "yyMMddHHmmss";

        /// <summary>
        /// Writes a value to the memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="value">The value.</param>
        public static void Write(this MemoryStream memoryStream, string value)
        {
            var bytes = CommunicationEncoding.GetBytes(value);
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes a value to the memory stream using string format convention.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="format">The string format.</param>
        /// <param name="args">The arguments.</param>
        public static void Write(this MemoryStream memoryStream, string format, params object[] args)
        {
            var bytes = CommunicationEncoding.GetBytes(string.Format(format, args));
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes bytes to the memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="bytes">The bytes.</param>
        public static void Write(this MemoryStream memoryStream, byte[] bytes)
        {
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes bytes to the memory stream as a base-64 encoded string.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        /// <param name="bytes">The bytes.</param>
        public static void WriteBase64(this MemoryStream memoryStream, byte[] bytes)
        {
            var encodedBytes = CommunicationEncoding.GetBytes(Convert.ToBase64String(bytes));
            memoryStream.Write(encodedBytes, 0, encodedBytes.Length);
        }

        /// <summary>
        /// Writes a space to the memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        public static void WriteSpace(this MemoryStream memoryStream)
        {
            memoryStream.Write(SpaceByte, 0, SpaceByte.Length);
        }
    }
}
