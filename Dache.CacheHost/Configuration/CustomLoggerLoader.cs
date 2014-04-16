using System;
using Dache.Core.Logging;

namespace Dache.CacheHost.Configuration
{
    /// <summary>
    /// Loads custom logger from configuration.
    /// </summary>
    internal static class CustomLoggerLoader
    {
        /// <summary>
        /// Loads a custom logger. If one was not specified, or the loading fails, loads the default logger.
        /// </summary>
        /// <returns>The logger.</returns>
        public static ILogger LoadLogger()
        {
            var defaultLogger = new EventViewerLogger("Cache Host", "Dache");

            // Configure custom logging
            try
            {
                var customLoggerTypeString = CacheHostConfigurationSection.Settings.CustomLogger.Type;

                // Check for custom logger
                if (string.IsNullOrWhiteSpace(customLoggerTypeString))
                {
                    // No custom logging
                    return defaultLogger;
                }

                // Have a custom logger, attempt to load it and confirm it
                var customLoggerType = Type.GetType(customLoggerTypeString);
                
                // Verify that it implements our ILogger interface
                if (customLoggerType != null && typeof(ILogger).IsAssignableFrom(customLoggerType))
                {
                    return (ILogger)Activator.CreateInstance(customLoggerType);
                }
            }
            catch (Exception ex)
            {
                // Custom logger load failed - no custom logging
                defaultLogger.Error(ex);
            }

            return defaultLogger;
        }
    }
}
