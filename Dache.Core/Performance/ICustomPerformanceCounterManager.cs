using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Represents a manager of custom performance counters.
    /// </summary>
    public interface ICustomPerformanceCounterManager : IDisposable
    {
        /// <summary>
        /// Gets the number of cached objects.
        /// </summary>
        PerformanceCounter NumberOfCachedObjects { get; }

        /// <summary>
        /// Gets the total requests per second.
        /// </summary>
        PerformanceCounter TotalRequestsPerSecond { get; }

        /// <summary>
        /// Gets the cache memory usage percent.
        /// </summary>
        PerformanceCounter CacheMemoryUsagePercent { get; }

        /// <summary>
        /// Gets the cache memory usage base percent.
        /// </summary>
        PerformanceCounter CacheMemoryUsageBasePercent { get; }

        /// <summary>
        /// Gets the cache memory usage in megabytes.
        /// </summary>
        PerformanceCounter CacheMemoryUsageMb { get; }

        /// <summary>
        /// Gets the adds per second.
        /// </summary>
        PerformanceCounter AddsPerSecond { get; }

        /// <summary>
        /// Gets the gets per second.
        /// </summary>
        PerformanceCounter GetsPerSecond { get; }

        /// <summary>
        /// Gets the removes per second.
        /// </summary>
        PerformanceCounter RemovesPerSecond { get; }

        /// <summary>
        /// Attempts to get a performance counter by its name.
        /// </summary>
        /// <param name="name">The performance counter name.</param>
        /// <param name="performanceCounter">The performance counter.</param>
        /// <returns>true if successful, false otherwise.</returns>
        bool TryGetPerformanceCounter(string name, out PerformanceCounter performanceCounter);

        /// <summary>
        /// Gets all custom performance counters.
        /// </summary>
        /// <returns>The custom performance counters.</returns>
        IEnumerable<PerformanceCounter> GetAll();

        /// <summary>
        /// Updates all performance counters.
        /// </summary>
        void UpdateAll();
    }
}
