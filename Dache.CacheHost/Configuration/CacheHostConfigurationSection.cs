using System;
using System.ComponentModel;
using System.Configuration;
using Dache.CacheHost.Storage;

namespace Dache.CacheHost.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify cache host settings.
    /// </summary>
    public class CacheHostConfigurationSection : ConfigurationSection
    {
        // The static readonly cache host configuration section of the application configuration
        private static readonly CacheHostConfigurationSection _settings = ConfigurationManager.GetSection("cacheHostSettings") as CacheHostConfigurationSection;

        /// <summary>
        /// Provides a reference to the cache host settings from configuration file.
        /// </summary>
        public static CacheHostConfigurationSection Settings
        {
            get
            {
                return _settings;
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
        }

        /// <summary>
        /// The cache host port. The default is 33333. Valid range is &gt; 0.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("port", IsRequired = true, DefaultValue = 33333)]
        public int Port
        {
            get
            {
                return (int)this["port"];
            }
            set
            {
                this["port"] = value;
            }
        }

        /// <summary>
        /// The maximum connections. The default is 20. Valid range is &gt; 0.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("maximumConnections", IsRequired = false, DefaultValue = 20)]
        public int MaximumConnections
        {
            get
            {
                return (int)this["maximumConnections"];
            }
            set
            {
                this["maximumConnections"] = value;
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
        /// The cache memory limit, as a percentage of the total system memory. The default is 80. Valid range is 5 to 90.
        /// </summary>
        [IntegerValidator(MinValue = 5, MaxValue = 90)]
        [ConfigurationProperty("cacheMemoryLimitPercentage", IsRequired = false, DefaultValue = 80)]
        public int CacheMemoryLimitPercentage
        {
            get
            {
                return (int)this["cacheMemoryLimitPercentage"];
            }
            set
            {
                this["cacheMemoryLimitPercentage"] = value;
            }
        }

        /// <summary>
        /// Whether or not to compress data via GZip when storing it in the cache. The default is false. Valid options are true and false.
        /// </summary>
        [ConfigurationProperty("compressData", IsRequired = false, DefaultValue = false)]
        public bool CompressData
        {
            get
            {
                return (bool)this["compressData"];
            }
            set
            {
                this["compressData"] = value;
            }
        }
    }
}
