using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.PerformanceTests.MultiOperation
{
    [Serializable]
    public class CustomObject
    {
        public CustomObject()
        {
            Thing1 = true;
            Thing2 = int.MaxValue;
            Thing3 = 'q';
            Thing4 = "asdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasasdfasdfasas";
            Thing5 = new Dictionary<string, string>();
            Thing5.Add("test1", "test2");
            Thing5.Add("test3", "test4");
        }

        public bool Thing1 { get; set; }

        public int Thing2 { get; set; }

        public char Thing3 { get; set; }

        public string Thing4 { get; set; }

        public Dictionary<string, string> Thing5 { get; set; }
    }
}
