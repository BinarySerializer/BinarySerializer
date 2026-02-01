namespace BinarySerializer
{
    public class BigEndianChecksum16Processor : ChecksumProcessor
    {
        public BigEndianChecksum16Processor(bool invertBits = false, int valueSize = 1)
        {
            InvertBits = invertBits;
            ValueSize = valueSize;
        }

        private ushort _checksumValue;
        private int _valueIndex;

        /// <summary>
        /// Indicates if the checksum value bits should be inverted
        /// </summary>
        public bool InvertBits { get; }

        /// <summary>
        /// The size of a value to calculate, in bytes
        /// </summary>
        public int ValueSize { get; }

        public override long CalculatedValue
        {
            get => (ushort)(InvertBits ? ~_checksumValue : _checksumValue);
            set => _checksumValue = (ushort)(InvertBits ? ~value : value);
        }

        public override void ProcessBytes(byte[] buffer, int offset, int count)
        {
            int end = offset + count;
            for (int i = offset; i < end; i++)
                _checksumValue = (ushort)(_checksumValue + (buffer[i] << ((ValueSize - (_valueIndex++ % ValueSize) - 1) * 8)));
        }
    }
}