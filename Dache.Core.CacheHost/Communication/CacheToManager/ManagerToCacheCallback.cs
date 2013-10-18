using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dache.Communication.CacheToManager;
using Dache.Core.DataStructures.Routing;
using Dache.Core.CacheHost.State;
using Dache.Core.CacheHost.Communication.ClientToCache;
using Dache.Core.CacheHost.Storage;
using Dache.Core.CacheHost.Communication.CacheToCache;

namespace Dache.Core.CacheHost.Communication.CacheToManager
{
    /// <summary>
    /// The WCF callback client for manager to cache communication. Allows the manager to communicate with a cache instance.
    /// </summary>
    public class ManagerToCacheCallback : IManagerToCacheCallbackContract
    {
        /// <summary>
        /// Registers a host.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        /// <param name="registrantIndex">The registrant index.</param>
        /// <param name="highestRegistrantIndex">The highest registrant index.</param>
        public void RegisterHost(string hostAddress, int registrantIndex, int highestRegistrantIndex)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }

            // Instantiate a cache client with the host address
            // TODO: make this configurable
            var cacheClient = new CacheToCacheClient(hostAddress, 5000);

            // Register the host
            CacheHostManager.Register(hostAddress, cacheClient, registrantIndex, highestRegistrantIndex);
        }

        /// <summary>
        /// Deregisters a host.
        /// </summary>
        /// <param name="hostAddress">The host address.</param>
        public void DeregisterHost(string hostAddress)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(hostAddress))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "hostAddress");
            }

            // Deregister the host
            CacheHostManager.Deregister(hostAddress);
        }
    }
}
