using System;
using System.ComponentModel;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using Dache.Core.Performance;

namespace Dache.CacheHost
{
    /// <summary>
    /// Configures the service with the intended values for name, version, description, account type and start-up type, etc.
    /// </summary>
    [RunInstaller(true)]
    public class CustomInstaller : Installer
    {
        // The service installer
        private ServiceInstaller _serviceInstaller = new ServiceInstaller();
        // The service process installer
        private ServiceProcessInstaller _serviceProcessInstaller = new ServiceProcessInstaller();
        // The service version
        private Version _serviceVersion = null;
        // The settings file name
        private const string _settingsFileName = "settings";
        // A line of stars for the console output
        private const string _consoleLineOfStars = "******************************************************************************";

        /// <summary>
        /// The constructor.
        /// </summary>
        public CustomInstaller()
        {
            // Config service installer
            var serviceName = "Dache Cache Host";
            _serviceVersion = Assembly.GetExecutingAssembly().GetName().Version;

            // Set some service information
            _serviceInstaller.DisplayName = serviceName; //+ " " + _serviceVersion;
            _serviceInstaller.ServiceName = serviceName; //+ " " + _serviceVersion;
            _serviceInstaller.StartType = System.ServiceProcess.ServiceStartMode.Automatic;

            // Perform install customizations
            BeforeInstall += CustomInstaller_BeforeInstall;
            // Perform rollback uninstall
            AfterRollback += CustomInstaller_BeforeUninstall;

            // Perform uninstall customizations
            BeforeUninstall += CustomInstaller_BeforeUninstall;

            // Now append version and assign description
            _serviceInstaller.Description = "Hosts an instance of the distributed cache.";

            // Config service process installer to install as Network Service
            _serviceProcessInstaller.Account = ServiceAccount.NetworkService;

            // Now add the Installers
            Installers.AddRange(new Installer[] { _serviceInstaller, _serviceProcessInstaller });
        }

        private void CustomInstaller_BeforeInstall(object sender, InstallEventArgs e)
        {
            Console.WriteLine(_consoleLineOfStars);
            Console.WriteLine("Enter the service name to install Cache Host as (leave blank to use default):");
            var customName = Console.ReadLine();
            Console.WriteLine(_consoleLineOfStars);
            if (!string.IsNullOrWhiteSpace(customName))
            {
                _serviceInstaller.DisplayName = customName; // +" " + _serviceVersion;
                _serviceInstaller.ServiceName = customName; // +" " + _serviceVersion;
            }

            // Create and save a settings file that says what this installation is named
            var installPath = Path.Combine(new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName, _settingsFileName);
            File.WriteAllText(installPath, "InstallName = " + _serviceInstaller.ServiceName.Trim());

            Console.WriteLine(_consoleLineOfStars);
            Console.Write("Service account is NetworkService. Use a custom account? (Y/N): ");
            var customServiceKey = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine(_consoleLineOfStars);
            if (customServiceKey.KeyChar.Equals('y') || customServiceKey.KeyChar.Equals('Y'))
            {
                _serviceProcessInstaller.Account = ServiceAccount.User;
            }

            Console.WriteLine(_consoleLineOfStars);
            Console.Write("Startup type is Automatic. Would you like Manual instead? (Y/N): ");
            var manualStartupKey = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine(_consoleLineOfStars);
            if (manualStartupKey.KeyChar.Equals('y') || manualStartupKey.KeyChar.Equals('Y'))
            {
                _serviceInstaller.StartType = ServiceStartMode.Manual;
            }

            // Ensure that event log source exists
            if (!EventLog.SourceExists("Cache Host"))
            {
                EventLog.CreateEventSource("Cache Host", "Dache");
            }

            // Create custom performance counters and counter category
            CustomPerformanceCounterInstaller.InstallCounters();
        }

        private void CustomInstaller_BeforeUninstall(object sender, InstallEventArgs e)
        {
            // Read the settings file that says what this installation is named - if it exists
            string settings = null;
            try
            {
                var installPath = Path.Combine(new FileInfo(System.Reflection.Assembly.GetExecutingAssembly().Location).DirectoryName, _settingsFileName);
                settings = File.ReadAllText(installPath);
                // Delete this file
                File.Delete(installPath);
            }
            catch
            {
                // Supress...
            }

            // Check if we have settings
            if (settings != null)
            {
                // We do, split by '='
                var splitSettings = settings.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (splitSettings.Length == 2)
                {
                    // Set service name based on installation
                    _serviceInstaller.ServiceName = splitSettings[1].Trim();
                }
            }

            Console.WriteLine(_consoleLineOfStars);
            Console.WriteLine("Do you want to uninstall the performance counters? Only do this if no other Dache Hosts are installed on this computer. (Y/N):");
            var uninstallPerformanceCountersKey = Console.ReadKey();
            Console.WriteLine();
            Console.WriteLine(_consoleLineOfStars);
            if (uninstallPerformanceCountersKey.KeyChar.Equals('y') || uninstallPerformanceCountersKey.KeyChar.Equals('Y'))
            {
                // Delete performance counter category
                CustomPerformanceCounterInstaller.UninstallCounters();
            }
        }
    }
}