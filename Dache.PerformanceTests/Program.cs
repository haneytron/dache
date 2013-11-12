using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.PerformanceTests
{
    class Program
    {
        static void Main(string[] args)
        {
            bool inputGood = false;
            do
            {
                Console.WriteLine("DACHE PERFORMANCE TESTS");
                Console.WriteLine();
                Console.WriteLine("Choose your test:");
                Console.WriteLine();
                Console.WriteLine("    1. Multiple Operation");
                Console.WriteLine("    2. Infinite Add (Run multiple copies to stress test)");
                Console.WriteLine();
                Console.Write("Your choice: ");

                var keyPressed = Console.ReadKey().KeyChar;

                Console.Clear();

                int selection = 0;
                if (!int.TryParse(new string(new[] { keyPressed }), out selection))
                {
                    continue;
                }

                inputGood = true;

                if (selection == 1)
                {
                    MultiOperation.MultiOperationTest.Run();
                }
                else if (selection == 2)
                {
                    InfiniteAdd.InfiniteAddTest.Run();
                }
                else
                {
                    inputGood = false;
                }


            } while (!inputGood);
        }
    }
}
