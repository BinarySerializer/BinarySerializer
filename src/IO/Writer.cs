using System;
using System.IO;
using System.Text;

namespace BinarySerializer 
{
    public class Writer : BinaryWriter 
    {
        #region Constructors

        public Writer(Stream stream, bool isLittleEndian = true, bool leaveOpen = false) : base(stream, new UTF8Encoding(), leaveOpen)
        {
            IsLittleEndian = isLittleEndian;
        }

        #endregion

        #region Public Properties

        public bool IsLittleEndian { get; set; }

        #endregion

        #region Protected Properties

        protected IXORCalculator XORCalculator { get; set; }
        protected IChecksumCalculator ChecksumCalculator { get; set; }
        protected uint BytesSinceAlignStart { get; set; }
        protected bool AutoAlignOn { get; set; }

        #endregion

        #region Write Methods

        public override void Write(int value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(short value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(uint value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(ushort value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(long value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(ulong value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public void Write(UInt24 value)
        {
            uint v = (uint)value;
            if (IsLittleEndian)
            {
                Write((byte)(v & 0xFF));
                Write((byte)((v >> 8) & 0xFF));
                Write((byte)((v >> 16) & 0xFF));
            }
            else
            {
                Write((byte)((v >> 16) & 0xFF));
                Write((byte)((v >> 8) & 0xFF));
                Write((byte)(v & 0xFF));
            }
        }

        public override void Write(float value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public override void Write(double value)
        {
            var data = BitConverter.GetBytes(value);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            Write(data);
        }

        public void WriteNullDelimitedString(string value, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            byte[] data = encoding.GetBytes(value + '\0');
            Write(data);
        }

        public void WriteString(string value, long size, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            byte[] data = encoding.GetBytes(value + '\0');
            if (data.Length != size)
                Array.Resize(ref data, (int)size);
            Write(data);
        }

        public override void Write(byte[] buffer)
        {
            if (buffer == null)
                return;

            var data = buffer;

            if (ChecksumCalculator?.CalculateForDecryptedData == true)
                ChecksumCalculator?.AddBytes(data);

            if (XORCalculator != null)
            {
                // Avoid changing data in array, so create a copy
                data = new byte[buffer.Length];
                Array.Copy(buffer, 0, data, 0, buffer.Length);

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] = XORCalculator.XORByte(data[i]);
                }
            }

            if (ChecksumCalculator?.CalculateForDecryptedData == false)
                ChecksumCalculator?.AddBytes(data);

            base.Write(data);

            if (AutoAlignOn)
                BytesSinceAlignStart += (uint)data.Length;
        }

        public override void Write(byte value)
        {
            if (ChecksumCalculator?.CalculateForDecryptedData == true)
                ChecksumCalculator?.AddByte(value);

            if (XORCalculator != null)
                value = XORCalculator.XORByte(value);

            if (ChecksumCalculator?.CalculateForDecryptedData == false)
                ChecksumCalculator?.AddByte(value);

            base.Write(value);

            if (AutoAlignOn)
                BytesSinceAlignStart++;
        }

        public override void Write(sbyte value) => Write((byte)value);

        #endregion

        #region Alignment

        // To make sure position is a multiple of alignBytes
        public void Align(int alignBytes) 
        {
            if (BaseStream.Position % alignBytes != 0) {
                int length = alignBytes - (int)(BaseStream.Position % alignBytes);
                byte[] data = new byte[length];
                Write(data);
            }
        }
        public void AlignOffset(int alignBytes, int offset) 
        {
            if ((BaseStream.Position - offset) % alignBytes != 0) {
                int length = alignBytes - (int)((BaseStream.Position - offset) % alignBytes);
                byte[] data = new byte[length];
                Write(data);
            }
        }

        // To make sure position is a multiple of alignBytes after reading a block of blocksize, regardless of prior position
        public void Align(int blockSize, int alignBytes) 
        {
            int rest = blockSize % alignBytes;
            if (rest > 0) {
                int length = alignBytes - rest;
                byte[] data = new byte[length];
                Write(data);
            }
        }

        public void AutoAlign(int alignBytes) 
        {
            if (BytesSinceAlignStart % alignBytes != 0) {
                int length = alignBytes - (int)(BytesSinceAlignStart % alignBytes);
                byte[] data = new byte[length];
                Write(data);
            }
            BytesSinceAlignStart = 0;
        }

        #endregion

        #region XOR & Checksum

        public void BeginXOR(IXORCalculator xorCalculator) => XORCalculator = xorCalculator;
        public void EndXOR() => XORCalculator = null;
        public IXORCalculator GetXORCalculator() => XORCalculator;
        
        public void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => ChecksumCalculator = checksumCalculator;
        public T EndCalculateChecksum<T>() 
        {
            IChecksumCalculator c = ChecksumCalculator;
            ChecksumCalculator = null;
            return ((IChecksumCalculator<T>)c).ChecksumValue;
        }
        
        #endregion
    }
}