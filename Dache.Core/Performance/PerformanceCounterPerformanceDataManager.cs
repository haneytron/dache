using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Manages performance data using custom performance counters.
    /// </summary>
    public class PerformanceCounterPerformanceDataManager : PerformanceDataManager, IDisposable
    {
        // The performance counter category name
        private const string _performanceCounterCategoryName = "Dache";

        // The number of cached objects
        private readonly PerformanceCounter _numberOfCachedObjectsCounter = null;
        // The total requests per second
        private readonly PerformanceCounter _totalRequestsPerSecondCounter = null;
        // The cache memory usage percent
        private readonly PerformanceCounter _cacheMemoryUsagePercentCounter = null;
        // The cache memory usage in megabytes
        private readonly PerformanceCounter _cacheMemoryUsageMbCounter = null;
        // The cache memory usage limit in megabytes
        private readonly PerformanceCounter _cacheMemoryUsageLimitMbCounter = null;
        // The adds per second
        private readonly PerformanceCounter _addsPerSecondCounter = null;
        // The gets per second
        private readonly PerformanceCounter _getsPerSecondCounter = null;
        // The removes per second
        private readonly PerformanceCounter _removesPerSecondCounter = null;

        /// <summary>
        /// The constructor. Initializes local performance counters.
        /// </summary>
        /// <param name="port">The port.</param>
        public PerformanceCounterPerformanceDataManager(int port) : base()
        {
            // Sanitize
            if (port <= 0)
            {
                throw new ArgumentException("cannot be <= 0", "port");
            }

            string performanceCounterInstanceName = string.Format("port:{0}", port);

            if (string.IsNullOrWhiteSpace(performanceCounterInstanceName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "performanceCounterInstanceName");
            }

            var readOnly = false;

            // Add the counters
            var performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Number of Cached Objects", performanceCounterInstanceName, readOnly);
            _numberOfCachedObjectsCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Total Requests per Second", performanceCounterInstanceName, readOnly);
            _totalRequestsPerSecondCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage MB", performanceCounterInstanceName, readOnly);
            _cacheMemoryUsageMbCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage Limit MB", performanceCounterInstanceName, readOnly);
            _cacheMemoryUsageLimitMbCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage %", performanceCounterInstanceName, readOnly);
            _cacheMemoryUsagePercentCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Adds per Second", performanceCounterInstanceName, readOnly);
            _addsPerSecondCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Gets per Second", performanceCounterInstanceName, readOnly);
            _getsPerSecondCounter = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Removes per Second", performanceCounterInstanceName, readOnly);
            _removesPerSecondCounter = performanceCounter;
        }

        /// <summary>
        /// Increments the adds per second.
        /// </summary>
        public override void IncrementAddsPerSecond()
        {
            base.IncrementAddsPerSecond();
            _addsPerSecondCounter.RawValue++;
            _totalRequestsPerSecondCounter.RawValue++;
        }

        /// <summary>
        /// Increments the gets per second.
        /// </summary>
        public override void IncrementGetsPerSecond()
        {
            base.IncrementGetsPerSecond();
            _getsPerSecondCounter.RawValue++;
            _totalRequestsPerSecondCounter.RawValue++;
        }

        /// <summary>
        /// Increments the removes per second.
        /// </summary>
        public override void IncrementRemovesPerSecond()
        {
            base.IncrementRemovesPerSecond();
            _removesPerSecondCounter.RawValue++;
            _totalRequestsPerSecondCounter.RawValue++;
        }

        /// <summary>
        /// The number of cached objects.
        /// </summary>
        public override long NumberOfCachedObjects
        {
            get
            {
                return base.NumberOfCachedObjects;
            }
            set
            {
                base.NumberOfCachedObjects = value;
                _numberOfCachedObjectsCounter.RawValue = value;
            }
        }

        /// <summary>
        /// The cache memory usage in megabytes.
        /// </summary>
        public override int CacheMemoryUsageMb
        {
            get
            {
                return base.CacheMemoryUsageMb;
            }
            set
            {
                base.CacheMemoryUsageMb = value;
                _cacheMemoryUsageMbCounter.RawValue = value;
            }
        }

        /// <summary>
        /// The cache memory usage limit in megabytes.
        /// </summary>
        public override int CacheMemoryUsageLimitMb
        {
            get
            {
                return base.CacheMemoryUsageLimitMb;
            }
            set
            {
                base.CacheMemoryUsageLimitMb = value;
                _cacheMemoryUsageLimitMbCounter.RawValue = value;
            }
        }

        /// <summary>
        /// The cache memory usage percent.
        /// </summary>
        public override int CacheMemoryUsagePercent
        {
            get
            {
                return base.CacheMemoryUsagePercent;
            }
            set
            {
                base.CacheMemoryUsagePercent = value;
                _cacheMemoryUsagePercentCounter.RawValue = value;
            }
        }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            _numberOfCachedObjectsCounter.Dispose();
            _totalRequestsPerSecondCounter.Dispose();
            _cacheMemoryUsageMbCounter.Dispose();
            _cacheMemoryUsageLimitMbCounter.Dispose();
            _cacheMemoryUsagePercentCounter.Dispose();
            _addsPerSecondCounter.Dispose();
            _getsPerSecondCounter.Dispose();
            _removesPerSecondCounter.Dispose();
        }
    }
}
