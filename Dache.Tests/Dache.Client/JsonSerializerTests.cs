using System;
using Dache.Client.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class JsonSerializerTests
    {
        [TestMethod]
        public void JsonSerializer_Serialize_GivenNullInput_ShouldReturnNull()
        {
            var jsonSerializer = new JsonSerializer();
            var result = jsonSerializer.Serialize<object>(null);
            
            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonSerializer_Serialize_GivenValidValueTypeInput_ShouldReturnBytes()
        {
            var jsonSerializer = new JsonSerializer();
            var input = 123;
            var result = jsonSerializer.Serialize(input);
            
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void JsonSerializer_Serialize_GivenValidReferenceTypeInput_ShouldReturnBytes()
        {
            var jsonSerializer = new JsonSerializer();
            var input = "test";
            var result = jsonSerializer.Serialize(input);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
        }

        [TestMethod]
        public void JsonSerializer_Deserialize_GivenNullInput_ShouldReturnNull()
        {
            var jsonSerializer = new JsonSerializer();
            var result = jsonSerializer.Deserialize<object>(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void JsonSerializer_Deserialize_GivenValidValueType_ShouldReturnValueType()
        {
            var jsonSerializer = new JsonSerializer();
            var input = 123;
            var serialized = jsonSerializer.Serialize(input);

            var deserialized = jsonSerializer.Deserialize<int>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual(123, deserialized);
        }

        [TestMethod]
        public void JsonSerializer_Deserialize_GivenValidReferenceType_ShouldReturnReferenceType()
        {
            var jsonSerializer = new JsonSerializer();
            var input = "test";
            var serialized = jsonSerializer.Serialize(input);

            var deserialized = jsonSerializer.Deserialize<string>(serialized);

            Assert.IsNotNull(deserialized);
            Assert.AreEqual("test", deserialized);
        }
    }
}
