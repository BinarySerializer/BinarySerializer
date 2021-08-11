namespace BinarySerializer
{
    public class LinearFile : PhysicalFile 
    {
        public LinearFile(Context context, string filePath, Endian endianness = Endian.Little, long fileLength = 0) : base(context, filePath, endianness, fileLength: fileLength) { }

        public override bool IsMemoryMapped => false;
    }
}