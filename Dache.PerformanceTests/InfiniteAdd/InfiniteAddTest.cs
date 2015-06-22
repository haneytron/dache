using System;
using System.Threading;
using System.Threading.Tasks;
using Dache.Client;
using Dache.Client.Exceptions;

namespace Dache.PerformanceTests.InfiniteAdd
{
    internal static class InfiniteAddTest
    {
        public static void Run()
        {
            var cacheClient = new CacheClient();

            // 502 chars = ~1 kb
            string value = "asdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasas";

            int itemsToAdd = 1000;

            Console.WriteLine("***** BEGIN INFINITE ADD " + itemsToAdd + " 1 KB STRING OBJECTS TEST (WILL NEVER END) *****");
            Console.WriteLine();

            cacheClient.HostDisconnected += (sender, e) => { Console.WriteLine("*** Host disconnected"); };
            cacheClient.HostReconnected += (sender, e) => { Console.WriteLine("*** Host reconnected"); };

            // Add items
            Task.Factory.StartNew(() => {
                int i = 0;
                while (true)
                {
                    try
                    {
                        cacheClient.AddOrUpdate("test" + i, value);
                    }
                    catch (NoCacheHostsAvailableException)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    i++;
                    if (i == itemsToAdd)
                    {
                        i = 0;
                    }
                }
            });

            Console.ReadKey();
        }
    }
}
