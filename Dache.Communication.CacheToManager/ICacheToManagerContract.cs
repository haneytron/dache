using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;

namespace Dache.Communication.CacheToManager
{
    /// <summary>
    /// Represents the communication contract between a cache instance and the cache manager.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(IManagerToCacheCallbackContract), Namespace = "http://schemas.getdache.net/cachemanager", Name = "A")]
    public interface ICacheToManagerContract
    {
        /// <summary>
        /// Registers the cache instance with the cache manager.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="cachedObjectCount">The cached object count.</param>
        [OperationContract(Name = "A", IsOneWay = true)]
        void Register(string hostAddress, long cachedObjectCount);
    }
}
