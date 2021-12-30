using System.Text;

namespace BinarySerializer 
{
    public class SerializerDefaults
    {
        public Pointer PointerAnchor { get; set; }
        public long? PointerNullValue { get; set; }
        public Encoding StringEncoding { get; set; }
    }
}