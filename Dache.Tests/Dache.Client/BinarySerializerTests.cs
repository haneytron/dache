using System;
using Dache.Client.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class BinarySerializerTests
    {
        [TestMethod]
        public void BinarySerializer_Serialize_GivenNullInput_ShouldReturnNull()
        {
            var binarySerializer = new BinarySerializer();
            var result = binarySerializer.Serialize<object>(null);
            
            Assert.IsNull(result);
        }

        [TestMethod]
        public void BinarySerializer_Serialize_GivenValidValueTypeInput_ShouldReturnBytes()
        {
            var binarySerializer = new BinarySerializer();
            var input = 123;
            var result = binarySerializer.Serialize(input);
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void BinarySerializer_Serialize_GivenValidReferenceTypeInput_ShouldReturnBytes()
        {
            var binarySerializer = new BinarySerializer();
            var input = "test";
            var result = binarySerializer.Serialize(input);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void BinarySerializer_Deserialize_GivenNullInput_ShouldReturnNull()
        {
            var binarySerializer = new BinarySerializer();
            var result = binarySerializer.Deserialize<object>(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void BinarySerializer_Deserialize_GivenValidValueType_ShouldReturnValueType()
        {
            var binarySerializer = new BinarySerializer();
            var input = 123;
            var serialized = binarySerializer.Serialize(input);

            var deserialized = binarySerializer.Deserialize<int>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(123, deserialized);
        }

        [TestMethod]
        public void BinarySerializer_Deserialize_GivenValidReferenceType_ShouldReturnReferenceType()
        {
            var binarySerializer = new BinarySerializer();
            var input = "test";
            var serialized = binarySerializer.Serialize(input);

            var deserialized = binarySerializer.Deserialize<string>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("test", deserialized);
        }
    }
}
