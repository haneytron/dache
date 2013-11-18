using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dache.Client.Serialization;
using Dache.Core.Logging;

namespace Dache.Client.Configuration
{
    /// <summary>
    /// Loads custom types from configuration.
    /// </summary>
    internal static class CustomTypesLoader
    {
        /// <summary>
        /// Loads a custom logger. If one was not specified, or the loading fails, loads the default logger.
        /// </summary>
        /// <returns>The logger.</returns>
        public static ILogger LoadLogger()
        {
            var defaultLogger = new EventViewerLogger("Cache Client", "Dache");

            // Configure custom logging
            try
            {
                var customLoggerTypeString = CacheClientConfigurationSection.Settings.CustomLogger.Type;
                // Check for custom logger
                if (string.IsNullOrWhiteSpace(customLoggerTypeString))
                {
                    // No custom logging
                    return defaultLogger;
                }
                
                // Have a custom logger, attempt to load it and confirm it
                var customLoggerType = Type.GetType(customLoggerTypeString);
                // Verify that it implements our ILogger interface
                if (customLoggerType != null && customLoggerType.IsAssignableFrom(typeof(ILogger)))
                {
                    return (ILogger)Activator.CreateInstance(customLoggerType);
                }
            }
            catch
            {
                // Custom logger load failed - no custom logging
            }

            return defaultLogger;
        }

        /// <summary>
        /// Loads a custom serializer. If one was not specified, or the loading fails, loads the default serializer.
        /// </summary>
        /// <returns>The serializer.</returns>
        public static IBinarySerializer LoadSerializer()
        {
            var defaultSerializer = new BinarySerializer();

            // Configure custom serializer
            try
            {
                var customSerializerTypeString = CacheClientConfigurationSection.Settings.CustomSerializer.Type;
                // Check for custom serializer
                if (string.IsNullOrWhiteSpace(customSerializerTypeString))
                {
                    // No custom serializer
                    return defaultSerializer;
                }
                 
                // Have a custom serializer, attempt to load it and confirm it
                var customSerializerType = Type.GetType(customSerializerTypeString);
                // Verify that it implements our IBinarySerializer interface
                if (customSerializerType != null && customSerializerType.IsAssignableFrom(typeof(IBinarySerializer)))
                {
                    return (IBinarySerializer)Activator.CreateInstance(customSerializerType);
                }
            }
            catch
            {
                // Custom serializer load failed - no custom serialization
            }

            return defaultSerializer;
        }
    }
}
