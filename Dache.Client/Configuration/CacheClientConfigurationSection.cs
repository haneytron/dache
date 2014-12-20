using System.Configuration;
using Dache.CacheHost.Configuration;

namespace Dache.Client.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache client settings.
    /// </summary>
    public class CacheClientConfigurationSection : ConfigurationSection
    {
        // The static readonly cache client configuration section of the application configuration
        private static readonly CacheClientConfigurationSection _settings = ConfigurationManager.GetSection("cacheClientSettings") as CacheClientConfigurationSection;
        
        /// <summary>
        /// The cache host settings.
        /// </summary>
        public static CacheClientConfigurationSection Settings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// How often to attempt to re-establish the connection to a disconnected cache host, in seconds. The default is 10. Valid range is &gt; 5.
        /// </summary>
        [IntegerValidator(MinValue = 5, MaxValue = int.MaxValue)]
        [ConfigurationProperty("hostReconnectIntervalSeconds", IsRequired = false, DefaultValue = 10)]
        public int HostReconnectIntervalSeconds
        {
            get
            {
                return (int)this["hostReconnectIntervalSeconds"];
            }
            set
            {
                this["hostReconnectIntervalSeconds"] = value;
            }
        }

        /// <summary>
        /// The host redundancy layers. If &gt; 0, this indicates how many servers will hold duplicated data per cache host. 
        /// In practical terms, setting this to &gt; 0 creates high availability. The default is 0. Valid range is 0 to 10.
        /// </summary>
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        [ConfigurationProperty("hostRedundancyLayers", IsRequired = false, DefaultValue = 0)]
        public int HostRedundancyLayers
        {
            get
            {
                return (int)this["hostRedundancyLayers"];
            }
            set
            {
                this["hostRedundancyLayers"] = value;
            }
        }

        /// <summary>
        /// The message buffer size. The default is 1024. Valid range is 1024 to 4096.
        /// </summary>
        [IntegerValidator(MinValue = 1024, MaxValue = 4096)]
        [ConfigurationProperty("messageBufferSize", IsRequired = false, DefaultValue = 1024)]
        public int MessageBufferSize
        {
            get
            {
                return (int)this["messageBufferSize"];
            }
            set
            {
                this["messageBufferSize"] = value;
            }
        }

        /// <summary>
        /// How long to permit a communication attempt before forcefully closing the connection. The default is 10. Valid range is &gt;= 5.
        /// </summary>
        [IntegerValidator(MinValue = 5, MaxValue = int.MaxValue)]
        [ConfigurationProperty("communicationTimeoutSeconds", IsRequired = false, DefaultValue = 10)]
        public int CommunicationTimeoutSeconds
        {
            get
            {
                return (int)this["communicationTimeoutSeconds"];
            }
            set
            {
                this["communicationTimeoutSeconds"] = value;
            }
        }

        /// <summary>
        /// The maximum size of a message permitted. The default is 104857600 (1 MB). Valid range is &gt;= 52428800 (512 KB).
        /// </summary>
        [IntegerValidator(MinValue = 104857600, MaxValue = int.MaxValue)]
        [ConfigurationProperty("maximumMessageSize", IsRequired = false, DefaultValue = 104857600)]
        public int MaximumMessageSize
        {
            get
            {
                return (int)this["maximumMessageSize"];
            }
            set
            {
                this["maximumMessageSize"] = value;
            }
        }

        /// <summary>
        /// The custom logger.
        /// </summary>
        [ConfigurationProperty("customLogger", IsRequired = false)]
        public CustomTypeElement CustomLogger
        {
            get
            {
                return (CustomTypeElement)this["customLogger"];
            }
            set
            {
                this["customLogger"] = value;
            }
        }

        /// <summary>
        /// The custom serializer.
        /// </summary>
        [ConfigurationProperty("customSerializer", IsRequired = false)]
        public CustomTypeElement CustomSerializer
        {
            get
            {
                return (CustomTypeElement)this["customSerializer"];
            }
            set
            {
                this["customSerializer"] = value;
            }
        }

        /// <summary>
        /// The cache hosts collection.
        /// </summary>
        [ConfigurationProperty("cacheHosts", IsDefaultCollection = false)]
        [ConfigurationCollection(typeof(CacheHostsCollection), AddItemName = "add", RemoveItemName = "remove", ClearItemsName = "clear")]
        public CacheHostsCollection CacheHosts
        {
            get
            {
                return (CacheHostsCollection)this["cacheHosts"];
            }
            set
            {
                this["cacheHosts"] = value;
            }
        }
    }
}
