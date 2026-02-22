namespace BinarySerializer
{
    public class Checksum8Processor : ChecksumProcessor
    {
        public Checksum8Processor(bool invertBits = false)
        {
            InvertBits = invertBits;
        }

        private byte _checksumValue;

        /// <summary>
        /// Indicates if the checksum value bits should be inverted
        /// </summary>
        public bool InvertBits { get; }

        public override long CalculatedValue
        {
            get => (byte)(InvertBits ? ~_checksumValue : _checksumValue);
            set => _checksumValue = (byte)(InvertBits ? ~value : value);
        }

        public override void ProcessBytes(byte[] buffer, int offset, int count)
        {
            int end = offset + count;
            for (int i = offset; i < end; i++)
                _checksumValue = (byte)(_checksumValue + buffer[i]);
        }
    }
}