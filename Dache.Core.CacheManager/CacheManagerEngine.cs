using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using Dache.Core.DataStructures.Interfaces;
using Dache.Core.DataStructures.Routing;

namespace Dache.Core.CacheManager
{
    /// <summary>
    /// The engine which runs the cache manager.
    /// </summary>
    public class CacheManagerEngine : IRunnable
    {
        // The WCF cache to manager service host
        private readonly ServiceHost _cacheToManagerServiceHost = null;
        // The WCF Dacheboard to manager service host
        private readonly ServiceHost _boardToManagerServiceHost = null;
        // The cache host information poller
        private readonly IRunnable _cacheHostInformationPoller = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheHostInformationPoller">The cache host information poller.</param>
        /// <param name="clientToCacheServiceHost">The cache to manager service host.</param>
        /// <param name="boardToManagerServiceHost">The Dacheboard to manager service host.</param>
        public CacheManagerEngine(IRunnable cacheHostInformationPoller, ServiceHost cacheToManagerServiceHost, ServiceHost boardToManagerServiceHost)
        {
            // Sanitize
            if (cacheHostInformationPoller == null)
            {
                throw new ArgumentNullException("cacheHostInformationPoller");
            }
            if (cacheToManagerServiceHost == null)
            {
                throw new ArgumentNullException("cacheToManagerServiceHost");
            }
            if (boardToManagerServiceHost == null)
            {
                throw new ArgumentNullException("boardToManagerServiceHost");
            }

            // Initialize the service hosts
            _cacheToManagerServiceHost = cacheToManagerServiceHost;
            _boardToManagerServiceHost = boardToManagerServiceHost;

            // Set the cache host information poller
            _cacheHostInformationPoller = cacheHostInformationPoller;

            // Initialize the routing table container with a fresh routing table
            RoutingTableContainer.Instance = new RoutingTable();
        }

        /// <summary>
        /// Starts the cache manager engine.
        /// </summary>
        public void Start()
        {
            // Begin listening for WCF requests
            _cacheToManagerServiceHost.Open();
            _boardToManagerServiceHost.Open();

            // Start the cache host information poller
            _cacheHostInformationPoller.Start();
        }

        /// <summary>
        /// Stops the cache manager engine.
        /// </summary>
        public void Stop()
        {
            // Stop listening for WCF requests
            _cacheToManagerServiceHost.Close();
            _boardToManagerServiceHost.Close();

            // Stop the cache host information poller
            _cacheHostInformationPoller.Stop();
        }
    }
}
