using System;
using Dache.Core.Logging;

namespace Dache.CacheHost.Configuration
{
    /// <summary>
    /// Loads custom logger from configuration.
    /// </summary>
    public static class CustomLoggerLoader
    {
        /// <summary>
        /// Gets or sets the default logger to use if no logger is specified via configuration.
        /// </summary>
        public static ILogger DefaultLogger { get; set; }

        /// <summary>
        /// Loads a custom logger. If one was not specified, or the loading fails, loads the default logger.
        /// </summary>
        /// <returns>The logger.</returns>
        public static ILogger LoadLogger()
        {
            if (DefaultLogger == null)
            {
                throw new InvalidOperationException("Please set the default logger before calling this method");
            }

            // Configure custom logging
            try
            {
                var customLoggerTypeString = CacheHostConfigurationSection.Settings.CustomLogger.Type;
                // Check for custom logger
                if (string.IsNullOrWhiteSpace(customLoggerTypeString))
                {
                    // No custom logging
                    return DefaultLogger;
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
                DefaultLogger.Error(ex);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("WARN: Custom logger type load failed");
                Console.WriteLine("WARN: " + ex.GetBaseException().Message);
                Console.WriteLine("WARN: Default logger will be used");
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
            }

            return DefaultLogger;
        }
    }
}
