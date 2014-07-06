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

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="cacheHostServer">The cache host server.</param>
        public CacheHostRunner(IRunnable cacheHostServer)
        {
            // Sanitize
            if (cacheHostServer == null)
            {
                throw new ArgumentNullException("cacheHostServer");
            }

            _cacheServer = cacheHostServer;
        }

        /// <summary>
        /// Starts the cache host.
        /// </summary>
        public void Start()
        {
            // Begin listening for requests
            _cacheServer.Start();
        }

        /// <summary>
        /// Stops the cache host.
        /// </summary>
        public void Stop()
        {
            // Stop listening for requests
            _cacheServer.Stop();
        }
    }
}
