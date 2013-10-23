using System;
using System.ServiceModel;
using Dache.Core.CacheHost.Storage;
using Dache.Core.Interfaces;

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
        public CacheHostEngine(IRunnable cacheHostInformationPoller, MemCache memCache, ServiceHost clientToCacheServiceHost)
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

            // Set the cache host information poller
            _cacheHostInformationPoller = cacheHostInformationPoller;

            // Set the mem cache container instance
            MemCacheContainer.Instance = memCache;

            // Initialize the service hosts
            _clientToCacheServiceHost = clientToCacheServiceHost;
        }

        /// <summary>
        /// Starts the cache host engine.
        /// </summary>
        public void Start()
        {
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
            // Dispose the MemCache guaranteed
            using (MemCacheContainer.Instance)
            {
                // Stop listening for WCF requests
                _clientToCacheServiceHost.Close();

                // Stop the cache host information poller
                _cacheHostInformationPoller.Stop();
            }
        }
    }
}
