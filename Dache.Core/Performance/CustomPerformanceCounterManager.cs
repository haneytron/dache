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

        // The performance counters
        private readonly IDictionary<string, PerformanceCounter> _performanceCounters = new Dictionary<string, PerformanceCounter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPerformanceCounterManager"/> class.
        /// </summary>
        /// <param name="performanceCounterInstanceName">The performance counter instance name.</param>
        /// <param name="readOnly">Whether or not the performance counters are read-only.</param>
        public CustomPerformanceCounterManager(string performanceCounterInstanceName, bool readOnly)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(performanceCounterInstanceName))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "performanceCounterInstanceName");
            }

            // Add the counters
            var performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Number of Cached Objects", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            NumberOfCachedObjects = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Total Requests per Second", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            TotalRequestsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage %", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            CacheMemoryUsagePercent = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage Base %", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            CacheMemoryUsageBasePercent = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Cache Memory Usage MB", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            CacheMemoryUsageMb = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Adds per Second", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            AddsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Gets per Second", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            GetsPerSecond = performanceCounter;

            performanceCounter = new PerformanceCounter(_performanceCounterCategoryName, "Removes per Second", performanceCounterInstanceName, readOnly);
            _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);
            RemovesPerSecond = performanceCounter;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CustomPerformanceCounterManager"/> class.
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
                _performanceCounters.Add(performanceCounter.CounterName, performanceCounter);

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
            }
        }

        /// <summary>
        /// Gets the number of cached objects.
        /// </summary>
        public PerformanceCounter NumberOfCachedObjects { get; private set; }

        /// <summary>
        /// Gets the total requests per second.
        /// </summary>
        public PerformanceCounter TotalRequestsPerSecond { get; private set; }

        /// <summary>
        /// Gets the cache memory usage percent.
        /// </summary>
        public PerformanceCounter CacheMemoryUsagePercent { get; private set; }

        /// <summary>
        /// Gets the cache memory usage base percent.
        /// </summary>
        public PerformanceCounter CacheMemoryUsageBasePercent { get; private set; }

        /// <summary>
        /// Gets the cache memory usage in megabytes.
        /// </summary>
        public PerformanceCounter CacheMemoryUsageMb { get; private set; }

        /// <summary>
        /// Gets the number of adds per second.
        /// </summary>
        public PerformanceCounter AddsPerSecond { get; private set; }

        /// <summary>
        /// Gets the number of gets per second.
        /// </summary>
        public PerformanceCounter GetsPerSecond { get; private set; }

        /// <summary>
        /// Gets the number of removes per second.
        /// </summary>
        public PerformanceCounter RemovesPerSecond { get; private set; }

        /// <summary>
        /// Attempts to get a performance counter by its name.
        /// </summary>
        /// <param name="name">The performance counter name.</param>
        /// <param name="performanceCounter">The performance counter.</param>
        /// <returns>true if successful, false otherwise.</returns>
        public bool TryGetPerformanceCounter(string name, out PerformanceCounter performanceCounter)
        {
            return _performanceCounters.TryGetValue(name, out performanceCounter);
        }

        /// <summary>
        /// Gets all custom performance counters.
        /// </summary>
        /// <returns>The custom performance counters.</returns>
        public IEnumerable<PerformanceCounter> GetAll()
        {
            return _performanceCounters.Values;
        }

        /// <summary>
        /// Updates all performance counters.
        /// </summary>
        public void UpdateAll()
        {
            foreach (var performanceCounter in _performanceCounters.Values)
            {
                // Do the update
                performanceCounter.NextValue();
            }
        }

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
