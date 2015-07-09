using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dache.Client.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Diagnostics;

namespace Dache.Tests.Dache.Client
{
    [TestClass]
    public class GZipSerializerTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException), "value")]
        public void GZipSerializer_Serialize_Null_Value_Throws_ArgumentNullException()
        {
            GZipSerializer sut = new GZipSerializer();

            sut.Serialize<object>(null);
        }

        [TestMethod]
        public void GZipSerializer_Serialize_Deserialize_Returns_Same_Object()
        {
            SerializeTestClass serializeTestClass = new SerializeTestClass
            {
                IntProp = 5,
                StringProp = "Test",
                DateTimeProp = DateTime.Today
            };

            GZipSerializer sut = new GZipSerializer();

            byte[] serializedClass = sut.Serialize(serializeTestClass);
            SerializeTestClass result = sut.Deserialize<SerializeTestClass>(serializedClass);

            Assert.IsNotNull(serializedClass);
            Assert.IsNotNull(result);
            Assert.AreEqual(serializeTestClass.DateTimeProp, result.DateTimeProp);
            Assert.AreEqual(serializeTestClass.IntProp, result.IntProp);
            Assert.AreEqual(serializeTestClass.StringProp, result.StringProp);
        }

        [TestMethod]
        public void GZipSerializer_Serialize_Smaller_Then_Just_Serialize()
        {
            SerializeTestClass serializeTestClass = new SerializeTestClass
            {
                IntProp = 5,
                StringProp = @"Overly long unformatted statements present fellow editors a dilemma: spend excessive time parsing out what a writer means or being mildly rude in not actually reading what is written. 
                               It is more collegial and collaborative to take an extra few moments to distill one's thoughts into bite size pieces.
                               Traditionally, the phrase too long; didn't read (abbreviated tl;dr or simply tldr) has been used on the Internet as a reply to an excessively long statement. 
                               It indicates that the reader did not actually read the statement due to its undue length.[2] This essay especially considers the term as used in Wikipedia discussions, 
                               and examines methods of fixing the problem when found in article content.
                               As a label, it is sometimes used as a tactic to thwart the kinds of discussion which are essential in collaborative editing. 
                               On the other hand, tl;dr may represent a shorthand acknowledgement of time saved by skimming over or skipping repetitive or poorly written material. 
                               Thus, the implication of the symbol can range from a brilliant and informative disquisition being given up due to a reader's lack of endurance, interest, or intelligence,
                               to a clustered composition of such utter failure to communicate that it has left the capable reader with a headache; judging this range is very subjective.",
                DateTimeProp = DateTime.Today
            };

            GZipSerializer sut = new GZipSerializer();

            byte[] serializedWithGZipSerializer = sut.Serialize(serializeTestClass);
            byte[] justSerialized = SerializeToByteArray(serializeTestClass);

            long sizeOfGzipped = GetSize(serializedWithGZipSerializer);
            long sizeOfSerialized = GetSize(justSerialized);

            Debug.WriteLine(string.Format("Size of zipped: {0} bytes", sizeOfGzipped));
            Debug.WriteLine(string.Format("Size of raw:    {0} bytes", sizeOfSerialized));

            Assert.IsNotNull(serializedWithGZipSerializer);
            Assert.IsNotNull(justSerialized);
            Assert.IsTrue(sizeOfGzipped < sizeOfSerialized);
        }

        [TestMethod]
        public void GZipSerializer_Deserialize_Null_Value_Returns_Null()
        {
            GZipSerializer sut = new GZipSerializer();

            object result = sut.Deserialize<object>(null);

            Assert.IsNull(result);
        }

        public long GetSize(object obj)
        {
            using (Stream s = new MemoryStream())
            {
                new BinaryFormatter().Serialize(s, obj);
                return s.Length;
            }
        }

        public byte[] SerializeToByteArray(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            using (var ms = new MemoryStream())
            {
                new BinaryFormatter().Serialize(ms, obj);

                return ms.ToArray();
            }
        }
    }

    [Serializable]
    public class SerializeTestClass
    {
        public int IntProp { get; set; }

        public string StringProp { get; set; }

        public DateTime DateTimeProp { get; set; }
    }
}
