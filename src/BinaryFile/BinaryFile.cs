using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    public abstract class BinaryFile : IDisposable
    {
        #region Constructor

        protected BinaryFile(Context context)
        {
            Context = context;
            PredefinedPointers = new Dictionary<uint, Pointer>();
        }

        #endregion

        #region Abstraction

        protected IFileManager FileManager => Context.FileManager;

        #endregion

        #region Public Properties

        /// <summary>
        /// The context the file is included in
        /// </summary>
        public Context Context { get; }

        public Endian Endianness { get; set; } = Endian.Little;

        /// <summary>
        /// Indicates if the file should be recreated when writing to it
        /// </summary>
        public bool RecreateOnWrite { get; set; } = true;

        /// <summary>
        /// Files can be identified with an alias besides <see cref="FilePath"/>
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The file path relative to the main directory in the context
        /// </summary>
        public string FilePath { get; set; }
        public string AbsolutePath => Context.BasePath + FilePath;

        public virtual long BaseAddress => 0;
        public abstract Pointer StartPointer { get; }
        public virtual bool SavePointersToMemoryMap => true;
        public virtual bool IgnoreCacheOnRead => false;

        #endregion

        #region Protected Properties

        protected Dictionary<uint, Pointer> PredefinedPointers { get; }
        protected SortedList<long, Region> Regions { get; set; }

        #endregion

        #region Methods

        public abstract Reader CreateReader();
        public abstract Writer CreateWriter();

        public virtual Pointer GetPointer(uint serializedValue, Pointer anchor = null) => new Pointer(serializedValue, this, anchor: anchor);
        public virtual bool AllowInvalidPointer(uint serializedValue, Pointer anchor = null) => false;

        public virtual Pointer GetPreDefinedPointer(uint offset) => PredefinedPointers.ContainsKey(offset) ? PredefinedPointers[offset] : null;

        public virtual void EndRead(Reader reader)
        {
            reader?.Dispose();
        }

        public virtual void EndWrite(Writer writer)
        {
            writer?.Flush();
            writer?.Dispose();
        }

        protected void CreateBackupFile()
        {
            if (Context.CreateBackupOnWrite && !FileManager.FileExists(AbsolutePath + ".BAK") && FileManager.FileExists(AbsolutePath))
            {
                using (Stream s = FileManager.GetFileReadStream(AbsolutePath))
                {
                    using (Stream sb = FileManager.GetFileWriteStream(AbsolutePath + ".BAK"))
                    {
                        s.CopyTo(sb);
                    }
                }
            }
        }

        public virtual void Dispose() { }

        #endregion

        #region File Map

        public virtual bool[] FileReadMap { get; protected set; }
        protected bool ShouldInitFileReadMap { get; set; }

        public void InitFileReadMap() => ShouldInitFileReadMap = true;
        public void InitFileReadMap(long length, bool forceInit = false)
        {
            if (forceInit || ShouldInitFileReadMap)
            {
                ShouldInitFileReadMap = false;
                FileReadMap = new bool[length];
            }
        }

        public void UpdateReadMap(long offset, long length)
        {
            if (FileReadMap == null)
                return;

            for (int i = 0; i < length; i++)
                FileReadMap[offset + i] = true;
        }
        public void ExportFileReadMap(string outputFilePath)
        {
            File.WriteAllBytes(outputFilePath, FileReadMap.Select(x => (byte)(x ? 0xFF : 0x00)).ToArray());
        }

        #endregion

        #region Region

        public void AddRegion(long offset, long length, string name)
        {
            if (Regions == null)
                Regions = new SortedList<long, Region>();

            Regions.Add(offset, new Region(offset, length, name));
        }

        public Region GetRegion(long offset)
        {
            if (Regions == null) 
                return null;

            // Binary search
            int lower = 0;
            int upper = Regions.Count - 1;
            var keys = Regions.Keys;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                var val = Regions[keys[middle]];

                if (offset < val.Offset)
                    upper = middle - 1;
                else if (offset >= val.Offset && offset < val.Offset + val.Length)
                    return val;
                else
                    lower = middle + 1;
            }

            return null;
        }

        public class Region
        {
            public Region(long offset, long length, string name)
            {
                Offset = offset;
                Length = length;
                Name = name;
            }

            public long Offset { get; }
            public long Length { get; }
            public string Name { get; }
        }

        #endregion
    }
}
