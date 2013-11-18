using Dache.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dache.PerformanceTests.MultiOperation
{
    internal static class MultiOperationTest
    {
        public static void Run()
        {
            var cacheClient = new CacheClient();

            var stopwatch = new Stopwatch();

            var overallStopwatch = new Stopwatch();
            overallStopwatch.Start();

            // 502 chars = 1 kb
            string value = "asdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasas";

            CustomObject myObject = new CustomObject();

            Console.WriteLine("***** BEGIN ADD 10000 TESTS *****");
            Console.WriteLine();

            #region Regular Add 10000 strings
            stopwatch.Start();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.AddOrUpdate("test" + i, value);
            }
            stopwatch.Stop();

            Console.WriteLine("Add time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Bulk Add 10000 strings
            var list = new List<KeyValuePair<string, object>>(10000);
            for (int i = 1; i <= 10000; i++)
            {
                list.Add(new KeyValuePair<string, object>("bulktest" + i, value));
            }

            stopwatch.Restart();
            cacheClient.AddOrUpdate(list);
            stopwatch.Stop();

            Console.WriteLine("Add Many time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Tagged Add 10000 strings
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.AddOrUpdateTagged("tagtest" + i, value, "demotag");
            }
            stopwatch.Stop();

            Console.WriteLine("Add Tagged time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN SINGLE KEY GET TEST *****");
            Console.WriteLine();

            #region Regular Get 1 string proof
            string resultString = null;
            stopwatch.Restart();
            var result = cacheClient.TryGet("test5", out resultString) && string.Equals(resultString, value);
            stopwatch.Stop();

            Console.WriteLine("Get cache key \"test5\": time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            Console.WriteLine("Get cache key \"test5\": successful = " + result);
            Console.WriteLine("Get cache key \"test5\": result length: " + (resultString == null ? 0 : resultString.Length) + ", original length: " + value.Length);
            if (resultString != null)
            {
                var origHash = value.GetHashCode();
                var resultHash = resultString.GetHashCode();
                Console.WriteLine("Original String Hash Code: " + origHash + ", Result String Hash Code: " + resultHash);
            }
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN GET 10000 TESTS *****");
            Console.WriteLine();

            #region Regular Get 10000 strings
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                if (!cacheClient.TryGet("test" + i, out value))
                {
                    Console.WriteLine("Get failed for cache key: " + "test" + i);
                }
            }
            stopwatch.Stop();

            Console.WriteLine("Get time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Bulk Get 10000 strings
            var list2 = new List<string>(10000);
            for (int i = 1; i <= 10000; i++)
            {
                list2.Add("bulktest" + i);
            }

            stopwatch.Restart();
            var getManyResults = cacheClient.Get<string>(list2);
            stopwatch.Stop();

            Console.WriteLine("Get Many time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            Console.WriteLine("Get Many object count: " + getManyResults.Count);
            #endregion

            #region Tagged Get 10000 strings
            stopwatch.Restart();
            var getTaggedResults = cacheClient.GetTagged<string>("demotag");
            stopwatch.Stop();

            Console.WriteLine("Get Tagged time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            Console.WriteLine("Get Tagged object count: " + getManyResults.Count);
            #endregion

            #region Local Get 10000 strings
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                if (!cacheClient.TryGetLocal("test" + i, out value))
                {
                    Console.WriteLine("Get failed for cache key: " + "test" + i);
                }
            }
            stopwatch.Stop();

            Console.WriteLine("Local Get time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Local Get again 10000 strings
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                if (!cacheClient.TryGetLocal("test" + i, out value))
                {
                    Console.WriteLine("Get failed for cache key: " + "test" + i);
                }
            }
            stopwatch.Stop();

            Console.WriteLine("Local Get again time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN REMOVE 10000 TESTS *****");
            Console.WriteLine();

            #region Regular Remove 10000 strings
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.Remove("test" + i);
            }
            stopwatch.Stop();

            Console.WriteLine("Remove time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Bulk Remove 10000 strings
            stopwatch.Restart();
            cacheClient.Remove(list2);
            stopwatch.Stop();

            Console.WriteLine("Remove Many time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Tagged Remove 10000 strings
            stopwatch.Restart();
            cacheClient.RemoveTagged("demotag");
            stopwatch.Stop();

            Console.WriteLine("Remove Tagged time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN ABSOLUTE EXPIRATION TEST *****");
            Console.WriteLine();

            #region Regular Add 10000 strings with absolute expiration
            var absoluteExpiration = DateTime.Now.AddMinutes(1);
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.AddOrUpdate("testabsolute" + i, value, absoluteExpiration);
            }
            stopwatch.Stop();

            Console.WriteLine("Add absolute expiration time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN 10000 COMPLEX OBJECT ADD TEST *****");
            Console.WriteLine();

            #region Regular Add 10000 complex objects with sliding expiration
            var slidingExpiration = new TimeSpan(0, 2, 0);
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.AddOrUpdate("testabsolutecomplex" + i, myObject, slidingExpiration);
            }
            stopwatch.Stop();

            Console.WriteLine("Add complex object time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            Console.WriteLine();
            Console.WriteLine("***** BEGIN 10000 COMPLEX OBJECT GET TEST *****");
            Console.WriteLine();

            #region Regular Get 10000 complex objects with sliding expiration
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.TryGet("testabsolutecomplex" + i, out myObject);
            }
            stopwatch.Stop();

            Console.WriteLine("Get complex object time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Local Get 10000 complex objects with sliding expiration
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.TryGetLocal("testabsolutecomplex" + i, out myObject);
            }
            stopwatch.Stop();

            Console.WriteLine("Local Get complex object time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            #region Local Get again 10000 complex objects with sliding expiration
            stopwatch.Restart();
            for (int i = 1; i <= 10000; i++)
            {
                cacheClient.TryGetLocal("testabsolutecomplex" + i, out myObject);
            }
            stopwatch.Stop();

            Console.WriteLine("Local Get again complex object time taken: " + stopwatch.ElapsedMilliseconds + " ms, " + stopwatch.ElapsedTicks + " ticks");
            #endregion

            overallStopwatch.Stop();

            Console.WriteLine();
            Console.WriteLine("***** END TESTS *****");
            Console.WriteLine();

            Console.WriteLine("Total time taken: " + overallStopwatch.ElapsedMilliseconds + " ms, " + overallStopwatch.ElapsedTicks + " ticks");
            Console.ReadKey();
        }
    }
}
