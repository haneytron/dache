using System;
using System.ServiceProcess;
using System.Threading;
using Dache.CacheHost.Routing;
using Dache.CacheHost.Storage;
using Dache.CacheHost.Configuration;
using Dache.Core.Communication;
using Dache.Core.Logging;
using Dache.Core.Performance;

namespace Dache.CacheHost
{
    /// <summary>
    /// The cache host windows service. Responsible for initializing, starting, and stopping the cache host functionality.
    /// </summary>
    internal class Service : ServiceBase
    {
        // The logger
        private readonly ILogger _logger = null;

        // The cache host engine that does the actual work
        private CacheHostEngine _cacheHostEngine = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        static void Main(string[] args)
        {
            ServiceBase.Run(new[] { new Service() });
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public Service()
        {
            // Set the host name
            ServiceName = "Dache Cache Host";

            // Load custom logging
            CustomLoggerLoader.DefaultLogger = new EventViewerLogger("Cache Host", "Dache");
            _logger = CustomLoggerLoader.LoadLogger();
        }

        /// <summary>
        /// Fires when the windows service starts.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        protected override void OnStart(string[] args)
        {
            _logger.Info("Cache Host is starting", "Cache Host is starting");

            // Configure the thread pool's minimum threads
            ThreadPool.SetMinThreads(128, 128);

            _logger.Info("Cache Host is starting", "Verifying settings");

            try
            {
                // Instantiate the cache host engine
                _cacheHostEngine = new CacheHostEngine(CacheHostConfigurationSection.Settings);
            }
            catch (Exception ex)
            {
                // The inner exception has the actual details of the configuration error
                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message.StartsWith("The value for the property", StringComparison.OrdinalIgnoreCase))
                {
                    ex = ex.InnerException;
                }

                // Log the error
                _logger.Error("Cache Host failed to start", ex.Message);

                // Stop the service
                Stop();
            }

            _logger.Info("Cache Host is starting", "Settings verified successfully");

            _cacheHostEngine.Start();
        }

        /// <summary>
        /// Fires when the windows service stops.
        /// </summary>
        protected override void OnStop()
        {
            _logger.Info("Cache Host is stopping", "Cache Host is stopping");

            try
            {
                _cacheHostEngine.Stop();
            }
            catch
            {
                // Ignore it - stopping anyway
            }
        }
    }
}
