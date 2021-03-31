using System.IO;

namespace BinarySerializer
{
    public class LinearSerializedFile : BinaryFile 
    {
        public LinearSerializedFile(Context context) : base(context) { }

        public uint Length { get; set; }

        public override Pointer StartPointer => new Pointer((uint)BaseAddress, this);

		public override Reader CreateReader() 
        {
			Stream s = FileManager.GetFileReadStream(AbsolutePath);
			Length = (uint)s.Length;
			Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
			return reader;
		}

		public override Writer CreateWriter() 
        {
			CreateBackupFile();
			Stream s = FileManager.GetFileWriteStream(AbsolutePath, RecreateOnWrite);
			Length = (uint)s.Length;
			Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
			return writer;
		}

		public override Pointer GetPointer(uint serializedValue, Pointer anchor = null) 
        {
			if (Length == 0) 
            {
				Stream s = FileManager.GetFileReadStream(AbsolutePath);
				Length = (uint)s.Length;
				s.Close();
			}

			uint anchorOffset = anchor?.AbsoluteOffset ?? 0;

			if (serializedValue + anchorOffset >= BaseAddress && serializedValue + anchorOffset <= BaseAddress + Length) 
				return new Pointer(serializedValue, this, anchor: anchor);

			return null;
		}
	}
}