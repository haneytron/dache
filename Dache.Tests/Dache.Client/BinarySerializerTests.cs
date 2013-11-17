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
            var result = binarySerializer.Serialize(null);
            
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
            var result = binarySerializer.Deserialize(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void BinarySerializer_Deserialize_GivenValidValueTypeAsBytesInput_ShouldReturnValueType()
        {
            var binarySerializer = new BinarySerializer();
            // int value of 123 as bytes
            var serializedInt = new byte[]
            { 0, 1, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0, 0, 0, 0, 0, 4, 1, 0, 0, 0, 12, 83, 121, 115, 116, 101, 
                109, 46, 73, 110, 116, 51, 50, 1, 0, 0, 0, 7, 109, 95, 118, 97, 108, 117, 101, 0, 8, 123, 0, 0, 0, 11 };

            var resultObject = binarySerializer.Deserialize(serializedInt);

            Assert.IsNotNull(resultObject);

            var deserializedInt = (int)resultObject;

            Assert.AreEqual(123, deserializedInt);
        }

        [TestMethod]
        public void BinarySerializer_Deserialize_GivenValidReferenceTypeAsBytesInput_ShouldReturnReferenceType()
        {
            var binarySerializer = new BinarySerializer();
            // string value of "test" as bytes
            var serializedString = new byte[] { 0, 1, 0, 0, 0, 255, 255, 255, 255, 1, 0, 0, 0, 0, 0, 0, 0, 6, 1, 0, 0, 0, 4, 116, 101, 115, 116, 11 };


            var resultObject = binarySerializer.Deserialize(serializedString);

            Assert.IsNotNull(resultObject);

            var deserializedString = resultObject as string;

            Assert.IsNotNull(deserializedString);
            Assert.AreEqual("test", deserializedString);
        }

        [TestMethod]
        public void BinarySerializer_Serialize_Deserialize_ShouldReturnOriginalResult()
        {
            var binarySerializer = new BinarySerializer();
            var input = "test";
            var serializedResult = binarySerializer.Serialize(input);

            Assert.IsNotNull(serializedResult);

            var deserializedResult = binarySerializer.Deserialize(serializedResult);

            Assert.IsNotNull(deserializedResult);

            var resultString = deserializedResult as string;

            Assert.IsNotNull(resultString);
            Assert.AreEqual("test", resultString);
        }
    }
}
