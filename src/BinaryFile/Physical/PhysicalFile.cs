using System.IO;

namespace BinarySerializer
{
    /// <summary>
    /// A <see cref="BinaryFile"/> which uses a physical file
    /// </summary>
    public abstract class PhysicalFile : BinaryFile
    {
        protected PhysicalFile(Context context, string filePath, Endian endianness = Endian.Little, long baseAddress = 0, Pointer startPointer = null, long fileLength = 0, long memoryMappedPriority = -1) : base(context, filePath, endianness, baseAddress, startPointer, memoryMappedPriority)
        {
            DestinationPath = context.GetAbsoluteFilePath(filePath);
            length = fileLength;
            RecreateOnWrite = true;
        }

        /// <summary>
        /// Indicates if the file should be recreated when writing to it
        /// </summary>
        public bool RecreateOnWrite { get; set; }

        /// <summary>
        /// The source file path for reading
        /// </summary>
        public string SourcePath => AbsolutePath;
        
        /// <summary>
        /// The destination file path for writing
        /// </summary>
        public string DestinationPath { get; set; }

        private long? length;
        public override long Length
        {
            get
            {
                if (length == null)
                {
                    using Stream s = FileManager.GetFileReadStream(AbsolutePath);
                    length = s.Length;
                }

                return length.Value;
            }
        }

        protected void CreateBackupFile()
        {
            var backupPath = AbsolutePath + ".BAK";

            if (Context.CreateBackupOnWrite && !FileManager.FileExists(backupPath) && FileManager.FileExists(AbsolutePath))
            {
                using Stream s = FileManager.GetFileReadStream(AbsolutePath);
                using Stream sb = FileManager.GetFileWriteStream(backupPath);
                s.CopyTo(sb);
            }
        }

        public override Reader CreateReader()
        {
            Stream s = FileManager.GetFileReadStream(SourcePath);
            length = s.Length;
            Reader reader = new Reader(s, isLittleEndian: Endianness == Endian.Little);
            return reader;
        }

        public override Writer CreateWriter()
        {
            CreateBackupFile();
            Stream s = FileManager.GetFileWriteStream(DestinationPath, RecreateOnWrite);
            length = s.Length;
            Writer writer = new Writer(s, isLittleEndian: Endianness == Endian.Little);
            return writer;
        }
    }
}