#nullable enable
using System.Text;

namespace BinarySerializer 
{
    public class SerializerDefaults
    {
        public Pointer? PointerAnchor { get; set; }
        public long? PointerNullValue { get; set; }
        public Encoding? StringEncoding { get; set; }

        /// <summary>
        /// All pointers serialized act as if they're read from that file. Useful for encoded files.
        /// </summary>
        public BinaryFile? PointerFile { get; set; }
    }
}