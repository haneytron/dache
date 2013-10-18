using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Dache.Communication.ClientToCache;
using Dache.Core.DataStructures.Interfaces;
using Dache.Core.CacheHost.Storage;
using Dache.Core.CacheHost.State;
using Dache.Core.CacheHost.Communication.CacheToManager;
using Dache.Core.DataStructures.Routing;

namespace Dache.Core.CacheHost
{
    /// <summary>
    /// The engine which runs the cache host.
    /// </summary>
    public class CacheHostEngine : IRunnable
    {
        // The WCF client to cache service host
        private readonly ServiceHost _clientToCacheServiceHost = null;
        // The cache host information poller
        private readonly IRunnable _cacheHostInformationPoller = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheHostInformationPoller">The cache host information poller.</param>
        /// <param name="memCache">The mem cache to use for storing objects.</param>
        /// <param name="clientToCacheServiceHost">The client to cache service host.</param>
        /// <param name="cacheManagerClient">The cache manager client.</param>
        public CacheHostEngine(IRunnable cacheHostInformationPoller, MemCache memCache, ServiceHost clientToCacheServiceHost, ICacheManagerClient cacheManagerClient)
        {
            // Sanitize
            if (cacheHostInformationPoller == null)
            {
                throw new ArgumentNullException("cacheHostInformationPoller");
            }
            if (memCache == null)
            {
                throw new ArgumentNullException("memCache");
            }
            if (clientToCacheServiceHost == null)
            {
                throw new ArgumentNullException("clientToCacheServiceHost");
            }
            if (cacheManagerClient == null)
            {
                throw new ArgumentNullException("cacheManagerClient");
            }

            // Set the cache host information poller
            _cacheHostInformationPoller = cacheHostInformationPoller;

            // Initialize the routing table container with a fresh routing table
            RoutingTableContainer.Instance = new RoutingTable();

            // Set the mem cache container instance
            MemCacheContainer.Instance = memCache;

            // Initialize the service hosts
            _clientToCacheServiceHost = clientToCacheServiceHost;

            // Set the cache host address
            CacheHostInformation.HostAddress = clientToCacheServiceHost.Description.Endpoints.First().Address.Uri.ToString();

            // Set the cache manager client
            ManagerClientContainer.Instance = cacheManagerClient;
        }

        /// <summary>
        /// Starts the cache host engine.
        /// </summary>
        public void Start()
        {
            // Register with the cache manager
            ManagerClientContainer.Instance.Register(CacheHostInformation.HostAddress, MemCacheContainer.Instance.GetCount());

            // Begin listening for WCF requests
            _clientToCacheServiceHost.Open();

            // Start the cache host information poller
            _cacheHostInformationPoller.Start();
        }

        /// <summary>
        /// Stops the cache host engine.
        /// </summary>
        public void Stop()
        {
            // Deregister with the cache manager by closing the manager client connection
            ManagerClientContainer.Instance.CloseConnection();

            // Deregister all cache clients
            CacheHostManager.DeregisterAll();

            // Stop listening for WCF requests
            _clientToCacheServiceHost.Close();

            // Stop the cache host information poller
            _cacheHostInformationPoller.Stop();

            // Dispose the MemCache
            MemCacheContainer.Instance.Dispose();
        }
    }
}
