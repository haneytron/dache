using Dache.Communication.CacheToManager;
using Dache.Core.CacheManager;
using Dache.Core.CacheManager.Communication;
using Dache.Core.CacheManager.Polling;
using Dache.Core.DataStructures.Interfaces;
using Dache.Core.DataStructures.Logging;
using Dache.Core.Logging;
using Dache.Service.CacheManager.Configuration;
using System;
using System.Diagnostics;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceProcess;
using System.Threading;
using Dache.Communication.BoardToManager;

namespace Dache.Service.CacheManager
{
    /// <summary>
    /// The cache manager windows service. Responsible for initializing, starting, and stopping the cache manager functionality.
    /// </summary>
    internal class CacheManagerService : ServiceBase
    {
        // The cache manager engine that does the actual work
        private IRunnable _cacheManagerEngine = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        static void Main(string[] args)
        {
            var servicesToRun = new ServiceBase[] 
            { 
                new CacheManagerService()
            };

            ServiceBase.Run(servicesToRun);
        }

        /// <summary>
        /// The constructor.
        /// </summary>
        public CacheManagerService()
        {
            // Set the host name
            ServiceName = "Dache Cache Manager";

            // Configure custom logging
            // TODO: verify that this actually works
            try
            {
                var customLoggerTypeString = CacheManagerConfigurationSection.Settings.CustomLogger.Type;
                // Check for custom logger
                if (string.IsNullOrWhiteSpace(customLoggerTypeString))
                {
                    // No custom logging
                    LoggerContainer.Instance = new EventViewerLogger("Cache Manager", "Dache");
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
                LoggerContainer.Instance = new EventViewerLogger("Cache Manager", "Dache");
                return;
            }
        }

        /// <summary>
        /// Fires when the windows service starts.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        protected override void OnStart(string[] args)
        {
            LoggerContainer.Instance.Info("Cache Manager is starting", "Cache Manager is starting");
            
            // Configure the thread pool's minimum threads
            ThreadPool.SetMinThreads(128, 128);

            LoggerContainer.Instance.Info("Cache Manager is starting", "Verifying settings");

            try
            {
                // Initialize the cache host information poller
                var cacheHostInformationPollingIntervalMilliseconds = CacheManagerConfigurationSection.Settings.CacheHostInformationPollingIntervalMilliseconds;
                var cacheHostInformationPoller = new CacheHostInformationPoller(cacheHostInformationPollingIntervalMilliseconds);

                // Initialize the cache to manager service host
                var cacheToManagerServiceHost = new ServiceHost(typeof(CacheToManagerServer));
                // Configure the cache to manager service host
                var cacheManagerAddress = CacheManagerConfigurationSection.Settings.Address;
                var cacheManagerPort = CacheManagerConfigurationSection.Settings.Port;
                // Build the endpoint address
                var endpointAddressFormattedString = "net.tcp://{0}:{1}/Dache/CacheManager";
                var endpointAddress = string.Format(endpointAddressFormattedString, cacheManagerAddress, cacheManagerPort);
                // Build the net tcp binding
                var netTcpBinding = CreateNetTcpBinding("http://schemas.getdache.net/cachemanager");

                // Service throttling
                var serviceThrottling = cacheToManagerServiceHost.Description.Behaviors.Find<ServiceThrottlingBehavior>();
                if (serviceThrottling == null)
                {
                    serviceThrottling = new ServiceThrottlingBehavior
                    {
                        MaxConcurrentCalls = int.MaxValue,
                        MaxConcurrentInstances = int.MaxValue,
                        MaxConcurrentSessions = int.MaxValue
                    };

                    cacheToManagerServiceHost.Description.Behaviors.Add(serviceThrottling);
                }

                // Configure the service endpoint
                cacheToManagerServiceHost.AddServiceEndpoint(typeof(ICacheToManagerContract), netTcpBinding, endpointAddress);



                // Initialize the Dacheboard to manager service host
                var boardToManagerServiceHost = new ServiceHost(typeof(BoardToManagerServer));
                // Configure the board to manager service host
                cacheManagerAddress = CacheManagerConfigurationSection.Settings.Address;
                cacheManagerPort = CacheManagerConfigurationSection.Settings.DacheboardPort;
                // Build the endpoint address
                endpointAddressFormattedString = "net.tcp://{0}:{1}/Dache/Dacheboard";
                endpointAddress = string.Format(endpointAddressFormattedString, cacheManagerAddress, cacheManagerPort);
                // Build the net tcp binding
                netTcpBinding = CreateNetTcpBinding("http://schemas.getdache.net/dacheboard");

                // Service throttling
                serviceThrottling = boardToManagerServiceHost.Description.Behaviors.Find<ServiceThrottlingBehavior>();
                if (serviceThrottling == null)
                {
                    serviceThrottling = new ServiceThrottlingBehavior
                    {
                        MaxConcurrentCalls = int.MaxValue,
                        MaxConcurrentInstances = int.MaxValue,
                        MaxConcurrentSessions = int.MaxValue
                    };

                    boardToManagerServiceHost.Description.Behaviors.Add(serviceThrottling);
                }

                // Configure the service endpoint
                boardToManagerServiceHost.AddServiceEndpoint(typeof(IBoardToManagerContract), netTcpBinding, endpointAddress);

                // Instantiate the cache manager engine
                _cacheManagerEngine = new CacheManagerEngine(cacheHostInformationPoller, cacheToManagerServiceHost, boardToManagerServiceHost);
            }
            catch (Exception ex)
            {
                // The inner exception has the actual details of the configuration error
                if (ex.InnerException != null && ex.InnerException.Message != null && ex.InnerException.Message.StartsWith("The value for the property", StringComparison.OrdinalIgnoreCase))
                {
                    ex = ex.InnerException;
                }

                // Log the error
                LoggerContainer.Instance.Error("Cache Manager failed to start", ex.Message);

                // Stop the service
                Stop();
            }

            LoggerContainer.Instance.Info("Cache Manager is starting", "Settings verified successfully");

            _cacheManagerEngine.Start();
        }

        /// <summary>
        /// Fires when the windows service stops.
        /// </summary>
        protected override void OnStop()
        {
            LoggerContainer.Instance.Info("Cache Manager is stopping", "Cache Manager is stopping");

            _cacheManagerEngine.Stop();
        }

        /// <summary>
        /// Creates a configured net tcp binding for communication.
        /// </summary>
        /// <param name="serviceNamespace">The service namespace.</param>
        /// <returns>A configured net tcp binding.</returns>
        private NetTcpBinding CreateNetTcpBinding(string serviceNamespace)
        {
            // Sanitize
            if (string.IsNullOrWhiteSpace(serviceNamespace))
            {
                throw new ArgumentException("cannot be null, empty, or white space", "serviceNamespace");
            }

            var netTcpBinding = new NetTcpBinding(SecurityMode.None, false)
            {
                // TODO: review settings, centralize this logic
                CloseTimeout = TimeSpan.FromSeconds(15),
                OpenTimeout = TimeSpan.FromSeconds(15),
                SendTimeout = TimeSpan.FromSeconds(15),
                ReceiveTimeout = TimeSpan.MaxValue,
                Namespace = serviceNamespace,
                MaxBufferSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue,
                MaxConnections = 100000,
                ListenBacklog = 100000,
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
