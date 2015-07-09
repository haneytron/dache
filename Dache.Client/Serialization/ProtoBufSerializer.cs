using ProtoBuf;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dache.Client.Serialization
{
    /// <summary>
    /// Serializes and deserializes objects to and from binary using ProtoBuf. Thread safe.
    /// </summary>
    internal class ProtoBufSerializer : IBinarySerializer
    {
        /// <summary>
        /// Serializes an object to byte array.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        public byte[] Serialize<T>(T value)
        {
            // Sanitize
            if (value == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, value);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>An object, or null if the byte array was null.</returns>
        public T Deserialize<T>(byte[] bytes)
        {
            // Sanitize
            if (bytes == null)
            {
                return default(T);
            }

            using (var memoryStream = new MemoryStream(bytes))
            {
                return Serializer.Deserialize<T>(memoryStream);
            }
        }
    }
}
