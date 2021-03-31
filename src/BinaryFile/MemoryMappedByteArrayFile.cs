using System;
using System.IO;

namespace BinarySerializer
{
    public class MemoryMappedByteArrayFile : MemoryMappedFile
    {
        public MemoryMappedByteArrayFile(string name, uint length, Context context, uint baseAddress) : base(context, baseAddress)
        {
            FilePath = name;
            Bytes = new byte[length];
        }
        public MemoryMappedByteArrayFile(string name, byte[] bytes, Context context, uint baseAddress) : base(context, baseAddress) 
        {
            FilePath = name;
            Bytes = bytes;
        }

        public override uint Length => (uint)Bytes.Length;

        public byte[] Bytes { get; }

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

        public void WriteBytes(uint position, byte[] source) {
            Array.Copy(source, 0, Bytes, position, Math.Min(source.Length, Bytes.Length-position));
        }
    }
}