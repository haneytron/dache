using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Dache.CacheHost.Configuration;
using Dache.CacheHost.Storage;
using Dache.Core.Performance;

namespace Dache.CacheHost
{
    class Program
    {
        private static CacheHostEngine _cacheHostEngine = null;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        /// <param name="args">The arguments passed, if any.</param>
        static void Main(string[] args)
        {
            // Configure the thread pool's minimum threads
            ThreadPool.SetMinThreads(128, 128);

            var version = Assembly.GetEntryAssembly().GetName().Version;

            Console.Title = "Dache Cache Host " + version;
            Console.ForegroundColor = ConsoleColor.Cyan;

            Console.WriteLine("INFO: Loading Settings...");
            Console.WriteLine();

            var result = ConfigureCacheHostEngine();
            if (!result) return;

            
            Console.WriteLine("INFO: Starting " + Console.Title + "...");

            _cacheHostEngine.Start();

            Console.WriteLine("INFO: Started Successfully");

            new AutoResetEvent(false).WaitOne();
        }

        static bool ConfigureCacheHostEngine()
        {
            // Gather settings
            CacheHostConfigurationSection configuration = null;
            try
            {
                configuration = CacheHostConfigurationSection.Settings;
            }
            catch (TypeInitializationException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: cacheHostSettings configuration is incorrect");
                Console.WriteLine("ERROR: " + ex.GetBaseException().Message);
                Console.WriteLine();
                Console.Write("Press any key to exit...");
                Console.ReadKey();
                return false;
            }

            if (configuration == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: cacheHostSettings configuration blockis  missing");
                Console.WriteLine("ERROR: Please verify that cacheHostSettings exists in config file");
                Console.WriteLine();
                Console.Write("Press any key to exit...");
                Console.ReadKey();
                return false;
            }

            // Instantiate the cache host engine
            _cacheHostEngine = new CacheHostEngine(configuration);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("SETTINGS: Listening on port " + configuration.Port);
            Console.WriteLine("SETTINGS: Memory Limit      " + configuration.CacheMemoryLimitPercentage + "%");
            Console.WriteLine("SETTINGS: Max Connections   " + configuration.MaximumConnections);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;

            return true;
        }
    }
}
