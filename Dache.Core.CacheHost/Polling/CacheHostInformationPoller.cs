using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Dache.Core.CacheHost.Performance;
using Dache.Core.CacheHost.Storage;
using Dache.Core.DataStructures.Interfaces;
using Microsoft.VisualBasic.Devices;

namespace Dache.Core.CacheHost.Polling
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

        // The computer info
        private readonly ComputerInfo _computerInfo = new ComputerInfo();
        // The performance counter for the process' current memory
        private readonly PerformanceCounter _currentMemoryPerformanceCounter = new PerformanceCounter("Process", "Private Bytes", Process.GetCurrentProcess().ProcessName, true);
        // The performance counter for the cache trim count
        private readonly PerformanceCounter _currentCacheTrimPerformanceCounter = new PerformanceCounter(".NET Memory Cache 4.0", "Cache Trims", Process.GetCurrentProcess().ProcessName + ":dache", true);
        // The last cached trimmed value
        private long _lastCacheTrimmedValue = 0;

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
                CustomPerformanceCounterManagerContainer.Instance.NumberOfCachedObjects.RawValue = MemCacheContainer.Instance.GetCount();
                var usedMemoryMb = _currentMemoryPerformanceCounter.RawValue / 1048576; // bytes / (1024 * 1024) for MB

                CustomPerformanceCounterManagerContainer.Instance.CacheMemoryUsageMb.RawValue = usedMemoryMb;
                CustomPerformanceCounterManagerContainer.Instance.CacheMemoryUsagePercent.RawValue = usedMemoryMb;
                CustomPerformanceCounterManagerContainer.Instance.CacheMemoryUsageBasePercent.RawValue = (long)(_computerInfo.TotalPhysicalMemory / 1048576);

                // Calculate expirations and evictions
                CustomPerformanceCounterManagerContainer.Instance.CacheExpirationsAndEvictionsPerSecond.RawValue = _currentCacheTrimPerformanceCounter.RawValue - _lastCacheTrimmedValue;
                _lastCacheTrimmedValue = _currentCacheTrimPerformanceCounter.RawValue;
            }
        }
    }
}
