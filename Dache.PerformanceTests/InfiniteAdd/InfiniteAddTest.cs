using System;
using System.Threading;
using Dache.Client;

namespace Dache.PerformanceTests.InfiniteAdd
{
    internal static class InfiniteAddTest
    {
        public static void Run()
        {
            var cacheClient = new CacheClient();

            // 502 chars = 1 kb
            string value = "asdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasas";

            int itemsToAdd = 1000;

            Console.WriteLine("***** BEGIN INFINITE ADD " + itemsToAdd + " ITEMS TEST (WILL NEVER END) *****");
            Console.WriteLine();

            cacheClient.HostDisconnected += (sender, e) => { Console.WriteLine("*** Host disconnected"); };
            cacheClient.HostReconnected += (sender, e) => { Console.WriteLine("*** Host reconnected"); };

            // Add items
            int i = 0;
            while (true)
            {
                cacheClient.AddOrUpdate("test" + i, value);
                i++;
                if (i == itemsToAdd)
                {
                    i = 0;
                }
            }
        }
    }
}
