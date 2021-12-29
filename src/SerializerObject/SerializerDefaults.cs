using System.Text;

namespace BinarySerializer 
{
    public class SerializerDefaults
    {
        public Pointer Anchor { get; set; }
        public long? NullValue { get; set; }
        public Encoding StringEncoding { get; set; }
    }
}