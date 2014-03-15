using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Dache.CacheHost.Storage;
using Dache.Core.Performance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Dache.Tests.Dache.CacheHost
{
    [TestClass]
    public class GZipMemCacheTests
    {
        GZipMemCacheAccessor sut;
        Mock<ICustomPerformanceCounterManager> customPerformanceCounterManagerMock;

        [TestInitialize]
        public void Initialize()
        {
            customPerformanceCounterManagerMock = new Mock<ICustomPerformanceCounterManager>();
            sut = new GZipMemCacheAccessor("Dache", 20, customPerformanceCounterManagerMock.Object);
        }

        [TestMethod]
        public void CompressDecompress_ValueDoesntChange()
        {
            string objectToBeCached = @"Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

            byte[] uncompressedBytes = Encoding.Unicode.GetBytes(objectToBeCached);
            byte[] compressedBytes = sut.Compress(uncompressedBytes).Result;

            long uncompressedSize = GetSize(uncompressedBytes);
            long compressedSize = GetSize(compressedBytes);

            string cachedObject = Encoding.Unicode.GetString(sut.Decompress(compressedBytes).Result);

            Assert.AreEqual(objectToBeCached, cachedObject);
            Assert.IsTrue(compressedSize < uncompressedSize);
        }

        protected long GetSize(object obj)
        {
            using (MemoryStream s = new MemoryStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                formatter.Serialize(s, obj);
                return s.Length;
            }
        }
    }

    public class GZipMemCacheAccessor : GZipMemCache
    {
        public GZipMemCacheAccessor(string cacheName, int physicalMemoryLimitPercentage, ICustomPerformanceCounterManager customPerformanceCounterManager)
            : base(cacheName, physicalMemoryLimitPercentage, customPerformanceCounterManager)
        { }

        public new async Task<byte[]> Compress(byte[] value)
        {
            return await base.Compress(value);
        }

        public new async Task<byte[]> Decompress(byte[] value)
        {
            return await base.Decompress(value);
        }
    }
}
