using System;
using System.IO;

namespace BinarySerializer
{
    public class MemoryMappedByteArrayFile : MemoryMappedFile
    {
        public MemoryMappedByteArrayFile(Context context, string name, uint baseAddress, uint length, Endian endianness = Endian.Little) : base(context, name, baseAddress, endianness)
        {
            Bytes = new byte[length];
        }

        public MemoryMappedByteArrayFile(Context context, string name, uint baseAddress, byte[] bytes, Endian endianness = Endian.Little) : base(context, name, baseAddress, endianness)
        {
            Bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
        }

        public override long Length => Bytes.Length;

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

        public void WriteBytes(uint position, byte[] source) {
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