using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Dache.Core.DataStructures.Interfaces;
using Dache.Core.CacheManager.State;

namespace Dache.Core.CacheManager.Polling
{
    /// <summary>
    /// Polls cache hosts for information.
    /// </summary>
    public class CacheHostInformationPoller : IRunnable
    {
        // The polling interval in milliseconds
        private readonly int _pollingIntervalMilliseconds = 0;

        // The cache host information polling timer
        private readonly Timer _cacheHostInformationPollingTimer = null;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="pollingIntervalMilliseconds">The polling interval, in milliseconds.</param>
        public CacheHostInformationPoller(int pollingIntervalMilliseconds)
        {
            // Sanitize
            if (pollingIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("Interval must be > 0", "pollingIntervalMilliseconds");
            }

            _pollingIntervalMilliseconds = pollingIntervalMilliseconds;

            // Initialize the cache host information polling timer
            _cacheHostInformationPollingTimer = new Timer(PollCacheHosts, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Starts the cached object count poller.
        /// </summary>
        public void Start()
        {
            // Enable periodic signalling of the cache host information polling timer
            _cacheHostInformationPollingTimer.Change(_pollingIntervalMilliseconds, _pollingIntervalMilliseconds);
        }

        /// <summary>
        /// Stops the cached object count poller.
        /// </summary>
        public void Stop()
        {
            // Disable periodic signalling of the cache host information polling timer
            _cacheHostInformationPollingTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// Polls cache hosts for information.
        /// </summary>
        /// <param name="state">The state. Ignored but required for timer callback methods. Pass null.</param>
        private void PollCacheHosts(object state)
        {
            // Lock to ensure atomicity (no overlap)
            lock (_cacheHostInformationPollingTimer)
            {
                // Update performance counters
                CacheHostManager.UpdatePerformanceCounters();
            }
        }
    }
}
