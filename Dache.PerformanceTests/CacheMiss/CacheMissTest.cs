using System;
using System.Threading;
using Dache.Client;

namespace Dache.PerformanceTests.CacheMiss
{
    internal static class CacheMissTest
    {
        public static void Run()
        {
            var cacheClient = new CacheClient();

            Console.WriteLine("***** BEGIN CACHE MISS TEST *****");
            Console.WriteLine();

            cacheClient.HostDisconnected += (sender, e) => { Console.WriteLine("*** Host disconnected"); };
            cacheClient.HostReconnected += (sender, e) => { Console.WriteLine("*** Host reconnected"); };

            // Try and get 10 items
            string value = null;
            for (int i = 0; i < 10; i++)
            {
                var cacheKey = "doesNotExist" + i;
                var result = cacheClient.TryGet(cacheKey, out value);
                if (!result)
                {
                    Console.WriteLine("PASS: Did not receive value for cache key: {0}", cacheKey);
                }
                else
                {
                    Console.WriteLine("FAIL: Received value for cache key: {0}", cacheKey);
                }
            }

            // Bulk 10
            var bulkResult = cacheClient.Get<string>(new[] { "doesNotExist0", "doesNotExist1", "doesNotExist2", "doesNotExist3", "doesNotExist4",
                "doesNotExist5", "doesNotExist6", "doesNotExist7", "doesNotExist8", "doesNotExist9" });

            if (bulkResult == null)
            {
                Console.WriteLine("PASS: Did not receive values for bulks cache keys");
            }
            else
            {
                Console.WriteLine("FAIL: Received values for bulks cache keys");
            }

            Console.ReadKey();
        }
    }
}
