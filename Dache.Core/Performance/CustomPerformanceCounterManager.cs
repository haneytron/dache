using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Manages custom performance counters.
    /// </summary>
    public class CustomPerformanceCounterManager : ICustomPerformanceCounterManager
    {
        // The performance counter category name
        private const string _performanceCounterCategoryName = "Dache";

        /// <summary>
        /// The constructor. Initializes local performance counters.
        /// </summary>
        /// <param name="port">The port.</param>
        /// <param name="readOnly">Whether or not the performance counters are read-only.</param>
        public CustomPerformanceCounterManager(int port, bool readOnly)
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

            // Add the counters
            var performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Number of Cached Objects", performanceCounterInstanceName, readOnly);
            NumberOfCachedObjects = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Total Requests per Second", performanceCounterInstanceName, readOnly);
            TotalRequestsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage %", performanceCounterInstanceName, readOnly);
            CacheMemoryUsagePercent = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage Base %", performanceCounterInstanceName, readOnly);
            CacheMemoryUsageBasePercent = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage MB", performanceCounterInstanceName, readOnly);
            CacheMemoryUsageMb = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Adds per Second", performanceCounterInstanceName, readOnly);
            AddsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Gets per Second", performanceCounterInstanceName, readOnly);
            GetsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Removes per Second", performanceCounterInstanceName, readOnly);
            RemovesPerSecond = performanceCounter;
        }

        /// <summary>
        /// The constructor. Initializes remote performance counters.
        /// </summary>
        /// <param name="machineName">The machine name.</param>
        /// <param name="performanceCounterInstanceName">The performance counter instance name.</param>
        public CustomPerformanceCounterManager(string machineName, string performanceCounterInstanceName)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(machineName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "machineName");
            }
            if (string.IsNullOrWhiteSpace(performanceCounterInstanceName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "performanceCounterInstanceName");
            }

            // Special case - "localhost" must be "." instead
            if (string.Equals(machineName, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                machineName = ".";
            }

            var performanceCounterCategory = new PerformanceCounterCategory(_performanceCounterCategoryName, machineName);
            var performanceCounters = performanceCounterCategory.GetCounters(performanceCounterInstanceName);

            // Add the counters
            foreach (var performanceCounter in performanceCounters)
            {
                if (string.Equals(performanceCounter.CounterName, "Number of Cached Objects", StringComparison.OrdinalIgnoreCase))
                {
                    NumberOfCachedObjects = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Total Requests per Second", StringComparison.OrdinalIgnoreCase))
                {
                    TotalRequestsPerSecond = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Cache Memory Usage %", StringComparison.OrdinalIgnoreCase))
                {
                    CacheMemoryUsagePercent = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Cache Memory Usage Base %", StringComparison.OrdinalIgnoreCase))
                {
                    CacheMemoryUsageBasePercent = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Cache Memory Usage MB", StringComparison.OrdinalIgnoreCase))
                {
                    CacheMemoryUsageMb = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Adds per Second", StringComparison.OrdinalIgnoreCase))
                {
                    AddsPerSecond = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Gets per Second", StringComparison.OrdinalIgnoreCase))
                {
                    GetsPerSecond = performanceCounter;
                }
                else if (string.Equals(performanceCounter.CounterName, "Removes per Second", StringComparison.OrdinalIgnoreCase))
                {
                    RemovesPerSecond = performanceCounter;
                }
                else
                {
                    // Dispose the ones we aren't using
                    performanceCounter.Dispose();
                }
            }
        }

        /// <summary>
        /// The number of cached objects.
        /// </summary>
        public PerformanceCounter NumberOfCachedObjects { get; private set; }

        /// <summary>
        /// The total requests per second.
        /// </summary>
        public PerformanceCounter TotalRequestsPerSecond { get; private set; }

        /// <summary>
        /// The cache memory usage percent.
        /// </summary>
        public PerformanceCounter CacheMemoryUsagePercent { get; private set; }
        
        /// <summary>
        /// The cache memory usage base percent.
        /// </summary>
        public PerformanceCounter CacheMemoryUsageBasePercent { get; private set; }

        /// <summary>
        /// The cache memory usage in megabytes.
        /// </summary>
        public PerformanceCounter CacheMemoryUsageMb { get; private set; }

        /// <summary>
        /// The adds per second.
        /// </summary>
        public PerformanceCounter AddsPerSecond { get; private set; }
        
        /// <summary>
        /// The gets per second.
        /// </summary>
        public PerformanceCounter GetsPerSecond { get; private set; }

        /// <summary>
        /// The removes per second.
        /// </summary>
        public PerformanceCounter RemovesPerSecond { get; private set; }

        /// <summary>
        /// Called when disposed.
        /// </summary>
        public void Dispose()
        {
            NumberOfCachedObjects.Dispose();
            TotalRequestsPerSecond.Dispose();
            CacheMemoryUsagePercent.Dispose();
            CacheMemoryUsageBasePercent.Dispose();
            CacheMemoryUsageMb.Dispose();
            AddsPerSecond.Dispose();
            GetsPerSecond.Dispose();
            RemovesPerSecond.Dispose();
        }
    }
}
