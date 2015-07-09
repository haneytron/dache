using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;

namespace Dache.Client.Serialization
{
    /// <summary>
    /// Compresses/decompress the objects then serializes/deserializes them.
    /// </summary>
    internal class GZipSerializer : IBinarySerializer
    {
        /// <summary>
        /// Compresses and serializes an object to byte array.
        /// </summary>
        /// <param name="value">The object that should be compressed and serialized.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        /// <exception cref="System.ArgumentNullException">value;Can't compress null value</exception>
        public byte[] Serialize<T>(T value)
        {
            // Sanitize
            if (value == null)
            {
                throw new ArgumentNullException("value", "Can't compress null value");
            }

            using (MemoryStream compressedStream = new MemoryStream())
            {
                using (GZipStream compressionStream = new GZipStream(compressedStream, CompressionMode.Compress, true))
                {
                    new BinaryFormatter().Serialize(compressionStream, value);
                }

                return compressedStream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes and decompresses a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array that should be decompressed and deserialized.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>A deserialized and decomressed object, or null if the byte array was null.</returns>
        public T Deserialize<T>(byte[] bytes)
        {
            // Sanitize
            if (bytes == null)
            {
                return default(T);
            }

            using (MemoryStream originalStream = new MemoryStream(bytes))
            {
                using (GZipStream decompressionStream = new GZipStream(originalStream, CompressionMode.Decompress, false))
                {
                    return (T)new BinaryFormatter().Deserialize(decompressionStream);
                }
            }
        }
    }
}
