using System;
using System.IO;

namespace BinarySerializer
{
    public class MemoryMappedByteArrayFile : VirtualFile
    {
        public MemoryMappedByteArrayFile(Context context, string name, long baseAddress, long length, Endian endianness = Endian.Little, long memoryMappedPriority = -1, Pointer parentPointer = null) : base(context, name, endianness, baseAddress, memoryMappedPriority: memoryMappedPriority, parentPointer: parentPointer)
        {
            Bytes = new byte[length];
        }

        public MemoryMappedByteArrayFile(Context context, string name, long baseAddress, byte[] bytes, Endian endianness = Endian.Little, long memoryMappedPriority = -1, Pointer parentPointer = null) : base(context, name, endianness, baseAddress, memoryMappedPriority: memoryMappedPriority, parentPointer: parentPointer)
        {
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public override long Length => Bytes.Length;
        public override bool IsMemoryMapped => true;

        private byte[] _bytes;

        protected byte[] Bytes
        {
            get => _bytes ?? throw new ObjectDisposedException(nameof(Stream));
            set => _bytes = value;
        }

        public override Reader CreateReader()
        {
            Reader reader = new Reader(new MemoryStream(Bytes), isLittleEndian: Endianness == Endian.Little);
            return reader;
        }

        public override Writer CreateWriter()
        {
            Writer writer = new Writer(new MemoryStream(Bytes), isLittleEndian: Endianness == Endian.Little);
            return writer;
        }

        public void WriteBytes(long position, byte[] source) {
            Array.Copy(source, 0, Bytes, position, Math.Min(source.Length, Bytes.Length-position));
        }

        public override void Dispose()
        {
            // Dispose base file
            base.Dispose();
            Bytes = null;
        }
    }
}