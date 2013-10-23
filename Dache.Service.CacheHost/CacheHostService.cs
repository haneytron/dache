using Dache.Communication.ClientToCache;
using Dache.Core.CacheHost;
using Dache.Core.CacheHost.Communication.ClientToCache;
using Dache.Core.CacheHost.Storage;
using Dache.Core.DataStructures.Interfaces;
using Dache.Core.DataStructures.Logging;
using Dache.Core.Logging;
using Dache.Service.CacheHost.Configuration;
using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Caching;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceProcess;
using System.Threading;
using Dache.Core.CacheHost.Polling;
using System.Linq;
using Dache.Core.CacheHost.Performance;
using Dache.Core.DataStructures.Performance;

namespace Dache.Service.CacheHost
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

                // Initialize the client to cache service host
                var clientToCacheServiceHost = new ServiceHost(typeof(ClientToCacheServer));
                // Configure the client to cache service host
                var cacheHostAddress = CacheHostConfigurationSection.Settings.Address;
                var cacheHostPort = CacheHostConfigurationSection.Settings.Port;
                // Build the endpoint address
                var endpointAddress = string.Format("net.tcp://{0}:{1}/Dache/CacheHost", cacheHostAddress, cacheHostPort);
                // Build the net tcp binding
                var netTcpBinding = CreateNetTcpBinding();
                // Service throttling
                var serviceThrottling = clientToCacheServiceHost.Description.Behaviors.Find<ServiceThrottlingBehavior>();
                if (serviceThrottling == null)
                {
                    serviceThrottling = new ServiceThrottlingBehavior
                    {
                        MaxConcurrentCalls = int.MaxValue,
                        MaxConcurrentInstances = int.MaxValue,
                        MaxConcurrentSessions = int.MaxValue
                    };

                    clientToCacheServiceHost.Description.Behaviors.Add(serviceThrottling);
                }

                // Configure the service endpoint
                clientToCacheServiceHost.AddServiceEndpoint(typeof(IClientToCacheContract), netTcpBinding, endpointAddress);

                // Configure the custom performance counter manager
                var serviceHostAddress = clientToCacheServiceHost.Description.Endpoints.First().Address.Uri;
                CustomPerformanceCounterManagerContainer.Instance = new CustomPerformanceCounterManager(string.Format("{0}_{1}", serviceHostAddress.Host, serviceHostAddress.Port), false);

                // Initialize the cache host information poller
                var cacheHostInformationPoller = new CacheHostInformationPoller(1000);

                // Instantiate the cache host engine
                _cacheHostEngine = new CacheHostEngine(cacheHostInformationPoller, memCache, clientToCacheServiceHost);
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

        /// <summary>
        /// Creates a configured net tcp binding for communication.
        /// </summary>
        /// <returns>A configured net tcp binding.</returns>
        private NetTcpBinding CreateNetTcpBinding()
        {
            var netTcpBinding = new NetTcpBinding(SecurityMode.None, false)
            {
                CloseTimeout = TimeSpan.FromSeconds(15),
                OpenTimeout = TimeSpan.FromSeconds(15),
                SendTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.MaxValue,
                Namespace = "http://schemas.getdache.net/cachehost",
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxConnections = 100000,
                ListenBacklog = 100000,
                TransferMode = System.ServiceModel.TransferMode.Buffered,
                ReliableSession = new OptionalReliableSession
                {
                    Enabled = false
                }
            };

            // Set reader quotas
            netTcpBinding.ReaderQuotas.MaxDepth = 64;
            netTcpBinding.ReaderQuotas.MaxStringContentLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxArrayLength = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxBytesPerRead = int.MaxValue;
            netTcpBinding.ReaderQuotas.MaxNameTableCharCount = int.MaxValue;

            return netTcpBinding;
        }

        ///// <summary>
        ///// Creates the unity container from the unity configuration section of the passed in name.
        ///// </summary>
        ///// <param name="configSectionName">The configuration section name.</param>
        ///// <returns>An instantiated unity container, or null if the configuration was not able to be properly loaded.</returns>
        //private IUnityContainer CreateUnityContainer(string configSectionName)
        //{
        //    // Sanitize
        //    if (string.IsNullOrWhiteSpace(configSectionName))
        //    {
        //        return null;
        //    }

        //    var unityConfigurationSection = ConfigurationManager.GetSection(configSectionName) as UnityConfigurationSection;

        //    if (unityConfigurationSection == null)
        //    {
        //        _eventLog.WriteEntry("The Unity Configuration Section named " + configSectionName + " was not able to be loaded", EventLogEntryType.Error);
        //        return null;
        //    }

        //    var unityContainer = new UnityContainer();

        //    try
        //    {
        //        unityContainer.LoadConfiguration(unityConfigurationSection);
        //    }
        //    catch
        //    {
        //        _eventLog.WriteEntry("The Unity Configuration Section named " + configSectionName + " was not able to be loaded", EventLogEntryType.Error);
        //        return null;
        //    }

        //    return unityContainer;
        //}
    }
}
