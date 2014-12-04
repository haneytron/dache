using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dache.CacheHost.Routing;
using Dache.CacheHost.Storage;
using Dache.Core.Communication;
using Dache.Core.Logging;
using Dache.Core.Performance;

namespace Dache.CacheHost
{
    /// <summary>
    /// The cache host engine. Instantiate this to host an instance of the cache host within your own process.
    /// </summary>
    public class CacheHostEngine
    {
        // The cache host runner
        private readonly IRunnable _cacheHostRunner = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="memCache">The mem cache to use.</param>
        /// <param name="logger">The logger to use.</param>
        /// <param name="port">The port to open.</param>
        /// <param name="physicalMemoryLimitPercentage">The cache memory limit, as a percentage of physical memory.</param>
        /// <param name="maximumConnections">The maximum number of simultaneous connections permitted to the cache host.</param>
        /// <param name="messageBufferSize">The message buffer size.</param>
        /// <param name="timeoutMilliseconds">The communication timeout, in milliseconds.</param>
        /// <param name="maxMessageSize">The maximum message size, in bytes.</param>
        public CacheHostEngine(IMemCache memCache, ILogger logger, int port, int physicalMemoryLimitPercentage = 85, int maximumConnections = 20, int messageBufferSize = 1024, 
            int timeoutMilliseconds = 10000, int maxMessageSize = 100 * 1024 * 1024)
        {
            // Sanitize
            if (memCache == null)
            {
                throw new ArgumentNullException("memCache");
            }
            if (logger == null)
            {
                throw new ArgumentNullException("logger");
            }
            if (port <= 0)
            {
                throw new ArgumentException("cannot be <= 0", "port");
            }
            if (physicalMemoryLimitPercentage < 5 || physicalMemoryLimitPercentage > 90)
            {
                throw new ArgumentException("must be >= 5 and <= 90", "physicalMemoryLimitPercentage");
            }

            // Initialize the tag routing table
            var tagRoutingTable = new TagRoutingTable();

            // Initialize the cache host server
            var cacheHostServer = new CacheHostServer(memCache, tagRoutingTable, logger, port, maximumConnections, messageBufferSize, timeoutMilliseconds, maxMessageSize);

            // Instantiate the cache host runner
            _cacheHostRunner = new CacheHostRunner(cacheHostServer);
        }

        /// <summary>
        /// Starts the cache host.
        /// </summary>
        public void Start()
        {
            _cacheHostRunner.Start();
        }

        /// <summary>
        /// Stops the cache host.
        /// </summary>
        public void Stop()
        {
            _cacheHostRunner.Stop();
        }
    }
}
