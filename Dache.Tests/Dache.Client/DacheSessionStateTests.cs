using System;
using Dache.Client.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dache.Client.Plugins.SessionState;
using ProtoBuf;
using System.IO;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class DacheSessionStateTests
    {
        [TestMethod]
        public void DacheSessionState_SerializedAndDeserializedByProtoBuf_ShouldSucceed()
        {
            byte[] serialized = null;
            DacheSessionState deserialized = null;

            using (var memoryStream = new MemoryStream())
            {
                Serializer.Serialize(memoryStream, new DacheSessionState());
                serialized = memoryStream.ToArray();
            }

            Assert.IsNotNull(serialized);
            Assert.IsTrue(serialized.Length > 0);

            using (var memoryStream = new MemoryStream(serialized))
            {
                deserialized = Serializer.Deserialize<DacheSessionState>(memoryStream);
            }

            Assert.IsNotNull(deserialized);
        }
    }
}
