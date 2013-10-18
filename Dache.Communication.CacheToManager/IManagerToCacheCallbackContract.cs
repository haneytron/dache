using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using Dache.Core.DataStructures.Routing;

namespace Dache.Communication.CacheToManager
{
    /// <summary>
    /// Represents the communication callback contract between the cache manager and a cache instance.
    /// </summary>
    [ServiceContract(Namespace = "http://schemas.getdache.net/cachemanager", Name = "A")]
    public interface IManagerToCacheCallbackContract
    {
        /// <summary>
        /// Registers a host.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="registrantIndex">The registrant index.</param>
        /// <param name="highestRegistrantIndex">The highest registrant index.</param>
        [OperationContract(Name = "A", IsOneWay = true)]
        void RegisterHost(string hostAddress, int registrantIndex, int highestRegistrantIndex);

        /// <summary>
        /// Deregisters a host.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        [OperationContract(Name = "B", IsOneWay = true)]
        void DeregisterHost(string hostAddress);
    }
}
