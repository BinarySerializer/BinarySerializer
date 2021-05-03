using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    public class MemoryMappedFile : BinaryFile 
    {
        public MemoryMappedFile(Context context, string filePath, uint baseAddress, Endian endianness = Endian.Little, long fileLength = 0) : base(context, filePath, endianness, baseAddress)
        {
            length = fileLength;
        }

		public override Reader CreateReader() {
			Stream s = FileManager.GetFileReadStream(AbsolutePath);
			length = s.Length;
			Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() {
			CreateBackupFile();
			Stream s = FileManager.GetFileWriteStream(AbsolutePath, RecreateOnWrite);
			length = s.Length;
			Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		private long length;
        public override long Length
        {
			get
            {
                if (length == 0)
                {
                    using Stream s = FileManager.GetFileReadStream(AbsolutePath);
                    length = s.Length;
                }
                return length;
            }
		}

		public virtual Pointer GetPointerInThisFileOnly(uint serializedValue, Pointer anchor = null) {
			uint anchorOffset = anchor?.AbsoluteOffset ?? 0;
			if (serializedValue + anchorOffset >= BaseAddress && serializedValue + anchorOffset < BaseAddress + Length) {
				return new Pointer(serializedValue, this, anchor: anchor);
			}
			return null;
		}

		public override Pointer GetPointer(uint serializedValue, Pointer anchor = null) {
			//Pointer ptr = GetPointerInThisFileOnly(serializedValue, anchor: anchor);
			//if (ptr != null) return ptr;
			List<MemoryMappedFile> files = Context.MemoryMap.Files.OfType<MemoryMappedFile>().ToList<MemoryMappedFile>();
			files.Sort((a, b) => b.BaseAddress.CompareTo(a.BaseAddress));
            return files.Select(f => f.GetPointerInThisFileOnly(serializedValue, anchor: anchor)).FirstOrDefault(p => p != null);
        }
    }
}
