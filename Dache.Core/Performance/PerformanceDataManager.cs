using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Manages performance data.
    /// </summary>
    public class PerformanceDataManager : IDisposable
    {
        // The per second timer
        private readonly Timer _perSecondTimer = null;

        /// <summary>
        /// The adds per second.
        /// </summary>
        protected int _addsPerSecond = 0;
        /// <summary>
        /// The gets per second.
        /// </summary>
        protected int _getsPerSecond = 0;
        /// <summary>
        /// The removes per second.
        /// </summary>
        protected int _removesPerSecond = 0;

        /// <summary>
        /// The constructor.
        /// </summary>
        public PerformanceDataManager()
        {
            // Configure per second timer to fire every 1000 ms starting 1000ms from now
            _perSecondTimer = new Timer(PerSecondOperations, null, 1000, 1000);
        }

        /// <summary>
        /// The number of cached objects.
        /// </summary>
        public virtual long NumberOfCachedObjects { get; set; }

        /// <summary>
        /// The cache memory usage in megabytes.
        /// </summary>
        public virtual int CacheMemoryUsageMb { get; set; }

        /// <summary>
        /// The cache memory usage limit in megabytes.
        /// </summary>
        public virtual int CacheMemoryUsageLimitMb { get; set; }

        /// <summary>
        /// The cache memory usage percent.
        /// </summary>
        public virtual int CacheMemoryUsagePercent { get; set; }

        /// <summary>
        /// The adds per second.
        /// </summary>
        public int AddsPerSecond
        {
            get
            {
                return _addsPerSecond;
            }
        }

        /// <summary>
        /// Increments the adds per second.
        /// </summary>
        public virtual void IncrementAddsPerSecond()
        {
            Interlocked.Increment(ref _addsPerSecond);
        }

        /// <summary>
        /// The gets per second.
        /// </summary>
        public int GetsPerSecond
        {
            get
            {
                return _getsPerSecond;
            }
        }

        /// <summary>
        /// Increments the gets per second.
        /// </summary>
        public virtual void IncrementGetsPerSecond()
        {
            Interlocked.Increment(ref _getsPerSecond);
        }

        /// <summary>
        /// The removes per second.
        /// </summary>
        public int RemovesPerSecond
        {
            get
            {
                return _removesPerSecond;
            }
        }

        /// <summary>
        /// Increments the removes per second.
        /// </summary>
        public virtual void IncrementRemovesPerSecond()
        {
            Interlocked.Increment(ref _removesPerSecond);
        }

        /// <summary>
        /// The total requests per second.
        /// </summary>
        public int TotalRequestsPerSecond
        {
            get
            {
                return _addsPerSecond + _getsPerSecond + _removesPerSecond;
            }
        }

        /// <summary>
        /// Performs per second operations.
        /// </summary>
        /// <param name="state">The state. Ignored but required for timer callback methods. Pass null.</param>
        private void PerSecondOperations(object state)
        {
            // Lock to ensure atomicity (no overlap)
            lock (_perSecondTimer)
            {
                // Clear per second counters
                Interlocked.Exchange(ref _addsPerSecond, 0);
                Interlocked.Exchange(ref _getsPerSecond, 0);
                Interlocked.Exchange(ref _removesPerSecond, 0);
            }
        }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            _perSecondTimer.Dispose();
        }
    }
}
