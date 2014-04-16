using System.Configuration;

namespace Dache.Board.Configuration
{
    /// <summary>
    /// An application configuration section that allows a user to specify dache board settings.
    /// </summary>
    public class DacheboardConfigurationSection : ConfigurationSection
    {
        // The static readonly Dacheboard configuration section of the application configuration
        private static readonly DacheboardConfigurationSection _settings = ConfigurationManager.GetSection("dacheboardSettings") as DacheboardConfigurationSection;

        /// <summary>
        /// Gets Dacheboard settings.
        /// </summary>
        public static DacheboardConfigurationSection Settings
        {
            get { return _settings; }
        }

        /// <summary>
        /// Gets or sets Dacheboard address.
        /// </summary>
        [StringValidator(InvalidCharacters = @"\:")]
        [ConfigurationProperty("address", IsRequired = true)]
        public string Address
        {
            get { return (string)this["address"]; }
            set { this["address"] = value; }
        }

        /// <summary>
        /// Gets or sets Dacheboard port.
        /// </summary>
        [IntegerValidator(MinValue = 1, MaxValue = int.MaxValue)]
        [ConfigurationProperty("port", IsRequired = true, DefaultValue = 33333)]
        public int Port
        {
            get { return (int)this["port"]; }
            set { this["port"] = value; }
        }

        /// <summary>
        /// Gets the manager reconnect interval in milliseconds.
        /// </summary>
        /// <value>
        /// The manager reconnect interval milliseconds.
        /// </value>
        /// <remarks>
        /// How often to attempt to re-establish the connection to the disconnected cache manager, in milliseconds.
        /// </remarks>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("managerReconnectIntervalMilliseconds", IsRequired = true, DefaultValue = 5000)]
        public int ManagerReconnectIntervalMilliseconds
        {
            get { return (int)this["managerReconnectIntervalMilliseconds"]; }
        }

        /// <summary>
        /// Gets or sets the information polling interval in milliseconds.
        /// </summary>
        /// <value>
        /// The Dacheboard information polling interval in milliseconds. Valid range is 1000 to 60000.
        /// </value>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("informationPollingIntervalMilliseconds", IsRequired = true, DefaultValue = 1000)]
        public int InformationPollingIntervalMilliseconds
        {
            get { return (int)this["informationPollingIntervalMilliseconds"]; }
            set { this["informationPollingIntervalMilliseconds"] = value; }
        }
    }
}
