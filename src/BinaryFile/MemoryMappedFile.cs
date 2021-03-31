using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    public class MemoryMappedFile : BinaryFile 
    {
		public MemoryMappedFile(Context context, uint baseAddress) : base(context) 
        {
			BaseAddress = baseAddress;
		}

        public override long BaseAddress { get; }

        public override Pointer StartPointer => new Pointer((uint)BaseAddress, this);

		public override Reader CreateReader() {
			Stream s = FileManager.GetFileReadStream(AbsolutePath);
			length = (uint)s.Length;
			Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() {
			CreateBackupFile();
			Stream s = FileManager.GetFileWriteStream(AbsolutePath, RecreateOnWrite);
			length = (uint)s.Length;
			Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		private uint length = 0;
		public virtual uint Length {
			get {
				if (length == 0) {
					using (Stream s = FileManager.GetFileReadStream(AbsolutePath)) {
						length = (uint)s.Length;
					}
				}
				return length;
			}
			set => length = value;
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
