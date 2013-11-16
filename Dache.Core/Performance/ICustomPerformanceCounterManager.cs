using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Represents a manager of custom performance counters.
    /// </summary>
    public interface ICustomPerformanceCounterManager : IDisposable
    {
        /// <summary>
        /// Attempts to get a performance counter by its name.
        /// </summary>
        /// <param name="name">The name.</param>
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

        /// <summary>
        /// The number of cached objects.
        /// </summary>
        PerformanceCounter NumberOfCachedObjects { get; }

        /// <summary>
        /// The total requests per second.
        /// </summary>
        PerformanceCounter TotalRequestsPerSecond { get; }

        /// <summary>
        /// The cache expirations per second.
        /// </summary>
        PerformanceCounter CacheExpirationsAndEvictionsPerSecond { get; }

        /// <summary>
        /// The cache memory usage percent.
        /// </summary>
        PerformanceCounter CacheMemoryUsagePercent { get; }

        /// <summary>
        /// The cache memory usage base percent.
        /// </summary>
        PerformanceCounter CacheMemoryUsageBasePercent { get; }

        /// <summary>
        /// The cache memory usage in megabytes.
        /// </summary>
        PerformanceCounter CacheMemoryUsageMb { get; }

        /// <summary>
        /// The adds per second.
        /// </summary>
        PerformanceCounter AddsPerSecond { get; }

        /// <summary>
        /// The gets per second.
        /// </summary>
        PerformanceCounter GetsPerSecond { get; }

        /// <summary>
        /// The removes per second.
        /// </summary>
        PerformanceCounter RemovesPerSecond { get; }
    }
}
