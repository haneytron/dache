using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dache.Client.Serialization
{
    /// <summary>
    /// Serializes and deserializes objects to and from binary. Thread safe.
    /// </summary>
    internal class BinarySerializer : IBinarySerializer
    {
        /// <summary>
        /// Serializes an object to byte array.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        public byte[] Serialize(object value)
        {
            // Sanitize
            if (value == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream())
            {
                new BinaryFormatter().Serialize(memoryStream, value);

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>An object, or null if the byte array was null.</returns>
        public object Deserialize(byte[] bytes)
        {
            // Sanitize
            if (bytes == null)
            {
                return null;
            }

            using (var memoryStream = new MemoryStream(bytes))
            {
                return new BinaryFormatter().Deserialize(memoryStream);
            }
        }
    }
}
