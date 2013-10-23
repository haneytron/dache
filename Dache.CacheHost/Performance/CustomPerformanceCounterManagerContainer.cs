using Dache.Core.Performance;

namespace Dache.CacheHost.Performance
{
    /// <summary>
    /// Contains an instance of a custom performance counter manager.
    /// </summary>
    public static class CustomPerformanceCounterManagerContainer
    {
        /// <summary>
        /// The instance.
        /// </summary>
        public static CustomPerformanceCounterManager Instance { get; set; }
    }
}
