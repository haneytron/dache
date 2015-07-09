using System;
using Dache.Client.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dache.Client.Plugins.SessionState;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class DacheSessionStateProviderTests
    {
        [TestMethod]
        public void DacheSessionStateProvider_StaticConstructorNoConfig_ShouldThrowTypeInitializationException()
        {
            try
            {
                var dacheSessionStateProvider = new DacheSessionStateProvider();
            }
            catch (TypeInitializationException)
            {
                return;
            }

            Assert.Fail();
        }
    }
}
