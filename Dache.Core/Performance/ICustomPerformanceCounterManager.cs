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
        /// The number of cached objects.
        /// </summary>
        PerformanceCounter NumberOfCachedObjects { get; }

        /// <summary>
        /// The total requests per second.
        /// </summary>
        PerformanceCounter TotalRequestsPerSecond { get; }

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
