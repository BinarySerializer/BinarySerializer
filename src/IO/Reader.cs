using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BinarySerializer
{
    public class Reader : BinaryReader 
    {
        #region Constructors

        public Reader(Stream stream, bool isLittleEndian = true, bool leaveOpen = false) : base(stream, new UTF8Encoding(), leaveOpen)
        {
            IsLittleEndian = isLittleEndian;
        }

        #endregion

        #region Public Properties

        public bool IsLittleEndian { get; set; }

        #endregion

        #region Protected Properties

        protected uint BytesSinceAlignStart { get; set; }
        protected bool AutoAlignOn { get; set; }
        protected IXORCalculator XORCalculator { get; set; }
        protected IChecksumCalculator ChecksumCalculator { get; set; }

        #endregion

        #region Read Methods

        public override int ReadInt32()
        {
            var data = ReadBytes(4);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt32(data, 0);
        }

        public override float ReadSingle()
        {
            var data = ReadBytes(4);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToSingle(data, 0);
        }

        public override short ReadInt16()
        {
            var data = ReadBytes(2);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt16(data, 0);
        }

        public override ushort ReadUInt16()
        {
            var data = ReadBytes(2);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt16(data, 0);
        }

        public override long ReadInt64()
        {
            var data = ReadBytes(8);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToInt64(data, 0);
        }

        public override uint ReadUInt32()
        {
            var data = ReadBytes(4);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt32(data, 0);
        }

        public override ulong ReadUInt64()
        {
            var data = ReadBytes(8);
            if (IsLittleEndian != BitConverter.IsLittleEndian) Array.Reverse(data);
            return BitConverter.ToUInt64(data, 0);
        }

        public UInt24 ReadUInt24()
        {
            var b1 = ReadByte();
            var b2 = ReadByte();
            var b3 = ReadByte();
            if (IsLittleEndian)
            {
                return
                    (UInt24)((((uint)b3) << 16) |
                    (((uint)b2) << 8) |
                    ((uint)b1));
            }
            else
            {
                return
                    (UInt24)((((uint)b1) << 16) |
                    (((uint)b2) << 8) |
                    ((uint)b3));
            }
        }

        public override byte[] ReadBytes(int count)
        {
            byte[] bytes = base.ReadBytes(count);

            if (AutoAlignOn)
                BytesSinceAlignStart += (uint)bytes.Length;

            if (ChecksumCalculator?.CalculateForDecryptedData == false)
                ChecksumCalculator?.AddBytes(bytes);

            if (XORCalculator != null)
            {
                for (int i = 0; i < count; i++)
                {
                    bytes[i] = XORCalculator.XORByte(bytes[i]);
                }
            }

            if (ChecksumCalculator?.CalculateForDecryptedData == true)
                ChecksumCalculator?.AddBytes(bytes);

            return bytes;
        }

        public override sbyte ReadSByte() => (sbyte)ReadByte();

        public override byte ReadByte()
        {
            byte result = base.ReadByte();

            if (AutoAlignOn)
                BytesSinceAlignStart++;

            if (ChecksumCalculator?.CalculateForDecryptedData == false)
                ChecksumCalculator?.AddByte(result);

            if (XORCalculator != null)
                result = XORCalculator.XORByte(result);

            if (ChecksumCalculator?.CalculateForDecryptedData == true)
                ChecksumCalculator?.AddByte(result);

            return result;
        }

        public string ReadNullDelimitedString(Encoding encoding)
        {
            List<byte> bytes = new List<byte>();
            byte b = ReadByte();

            while (b != 0x0)
            {
                bytes.Add(b);
                b = ReadByte();
            }

            if (bytes.Count > 0)
            {
                if (encoding == null) 
                    throw new ArgumentNullException(nameof(encoding));
                return encoding.GetString(bytes.ToArray());
            }

            return "";
        }

        public string ReadString(long size, Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException(nameof(encoding));

            byte[] bytes = ReadBytes((int)size);
            int firstIndexOf = Array.IndexOf<byte>(bytes, (byte)0x0);
            if (firstIndexOf >= 0 && firstIndexOf < bytes.Length)
            {
                if (firstIndexOf == 0) return "";
                Array.Resize<byte>(ref bytes, firstIndexOf);
            }

            return encoding.GetString(bytes);
        }

        #endregion

        #region Alignment

        public bool AutoAligning
        {
            get => AutoAlignOn;
            set
            {
                AutoAlignOn = value;
                BytesSinceAlignStart = 0;
            }
        }

        // To make sure position is a multiple of alignBytes
        public void Align(int alignBytes) 
        {
            if (BaseStream.Position % alignBytes != 0)
                ReadBytes(alignBytes - (int)(BaseStream.Position % alignBytes));
        }
        public void AlignOffset(int alignBytes, int offset) {
            if ((BaseStream.Position - offset) % alignBytes != 0)
                ReadBytes(alignBytes - (int)((BaseStream.Position - offset) % alignBytes));
        }

        // To make sure position is a multiple of alignBytes after reading a block of blocksize, regardless of prior position
        public void Align(int blockSize, int alignBytes) {
            int rest = blockSize % alignBytes;
            if (rest > 0)
            {
                byte[] aligned = ReadBytes(alignBytes - rest);

                if (aligned.Any(b => b != 0x0))
                    throw new Exception("A data byte was skipped during alignment");
            }
        }
        
        public void AutoAlign(int alignBytes) 
        {
            if (BytesSinceAlignStart % alignBytes != 0)
                ReadBytes(alignBytes - (int)(BytesSinceAlignStart % alignBytes));

            BytesSinceAlignStart = 0;
        }

        #endregion

        #region XOR & Checksum

        public void BeginXOR(IXORCalculator xorCalculator) => XORCalculator = xorCalculator;
        public void EndXOR() => XORCalculator = null;
        public IXORCalculator GetXORCalculator() => XORCalculator;
        public void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => ChecksumCalculator = checksumCalculator;
        public T EndCalculateChecksum<T>() {
            IChecksumCalculator c = ChecksumCalculator;
            ChecksumCalculator = null;
            return ((IChecksumCalculator<T>)c).ChecksumValue;
        }

        #endregion
    }
}