using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading;

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
        /// The Dacheboard settings.
        /// </summary>
        public static DacheboardConfigurationSection Settings
        {
            get
            {
                return _settings;
            }
        }

        /// <summary>
        /// The Dacheboard address.
        /// </summary>
        [StringValidator(InvalidCharacters = @"\:")]
        [ConfigurationProperty("address", IsRequired = true)]
        public string Address
        {
            get
            {
                return (string)this["address"];
            }
            set
            {
                this["address"] = value;
            }
        }

        /// <summary>
        /// The Dacheboard port.
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
        /// How often to attempt to re-establish the connection to the disconnected cache manager, in milliseconds.
        /// </summary>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("managerReconnectIntervalMilliseconds", IsRequired = true, DefaultValue = 5000)]
        public int ManagerReconnectIntervalMilliseconds
        {
            get
            {
                return (int)this["managerReconnectIntervalMilliseconds"];
            }
        }

        /// <summary>
        /// The Dacheboard information polling interval in milliseconds. Valid range is 1000 to 60000.
        /// </summary>
        [IntegerValidator(MinValue = 1000, MaxValue = 60000)]
        [ConfigurationProperty("informationPollingIntervalMilliseconds", IsRequired = true, DefaultValue = 1000)]
        public int InformationPollingIntervalMilliseconds
        {
            get
            {
                return (int)this["informationPollingIntervalMilliseconds"];
            }
            set
            {
                this["informationPollingIntervalMilliseconds"] = value;
            }
        }
    }
}
