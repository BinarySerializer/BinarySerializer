﻿#nullable enable
using System;

namespace BinarySerializer
{
    /// <summary>
    /// Checksum calculator for an 8-bit checksum
    /// </summary>
    public class Checksum8Calculator : IChecksumCalculator<byte>
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="calculateForDecryptedData">Indicates if the checksum should be calculated for the decrypted data. This is ignored if the data is not encrypted.</param>
        public Checksum8Calculator(bool calculateForDecryptedData = true)
        {
            CalculateForDecryptedData = calculateForDecryptedData;
        }

        /// <summary>
        /// Indicates if the checksum should be calculated for the decrypted data. This is ignored if the data is not encrypted.
        /// </summary>
        public bool CalculateForDecryptedData { get; }

        /// <summary>
        /// Adds a byte to the checksum
        /// </summary>
        /// <param name="b">The byte to add</param>
        public void AddByte(byte b)
        {
            ChecksumValue = (byte)((ChecksumValue + b) % 256);
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
        public byte ChecksumValue { get; set; }
    }
}