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
        /// How often to attempt to re-establish the connection to a disconnected cache host, in seconds.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = 300)]
        [ConfigurationProperty("hostReconnectIntervalSeconds", IsRequired = true, DefaultValue = 10)]
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
        /// The host redundancy layers. If > 0, this indicates how many servers will hold duplicated data per cache host. 
        /// In practical terms, setting this to > 0 creates high availability.
        /// </summary>
        [IntegerValidator(MinValue = 0, MaxValue = 10)]
        [ConfigurationProperty("hostRedundancyLayers", IsRequired = true, DefaultValue = 0)]
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
