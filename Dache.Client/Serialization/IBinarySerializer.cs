
namespace Dache.Client.Serialization
{
    /// <summary>
    /// Represents a binary serializer. You can implement this interface to inject your own custom serialization into a Dache client.
    /// NOTE: your custom serializer should be thread safe.
    /// </summary>
    public interface IBinarySerializer
    {
        /// <summary>
        /// Serializes an object to byte array.
        /// </summary>
        /// <param name="value">The object.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        byte[] Serialize<T>(T value);
        
        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <typeparam name="T">The type of the object.</typeparam>
        /// <returns>An object, or null if the byte array was null.</returns>
        T Deserialize<T>(byte[] bytes);
    }
}
