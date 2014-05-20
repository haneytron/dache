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
        /// The message type.
        /// </summary>
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
        /// Writes the control byte placeholder to the memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        public static void WriteControlBytePlaceHolder(this MemoryStream memoryStream)
        {
            memoryStream.Write(ControlByteDefault, 0, ControlByteDefault.Length);
        }

        /// <summary>
        /// Writes a space to the memory stream.
        /// </summary>
        /// <param name="memoryStream">The memory stream.</param>
        public static void WriteSpace(this MemoryStream memoryStream)
        {
            memoryStream.Write(SpaceByte, 0, SpaceByte.Length);
        }

        /// <summary>
        /// Sets the control byte to the specified message type in a command byte array.
        /// </summary>
        /// <param name="command">The command byte array.</param>
        /// <param name="messageType">The message type.</param>
        public static void SetControlByte(this byte[] command, MessageType messageType)
        {
            // Set message type
            command[0] = Convert.ToByte((int)messageType);
        }

        /// <summary>
        /// Extracts the message type from the control byte in a command byte array.
        /// </summary>
        /// <param name="command">The command byte array.</param>
        /// <param name="messageType">The message type.</param>
        public static void ExtractControlByte(this byte[] command, out MessageType messageType)
        {
            messageType = (MessageType)command[0];
        }
    }
}
