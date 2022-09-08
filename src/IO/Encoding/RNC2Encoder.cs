﻿#nullable enable
using System;
using System.IO;
using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// Compresses/decompresses data with the RNC2 algorithm
    /// </summary>
    public class RNC2Encoder : IStreamEncoder 
    {
        public RNC2Encoder(bool hasHeader = true)
        {
            HasHeader = hasHeader;
        }

        public string Name => "RNC2";
        public bool HasHeader { get; }

        public void DecodeStream(Stream input, Stream output)
        {
            if (input == null) 
                throw new ArgumentNullException(nameof(input));
            if (output == null) 
                throw new ArgumentNullException(nameof(output));

            using Reader reader = new(input, isLittleEndian: false, leaveOpen: true);

            byte[] decompressed;

            if (HasHeader)
            {
                string header = reader.ReadString(3, new ASCIIEncoding());

                if (header != "RNC")
                    throw new Exception("Data is not compressed with RNC!");

                byte method = reader.ReadByte();

                if (method != 2)
                    throw new Exception("Data is not compressed with RNC method 2!");

                uint decompressedSize = reader.ReadUInt32();
                uint compressedSize = reader.ReadUInt32();
                ushort decompressedCRC = reader.ReadUInt16();
                ushort compressedCRC = reader.ReadUInt16();
                byte leeway = reader.ReadByte();
                byte packChunks = reader.ReadByte();

                byte[] compressedData = reader.ReadBytes((int)compressedSize);

                decompressed = DecompressRNC2(compressedData, decompressedSize, leeway, packChunks);
            }
            else
            {
                decompressed = DecompressRNC2(reader);
            }

            output.Write(decompressed, 0, decompressed.Length);
        }

        public void EncodeStream(Stream input, Stream output) => throw new NotImplementedException();

#nullable disable

        // Huffman decoding
        private enum Command 
        {
            LIT=0,  // 0, Literal
            MOV,    // 10, Move bytes + length + distance, Get bytes if length=9 + 4bits
            MV2,    // 110, Move 2 bytes
            MV3,    // 1110, Move 3 bytes
            CND     // 1111, Conditional copy, or EOF
        };
        public class HuffmanCode<T> 
        {
            public int Length;
            public int Code;
            public T Value;
            public HuffmanCode(int length, int code, T value) 
            {
                Length = length;
                Code = code;
                Value = value;
            }
        }
        public class HuffmanDecoder<T> 
        {
            private class Node 
            {
                public Node Node0;
                public Node Node1;
                public T Value;
                public T GetValue(Func<int> GetBit) 
                {
                    if (Node0 == null && Node1 == null) 
                        return Value;

                    int bit = GetBit();

                    if (bit == 1) 
                        return Node1.GetValue(GetBit);
                    else 
                        return Node0.GetValue(GetBit);
                }
            }

            HuffmanCode<T>[] codes;

            private readonly Node root = new Node();
            private void Insert(HuffmanCode<T> code) 
            {
                Node currentNode = root;
                for (int bitIndex = code.Length-1; bitIndex >= 0; bitIndex--) 
                {
                    int bit = BitHelpers.ExtractBits(code.Code, 1, bitIndex);

                    if (bit == 1)
                    {
                        currentNode.Node1 ??= new Node();
                        currentNode = currentNode.Node1;
                    } 
                    else
                    {
                        currentNode.Node0 ??= new Node();
                        currentNode = currentNode.Node0;
                    }
                }
                currentNode.Value = code.Value;
            }
            public HuffmanDecoder(HuffmanCode<T>[] codes) 
            {
                this.codes = codes;

                foreach (HuffmanCode<T> code in codes) 
                    Insert(code);
            }
            public T GetValue(Func<int> GetBit) => root.GetValue(GetBit);
        }

        readonly HuffmanDecoder<Command> cmdDecoder = new HuffmanDecoder<Command>(new HuffmanCode<Command>[] 
        {
            new HuffmanCode<Command>(1,0b0000,Command.LIT),
            new HuffmanCode<Command>(2,0b0010,Command.MOV),
            new HuffmanCode<Command>(3,0b0110,Command.MV2),
            new HuffmanCode<Command>(4,0b1110,Command.MV3),
            new HuffmanCode<Command>(4,0b1111,Command.CND)
        });

        readonly HuffmanDecoder<byte> lengthDecoder = new HuffmanDecoder<byte>(new HuffmanCode<byte>[] 
        {
            new HuffmanCode<byte>(2,0b000,4),
            new HuffmanCode<byte>(2,0b010,5),
            new HuffmanCode<byte>(3,0b010,6),
            new HuffmanCode<byte>(3,0b011,7),
            new HuffmanCode<byte>(3,0b110,8),
            new HuffmanCode<byte>(3,0b111,9)
        });

        readonly HuffmanDecoder<byte> distanceDecoder = new HuffmanDecoder<byte>(new HuffmanCode<byte>[] 
        {
            new HuffmanCode<byte>(1,0b000000,0),
            new HuffmanCode<byte>(3,0b000110,1),
            new HuffmanCode<byte>(4,0b001000,2),
            new HuffmanCode<byte>(4,0b001001,3),
            new HuffmanCode<byte>(5,0b010101,4),
            new HuffmanCode<byte>(5,0b010111,5),
            new HuffmanCode<byte>(5,0b011101,6),
            new HuffmanCode<byte>(5,0b011111,7),
            new HuffmanCode<byte>(6,0b101000,8),
            new HuffmanCode<byte>(6,0b101001,9),
            new HuffmanCode<byte>(6,0b101100,10),
            new HuffmanCode<byte>(6,0b101101,11),
            new HuffmanCode<byte>(6,0b111000,12),
            new HuffmanCode<byte>(6,0b111001,13),
            new HuffmanCode<byte>(6,0b111100,14),
            new HuffmanCode<byte>(6,0b111101,15)
        });

        private byte[] DecompressRNC2(byte[] data, uint decompressedSize, byte leeway, byte packChunks) 
        {
            int currentBit = 0;
            byte? currentBitByte = null;
            int currentByte = 0;
            int currentOutByte = 0;

            byte[] uncompressed = new byte[decompressedSize];
            Array.Copy(data, 0, uncompressed, 0, Math.Min(uncompressed.Length, data.Length));

            // Helpers
            void GetNewBitByte() 
            {
                currentBit = 0;
                currentBitByte = data[currentByte];
                currentByte++;
            }
            int ReadBit() 
            {
                if (!currentBitByte.HasValue || currentBit > 7) 
                    GetNewBitByte();

                // Bit numbering scheme MSB 0!
                int bit = BitHelpers.ExtractBits(currentBitByte.Value, 1, 7 - currentBit);
                currentBit++;
                return bit;
            }
            byte ReadByte() 
            {
                byte b = data[currentByte];
                currentByte++;
                return b;
            }
            int ReadDistance() 
            {
                int distMult = distanceDecoder.GetValue(ReadBit);
                int distByte = ReadByte();
                return (distByte | (distMult << 8)) + 1;
            }
            void MoveBytes(int distance, int count) 
            {
                if (count == 0) 
                    throw new Exception("Decompression error");

                for (int i = 0; i < count; i++) 
                {
                    uncompressed[currentOutByte] = uncompressed[currentOutByte - distance];
                    currentOutByte++;
                }
            };

            // Unused
            ReadBit();
            ReadBit();
            byte foundChunks = 0;
            bool done = false;
            while (!done && foundChunks < packChunks) 
            {
                Command cmd = cmdDecoder.GetValue(ReadBit);

                switch (cmd) 
                {
                    case Command.LIT: // Literal
                        uncompressed[currentOutByte] = ReadByte();
                        currentOutByte++;
                        break;

                    case Command.MOV: 
                    {
                        byte count = lengthDecoder.GetValue(ReadBit);
                        if (count != 9)
                            MoveBytes(ReadDistance(), count);
                        else {
                            uint rep = 0;
                            for (uint i = 0; i < 4; i++)
                                rep = (rep << 1) | (uint)ReadBit();
                            rep = (rep + 3) * 4;
                            for (uint i = 0; i < rep; i++) {
                                uncompressed[currentOutByte] = ReadByte();
                                currentOutByte++;
                            }
                        }
                    }
                    break;

                    case Command.MV2:
                        MoveBytes(ReadByte() + 1, 2);
                        break;

                    case Command.MV3:
                        MoveBytes(ReadDistance(), 3);
                        break;

                    case Command.CND: 
                    {
                        byte count = ReadByte();
                        if (count != 0)
                            MoveBytes(ReadDistance(), count + 8);
                        else {
                            foundChunks++;
                            done = ReadBit() == 0;
                        }

                    }
                    break;
                }
            }

            if (currentOutByte != uncompressed.Length) 
                throw new Exception("Current out byte did not reach output length");

            if (foundChunks != packChunks) 
                throw new Exception("Not all chunks were parsed");

            return uncompressed;
        }

        private byte[] DecompressRNC2(Reader reader) 
        {
            int currentBit = 0;
            byte? currentBitByte = null;
            int currentOutByte = 0;

            using MemoryStream ms = new MemoryStream();

            // Helpers
            void GetNewBitByte() 
            {
                currentBit = 0;
                currentBitByte = ReadByte();
            }
            int ReadBit() 
            {
                if (!currentBitByte.HasValue || currentBit > 7) {
                    GetNewBitByte();
                }
                // Bit numbering scheme MSB 0!
                int bit = BitHelpers.ExtractBits(currentBitByte.Value, 1, 7 - currentBit);
                currentBit++;
                return bit;
            }
            byte ReadByte() => reader.ReadByte();

            void WriteByte(byte b) 
            {
                ms.WriteByte(b);
                currentOutByte++;
            }
            byte GetDistanceByte(int distance) 
            {
                long pos = ms.Position;
                ms.Position = pos - distance;
                int b = ms.ReadByte();
                ms.Position = pos;
                if(b < 0) return 0;
                return (byte)b;
            }
            int ReadDistance() 
            {
                int distMult = distanceDecoder.GetValue(ReadBit);
                int distByte = ReadByte();
                return (distByte | (distMult << 8)) + 1;
            }
            void MoveBytes(int distance, int count) 
            {
                if (count == 0) throw new Exception("Decompression error");
                for (int i = 0; i < count; i++) {
                    //Controller.print(currentOutByte + " - " + distance + " - " + currentByte + " - " + currentBit);
                    WriteByte(GetDistanceByte(distance));
                }
            };

            // Unused
            ReadBit();
            ReadBit();

            byte foundChunks = 0;
            bool done = false;

            while (!done) 
            {
                Command cmd = cmdDecoder.GetValue(ReadBit);
                //Controller.print(cmd);
                switch (cmd) 
                {
                    case Command.LIT: // Literal
                        WriteByte(ReadByte());
                        break;

                    case Command.MOV: 
                    {
                        byte count = lengthDecoder.GetValue(ReadBit);
                        if (count != 9)
                            MoveBytes(ReadDistance(), count);
                        else {
                            uint rep = 0;
                            for (uint i = 0; i < 4; i++)
                                rep = (rep << 1) | (uint)ReadBit();
                            rep = (rep + 3) * 4;
                            for (uint i = 0; i < rep; i++) {
                                WriteByte(ReadByte());
                            }
                        }
                    }
                        break;

                    case Command.MV2:
                        MoveBytes(ReadByte() + 1, 2);
                        break;

                    case Command.MV3:
                        MoveBytes(ReadDistance(), 3);
                        break;

                    case Command.CND: 
                    {
                        byte count = ReadByte();
                            
                        if (count != 0)
                        {
                            MoveBytes(ReadDistance(), count + 8);
                        }
                        else 
                        {
                            foundChunks++;
                            done = ReadBit() == 0;
                        }
                    }
                        break;
                }
            }

            ms.Position = 0;

            return ms.ToArray();
        }
    }
}