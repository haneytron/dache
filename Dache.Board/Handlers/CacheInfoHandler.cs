using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Dache.Board.Configuration;

namespace Dache.Board.Handlers
{
    /// <summary>
    /// Retrieves cache information.
    /// </summary>
    internal static class CacheInfoHandler
    {
        // The timer which updates cache information
        private static readonly Timer _updateCacheInformationTimer = null;

        // The current cache information
        private static IList<KeyValuePair<string, PerformanceCounter[]>> _currentCacheInformation = null;

        /// <summary>
        /// Initializes static members of the <see cref="CacheInfoHandler"/> class.
        /// </summary>
        static CacheInfoHandler()
        {
            // Immediately update the cache information synchronously
            UpdateCacheInformationThread(null);

            // Get the timer interval from configuration
            var timerInterval = DacheboardConfigurationSection.Settings.InformationPollingIntervalMilliseconds;
            
            // Configure the timer to fire according to the configuration
            _updateCacheInformationTimer = new Timer(UpdateCacheInformationThread, null, timerInterval, timerInterval);
        }

        /// <summary>
        /// Retrieves the cache information.
        /// </summary>
        /// <returns>A list of key-value pair where the key is the cache host name and the value is its performance counters.</returns>
        public static IList<KeyValuePair<string, PerformanceCounter[]>> GetCacheInfo()
        {
            return _currentCacheInformation;
        }

        /// <summary>
        /// The thread which updates the cache information.
        /// </summary>
        /// <param name="state">The state. Required for timer callbacks but ignored. Pass null.</param>
        private static void UpdateCacheInformationThread(object state)
        {
            // Lock to prevent concurrent hits if the thread rolls over itself
            // lock (_currentCacheInformation)
            // {
                // TODO: get information from the cache hosts
                // _currentCacheInformation = BoardToManagerClientContainer.Instance.GetPerformanceInformation();
            // }
        }
    }
}