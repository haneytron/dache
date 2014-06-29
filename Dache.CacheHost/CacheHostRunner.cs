using System;

namespace Dache.CacheHost
{
    /// <summary>
    /// Runs the cache host.
    /// </summary>
    internal class CacheHostRunner : IRunnable
    {
        // The cache server
        private readonly IRunnable _cacheServer = null;
        // The cache host information poller
        private readonly IRunnable _cacheHostInformationPoller = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheHostInformationPoller">The cache host information poller.</param>
        /// <param name="cacheHostServer">The cache host server.</param>
        public CacheHostRunner(IRunnable cacheHostInformationPoller, IRunnable cacheHostServer)
        {
            // Sanitize
            if (cacheHostInformationPoller == null)
            {
                throw new ArgumentNullException("cacheHostInformationPoller");
            }
            if (cacheHostServer == null)
            {
                throw new ArgumentNullException("cacheHostServer");
            }

            // Set the cache host information poller
            _cacheHostInformationPoller = cacheHostInformationPoller;

            // Initialize the serer
            _cacheServer = cacheHostServer;
        }

        /// <summary>
        /// Starts the cache host.
        /// </summary>
        public void Start()
        {
            // Begin listening for requests
            _cacheServer.Start();

            // Start the cache host information poller
            _cacheHostInformationPoller.Start();
        }

        /// <summary>
        /// Stops the cache host.
        /// </summary>
        public void Stop()
        {
            // Stop listening for requests
            _cacheServer.Stop();

            // Stop the cache host information poller
            _cacheHostInformationPoller.Stop();
        }
    }
}
