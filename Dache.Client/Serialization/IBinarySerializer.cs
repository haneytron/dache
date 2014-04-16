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
        /// <returns>A byte array of the serialized object, or null if the object was null.</returns>
        byte[] Serialize(object value);
        
        /// <summary>
        /// Deserializes a byte array into an object.
        /// </summary>
        /// <param name="bytes">The byte array.</param>
        /// <returns>An object, or null if the byte array was null.</returns>
        object Deserialize(byte[] bytes);
    }
}
