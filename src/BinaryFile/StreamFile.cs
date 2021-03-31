using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    public class StreamFile : BinaryFile 
    {
        public StreamFile(string name, Stream stream, Context context) : base(context) 
        {
			FilePath = name;
			Stream = stream;
			Length = stream.Length;
		}

        public long Length { get; set; }
        public bool AllowLocalPointers { get; set; }
        private Stream Stream { get; }

        public override Pointer StartPointer => new Pointer((uint)BaseAddress, this);

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
	}
}