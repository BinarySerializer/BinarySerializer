namespace BinarySerializer
{
    /// <summary>
    /// A <see cref="BinaryFile"/> which uses a virtual file
    /// </summary>
    public abstract class VirtualFile : BinaryFile
    {
        protected VirtualFile(Context context, string filePath, Endian endianness = Endian.Little, long baseAddress = 0, Pointer startPointer = null, long memoryMappedPriority = -1) : base(context, filePath, endianness, baseAddress, startPointer, memoryMappedPriority)
        {

        }
    }
}