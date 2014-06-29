using System;
using System.Diagnostics;
using System.Threading;
using Dache.CacheHost.Storage;
using Dache.Core.Performance;

namespace Dache.CacheHost.Polling
{
    /// <summary>
    /// Polls cache hosts for information.
    /// </summary>
    internal class CacheHostInformationPoller : IRunnable
    {
        // The mem cache
        private readonly IMemCache _memCache = null;
        // The custom performance counter manager
        private readonly ICustomPerformanceCounterManager _customPerformanceCounterManager = null;
        // The polling interval in milliseconds
        private readonly int _pollingIntervalMilliseconds = 0;
        // The cache host information polling timer
        private readonly Timer _cacheHostInformationPollingTimer = null;
        // The performance counter for the process' current memory
        private readonly PerformanceCounter _currentMemoryPerformanceCounter = new PerformanceCounter("Process", "Private Bytes", Process.GetCurrentProcess().ProcessName, true);

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="memCache">The mem cache.</param>
        /// <param name="customPerformanceCounterManager">The custom performance counter manager.</param>
        /// <param name="pollingIntervalMilliseconds">The polling interval, in milliseconds.</param>
        public CacheHostInformationPoller(IMemCache memCache, ICustomPerformanceCounterManager customPerformanceCounterManager, int pollingIntervalMilliseconds)
        {
            // Sanitize
            if (memCache == null)
            {
                throw new ArgumentNullException("memCache");
            }
            if (customPerformanceCounterManager == null)
            {
                throw new ArgumentNullException("customPerformanceCounterManager");
            }
            if (pollingIntervalMilliseconds <= 0)
            {
                throw new ArgumentException("Interval must be > 0", "pollingIntervalMilliseconds");
            }

            _memCache = memCache;
            _customPerformanceCounterManager = customPerformanceCounterManager;
            _pollingIntervalMilliseconds = pollingIntervalMilliseconds;

            // Initialize the cache host information polling timer
            _cacheHostInformationPollingTimer = new Timer(PollCacheHost, null, Timeout.Infinite, Timeout.Infinite);
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
        /// Polls the cache host for information.
        /// </summary>
        /// <param name="state">The state. Ignored but required for timer callback methods. Pass null.</param>
        private void PollCacheHost(object state)
        {
            // Lock to ensure atomicity (no overlap)
            lock (_cacheHostInformationPollingTimer)
            {
                // Update performance counters
                _customPerformanceCounterManager.NumberOfCachedObjects.RawValue = _memCache.Count;
                var usedMemoryMb = _currentMemoryPerformanceCounter.RawValue / 1048576; // bytes / (1024 * 1024) for MB

                _customPerformanceCounterManager.CacheMemoryUsageMb.RawValue = usedMemoryMb;
                _customPerformanceCounterManager.CacheMemoryUsagePercent.RawValue = usedMemoryMb;
                _customPerformanceCounterManager.CacheMemoryUsageBasePercent.RawValue = _memCache.MemoryLimit;
            }
        }
    }
}
