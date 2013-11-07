using System;
using System.Collections.Specialized;
using System.ServiceProcess;
using System.Threading;
using Dache.CacheHost.Communication;
using Dache.CacheHost.Configuration;
using Dache.CacheHost.Performance;
using Dache.CacheHost.Polling;
using Dache.CacheHost.Storage;
using Dache.Core.Interfaces;
using Dache.Core.Logging;
using Dache.Core.Performance;

namespace Dache.CacheHost
{
    /// <summary>
    /// The cache host windows service. Responsible for initializing, starting, and stopping the cache host functionality.
    /// </summary>
    internal class CacheHostService : ServiceBase
    {
        // The cache host engine that does the actual work
        private IRunnable _cacheHostEngine = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        static void Main(string[] args)
        {
            var servicesToRun = new ServiceBase[] 
            { 
                new CacheHostService()
            };

            ServiceBase.Run(servicesToRun);
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public CacheHostService()
        {
            // Set the host name
            ServiceName = "Dache Cache Host";

            // Configure custom logging
            try
            {
                var customLoggerTypeString = CacheHostConfigurationSection.Settings.CustomLogger.Type;
                // Check for custom logger
                if (string.IsNullOrWhiteSpace(customLoggerTypeString))
                {
                    // No custom logging
                    LoggerContainer.Instance = new EventViewerLogger("Cache Host", "Dache");
                    return;
                }

                // Have a custom logger, attempt to load it and confirm it
                var customLoggerType = Type.GetType(customLoggerTypeString);
                // Verify that it implements our ILogger interface
                if (customLoggerType != null && customLoggerType.IsAssignableFrom(typeof(ILogger)))
                {
                    LoggerContainer.Instance = (ILogger)Activator.CreateInstance(customLoggerType);
                }
            }
            catch
            {
                // Custom logger load failed - no custom logging
                LoggerContainer.Instance = new EventViewerLogger("Cache Host", "Dache");
                return;
            }
        }

        /// <summary>
        /// Fires when the windows service starts.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        protected override void OnStart(string[] args)
        {
            LoggerContainer.Instance.Info("Cache Host is starting", "Cache Host is starting");

            // Configure the thread pool's minimum threads
            ThreadPool.SetMinThreads(128, 128);

            LoggerContainer.Instance.Info("Cache Host is starting", "Verifying settings");

            try
            {
                // Initialize the mem cache container instance
                var physicalMemoryLimitPercentage = CacheHostConfigurationSection.Settings.CacheMemoryLimitPercentage;
                var cacheConfig = new NameValueCollection();
                cacheConfig.Add("pollingInterval", "00:00:15");
                cacheConfig.Add("physicalMemoryLimitPercentage", physicalMemoryLimitPercentage.ToString());
                var memCache = new MemCache("Dache", cacheConfig);

                // Initialize the client to cache server
                var port = CacheHostConfigurationSection.Settings.Port;
                var clientToCacheServer = new CacheHostServer(port);

                // Configure the custom performance counter manager
                CustomPerformanceCounterManagerContainer.Instance = new CustomPerformanceCounterManager(string.Format("port:{0}", port), false);

                // Initialize the cache host information poller
                var cacheHostInformationPoller = new CacheHostInformationPoller(1000);

                // Instantiate the cache host engine
                _cacheHostEngine = new CacheHostEngine(cacheHostInformationPoller, memCache, clientToCacheServer);
            }
            catch (Exception ex)
            {
                // The inner exception has the actual details of the configuration error
                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message.StartsWith("The value for the property", StringComparison.OrdinalIgnoreCase))
                {
                    ex = ex.InnerException;
                }

                // Log the error
                LoggerContainer.Instance.Error("Cache Host failed to start", ex.Message);

                // Stop the service
                Stop();
            }

            LoggerContainer.Instance.Info("Cache Host is starting", "Settings verified successfully");

            _cacheHostEngine.Start();
        }

        /// <summary>
        /// Fires when the windows service stops.
        /// </summary>
        protected override void OnStop()
        {
            LoggerContainer.Instance.Info("Cache Host is stopping", "Cache Host is stopping");

            _cacheHostEngine.Stop();
        }
    }
}
