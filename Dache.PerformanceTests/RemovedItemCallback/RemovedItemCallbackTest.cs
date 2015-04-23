using System;
using System.Threading;
using Dache.Client;

namespace Dache.PerformanceTests.RemovedItemCallback
{
    internal static class RemovedItemCallbackTest
    {
        public static void Run()
        {
            int totalCallbacks = 0;
            var cacheClient = new CacheClient();

            // 502 chars = 1 kb
            string value = "asdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasas";

            int itemsToAdd = 1000;

            Console.WriteLine("***** BEGIN REMOVED ITEM CALLBACK TEST *****");
            Console.WriteLine();

            cacheClient.HostDisconnected += (sender, e) => { Console.WriteLine("*** Host disconnected"); };
            cacheClient.HostReconnected += (sender, e) => { Console.WriteLine("*** Host reconnected"); };
            cacheClient.CacheItemExpired += (sender, e) => { Interlocked.Increment(ref totalCallbacks); Console.WriteLine(string.Format("Cache key expired: {0}, Total Removed: {1}", e.CacheKey, totalCallbacks)); };

            // Add items
            for (int i = 1; i <= itemsToAdd; i++)
            {
                cacheClient.AddOrUpdate("test" + i, value, notifyRemoved: true);
            }

            Console.WriteLine("***** " + itemsToAdd + " ITEMS ADDED *****");
            Console.WriteLine("***** BEGIN REMOVING " + itemsToAdd + " ITEMS AFTER 2000 MS PAUSE *****");
            Thread.Sleep(2000);

            // Remove items
            for (int i = 1; i <= itemsToAdd; i++)
            {
                cacheClient.Remove("test" + i);
            }

            Console.ReadKey();
        }
    }
}
