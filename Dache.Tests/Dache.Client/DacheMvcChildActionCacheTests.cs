using System;
using Dache.Client;
using Dache.Client.Plugins.OutputCache;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class DacheMvcChildActionCacheTests
    {
        [TestMethod]
        public void DacheMvcChildActionCache_Constructor_GivenNullInput_ShouldThrowArgumentNullException()
        {
            try
            {
                var dacheMvcChildActionCache = new DacheMvcChildActionCache(null);
            }
            catch (ArgumentNullException)
            {
                // Good
                return;
            }

            Assert.Fail("ArgumentNullException was not thrown");
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Constructor_GivenValidInput_ShouldNotThrowException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Add_GivenNullCacheKey_ShouldThrowArgumentException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            try
            {
                dacheMvcChildActionCache.Add(null, new object(), DateTime.Now.AddDays(1));
            }
            catch (ArgumentException)
            {
                // Good
                return;
            }

            Assert.Fail("ArgumentException was not thrown");
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Add_GivenNullObject_ShouldThrowArgumentNullException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            try
            {
                dacheMvcChildActionCache.Add("test", null, DateTime.Now.AddDays(1));
            }
            catch (ArgumentNullException)
            {
                // Good
                return;
            }

            Assert.Fail("ArgumentNullException was not thrown");
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Add_GivenNullCacheKeyAndNullObject_ShouldThrowArgumentException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            try
            {
                dacheMvcChildActionCache.Add(null, null, DateTime.Now.AddDays(1));
            }
            catch (ArgumentException)
            {
                // Good
                return;
            }

            Assert.Fail("ArgumentException was not thrown");
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Add_GivenValidInput_ShouldNotThrowException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            dacheMvcChildActionCache.Add("test", new object(), DateTime.Now.AddDays(1));
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Get_GivenNullCacheKey_ShouldThrowArgumentException()
        {
            var cacheClient = new Mock<ICacheClient>();

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            try
            {
                dacheMvcChildActionCache.Get(null);
            }
            catch (ArgumentException)
            {
                // Good
                return;
            }

            Assert.Fail("ArgumentException was not thrown");
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Get_GivenValidInputAndValueNotFoundInCache_ShouldReturnNull()
        {
            var cacheClient = new Mock<ICacheClient>();
            object value = null;
            cacheClient.Setup(i => i.TryGet<object>(It.IsAny<string>(), out value, false )).Returns(false);

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            var result = dacheMvcChildActionCache.Get("test");

            Assert.IsNull(result);
        }

        [TestMethod]
        public void DacheMvcChildActionCache_Get_GivenValidInputAndValueFoundInCache_ShouldReturnValue()
        {
            var cacheClient = new Mock<ICacheClient>();
            object value = new object();
            cacheClient.Setup(i => i.TryGet<object>(It.IsAny<string>(), out value, false)).Returns(true);

            var dacheMvcChildActionCache = new DacheMvcChildActionCache(cacheClient.Object);

            var result = dacheMvcChildActionCache.Get("test");

            Assert.IsNotNull(result);
            Assert.AreEqual(value, result);
        }
    }
}
