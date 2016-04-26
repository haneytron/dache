using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dache.PerformanceTests.MultiOperation
{
    [Serializable, ProtoContract(SkipConstructor = true)]
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

        [ProtoMember(1)]
        public bool Thing1 { get; set; }

        [ProtoMember(2)]
        public int Thing2 { get; set; }

        [ProtoMember(3)]
        public char Thing3 { get; set; }

        [ProtoMember(4)]
        public string Thing4 { get; set; }

        [ProtoMember(5)]
        public Dictionary<string, string> Thing5 { get; set; }
    }
}
