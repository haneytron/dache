using System;
using Dache.Client.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class ProtoBufSerializerTests
    {
        [TestMethod]
        public void ProtoBufSerializer_Serialize_GivenNullInput_ShouldReturnNull()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var result = protoBufSerializer.Serialize<object>(null);
            
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ProtoBufSerializer_Serialize_GivenValidValueTypeInput_ShouldReturnBytes()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var input = 123;
            var result = protoBufSerializer.Serialize(input);
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void ProtoBufSerializer_Serialize_GivenValidReferenceTypeInput_ShouldReturnBytes()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var input = "test";
            var result = protoBufSerializer.Serialize(input);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void ProtoBufSerializer_Deserialize_GivenNullInput_ShouldReturnNull()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var result = protoBufSerializer.Deserialize<object>(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ProtoBufSerializer_Deserialize_GivenValidValueType_ShouldReturnValueType()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var input = 123;
            var serialized = protoBufSerializer.Serialize(input);

            var deserialized = protoBufSerializer.Deserialize<int>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(123, deserialized);
        }

        [TestMethod]
        public void ProtoBufSerializer_Deserialize_GivenValidReferenceType_ShouldReturnReferenceType()
        {
            var protoBufSerializer = new ProtoBufSerializer();
            var input = "test";
            var serialized = protoBufSerializer.Serialize(input);

            var deserialized = protoBufSerializer.Deserialize<string>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("test", deserialized);
        }
    }
}
