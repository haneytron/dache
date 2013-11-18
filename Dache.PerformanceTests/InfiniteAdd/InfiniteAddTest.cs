using System;
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

            Console.WriteLine("***** BEGIN INFINITE ADD TEST (WILL NEVER END) *****");
            Console.WriteLine();

            int i = 1;
            while (true)
            {
                cacheClient.AddOrUpdate("test" + i, value);
                i++;
                if (i == 1000)
                {
                    i = 0;
                }
            }
        }
    }
}
