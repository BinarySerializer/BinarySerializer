using System.IO;

namespace BinarySerializer
{
    public class LinearSerializedFile : BinaryFile 
    {
        public LinearSerializedFile(Context context, string filePath, Endian endianness = Endian.Little, long fileLength = 0) : base(context, filePath, endianness)
        {
            length = fileLength;
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

		public override Reader CreateReader() 
        {
			Stream s = FileManager.GetFileReadStream(AbsolutePath);
            length = s.Length;
			Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() 
        {
			CreateBackupFile();
			Stream s = FileManager.GetFileWriteStream(AbsolutePath, RecreateOnWrite);
            length = s.Length;
			Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		public override Pointer GetPointer(uint serializedValue, Pointer anchor = null) 
        {
            var anchorOffset = anchor?.AbsoluteOffset ?? 0;

			if (serializedValue + anchorOffset >= BaseAddress && serializedValue + anchorOffset <= BaseAddress + Length) 
				return new Pointer(serializedValue, this, anchor: anchor);

			return null;
		}
    }
}