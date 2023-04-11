#nullable enable
using System;

namespace BinarySerializer
{
    // TODO: The value size system is a hacky workaround for when the checksum isn't calculated per byte. Ideally we'd
    //       have a better system in place where the checksum calculator has access to the entire byte array to be calculated.

    /// <summary>
    /// Checksum calculator for a 32-bit checksum
    /// </summary>
    public class Checksum32Calculator : IChecksumCalculator<int>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="calculateForDecryptedData">Indicates if the checksum should be calculated for the decrypted data. This is ignored if the data is not encrypted.</param>
        /// <param name="invertBits">Indicates if the checksum value bits should be inverted</param>
        /// <param name="initialChecksumValue">The initial checksum value</param>
        /// <param name="valueSize">The size of a value to calculate, in bytes</param>
        public Checksum32Calculator(bool calculateForDecryptedData = true, bool invertBits = false, ushort initialChecksumValue = 0, int valueSize = 1)
        {
            InvertBits = invertBits;
            CalculateForDecryptedData = calculateForDecryptedData;
            _checksumValue = initialChecksumValue;
            ValueSize = valueSize;
        }

        private int _checksumValue;
        private int _valueIndex;

        /// <summary>
        /// Indicates if the checksum should be calculated for the decrypted data. This is ignored if the data is not encrypted.
        /// </summary>
        public bool CalculateForDecryptedData { get; }

        /// <summary>
        /// Indicates if the checksum value bits should be inverted
        /// </summary>
        public bool InvertBits { get; }

        /// <summary>
        /// The size of a value to calculate, in bytes
        /// </summary>
        public int ValueSize { get; }

        /// <summary>
        /// Adds a byte to the checksum
        /// </summary>
        /// <param name="b">The byte to add</param>
        public void AddByte(byte b)
        {
            _checksumValue += b << ((_valueIndex % ValueSize) * 8);
            _valueIndex++;
        }

        /// <summary>
        /// Adds an array of bytes to the checksum
        /// </summary>
        /// <param name="bytes">The bytes to add</param>
        /// <param name="offset">The offset in the array to start reading from</param>
        /// <param name="count">The amount of bytes to read from the array</param>
        public void AddBytes(byte[] bytes, int offset, int count)
        {
            if (bytes == null) 
                throw new ArgumentNullException(nameof(bytes));
            
            for (int i = 0; i < count; i++)
                AddByte(bytes[offset + i]);
        }

        /// <summary>
        /// The current checksum value
        /// </summary>
        public int ChecksumValue => InvertBits ? ~_checksumValue : _checksumValue;
    }
}