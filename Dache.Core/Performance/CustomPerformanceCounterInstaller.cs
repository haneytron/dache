using System.Diagnostics;

namespace Dache.Core.Performance
{
    /// <summary>
    /// Installs or uninstalls custom performance counters.
    /// </summary>
    public static class CustomPerformanceCounterInstaller
    {
        // The performance counter category name
        private const string _performanceCounterCategoryName = "Dache";

        /// <summary>
        /// Installs the performance counters.
        /// </summary>
        public static void InstallCounters()
        {
            // Create custom performance counters and counter category
            var counterCreationDataCollection = new CounterCreationDataCollection();
            counterCreationDataCollection.Add(new CounterCreationData("Number of Cached Objects", "The number of objects being stored in the cache", PerformanceCounterType.NumberOfItems64));
            counterCreationDataCollection.Add(new CounterCreationData("Cache Expirations and Evictions per Second", "The number of objects that are expired and evicted from the cache per second", PerformanceCounterType.NumberOfItems64));
            counterCreationDataCollection.Add(new CounterCreationData("Cache Memory Usage %", "The percentage of memory used by the cache", PerformanceCounterType.RawFraction));
            counterCreationDataCollection.Add(new CounterCreationData("Cache Memory Usage Base %", "The base percentage of memory used by the cache", PerformanceCounterType.RawBase));
            counterCreationDataCollection.Add(new CounterCreationData("Cache Memory Usage MB", "The amount of memory used by the cache", PerformanceCounterType.NumberOfItems32));
            counterCreationDataCollection.Add(new CounterCreationData("Total Requests per Second", "The total number of all types of requests per second", PerformanceCounterType.RateOfCountsPerSecond64));
            counterCreationDataCollection.Add(new CounterCreationData("Adds per Second", "The number of adds per second", PerformanceCounterType.RateOfCountsPerSecond64));
            counterCreationDataCollection.Add(new CounterCreationData("Gets per Second", "The number of gets per second", PerformanceCounterType.RateOfCountsPerSecond64));
            counterCreationDataCollection.Add(new CounterCreationData("Removes per Second", "The number of removes per second", PerformanceCounterType.RateOfCountsPerSecond64));

            // Delete performance counter category
            if (PerformanceCounterCategory.Exists(_performanceCounterCategoryName))
            {
                PerformanceCounterCategory.Delete(_performanceCounterCategoryName);
            }

            // Create performance counter category
            PerformanceCounterCategory.Create(_performanceCounterCategoryName, "Performance counters related to Dache services", PerformanceCounterCategoryType.MultiInstance, counterCreationDataCollection);
        }

        /// <summary>
        /// Uninstalls the performance counters.
        /// </summary>
        public static void UninstallCounters()
        {
            if (PerformanceCounterCategory.Exists(_performanceCounterCategoryName))
            {
                PerformanceCounterCategory.Delete(_performanceCounterCategoryName);
            }
        }
    }
}
