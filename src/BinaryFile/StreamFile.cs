using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    /// <summary>
    /// A <see cref="BinaryFile"/> used for a <see cref="Stream"/>. This type of file should only be used for limited operations, such as serializing an encoded file.
    /// </summary>
    public class StreamFile : BinaryFile 
    {
        public StreamFile(Context context, string name, Stream stream, Endian endianness = Endian.Little, bool allowLocalPointers = false) : base(context, name, endianness)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Length = stream.Length;
            AllowLocalPointers = allowLocalPointers;
        }

        private Stream _stream;

        public override long Length { get; }
        public bool AllowLocalPointers { get; }

        protected Stream Stream
        {
            get => _stream ?? throw new ObjectDisposedException(nameof(Stream));
            set => _stream = value;
        }

		public override Reader CreateReader() {
			Reader reader = new Reader(Stream, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() {
			Writer writer = new Writer(Stream, isLittleEndian: Endianness == Endian.Little);
			Stream.Position = 0;
			return writer;
		}

		public override Pointer GetPointer(uint serializedValue, Pointer anchor = null) 
        {
			// If we allow local pointers we assume the pointer leads to the stream file
			if (AllowLocalPointers)
            {
                uint anchorOffset = anchor?.AbsoluteOffset ?? 0;
				if (serializedValue + anchorOffset >= BaseAddress && serializedValue + anchorOffset <= BaseAddress + Length)
					return new Pointer(serializedValue, this, anchor: anchor);
				else
					return null;
            }
			else
            {
                // Get every memory mapped file
                List<MemoryMappedFile> files = Context.MemoryMap.Files.OfType<MemoryMappedFile>().ToList();

				files.Sort((a, b) => b.BaseAddress.CompareTo(a.BaseAddress));
                return files.Select(f => f.GetPointerInThisFileOnly(serializedValue, anchor: anchor)).FirstOrDefault(p => p != null);
            }
		}

        public override void Dispose()
        {
            // Dispose base file
            base.Dispose();

            // Dispose and remove the reference to the stream
            _stream?.Dispose();
            Stream = null;
        }
    }
}