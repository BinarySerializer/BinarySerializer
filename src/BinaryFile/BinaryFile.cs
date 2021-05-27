using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BinarySerializer
{
    public abstract class BinaryFile : IDisposable
    {
        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="context">The context the file belongs to</param>
        /// <param name="filePath">The file path relative to the main directory in the context</param>
        /// <param name="endianness">The endianness to use when serializing the file</param>
        /// <param name="baseAddress">The base address for the file. If the file is not memory mapped this should be 0.</param>
        /// <param name="startPointer">The start pointer for the file. If null it will be the same as <see cref="BaseAddress"/></param>
        protected BinaryFile(Context context, string filePath, Endian endianness = Endian.Little, long baseAddress = 0, Pointer startPointer = null)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            AbsolutePath = Context.GetAbsoluteFilePath(filePath);
            Endianness = endianness;
            RecreateOnWrite = true;
            BaseAddress = baseAddress;
            StartPointer = startPointer ?? new Pointer(baseAddress, this);
        }

        #endregion

        #region Abstraction

        protected IFileManager FileManager => Context.FileManager;

        #endregion

        #region Public Properties

        /// <summary>
        /// The context the file belongs to
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// The endianness to use when serializing the file
        /// </summary>
        public Endian Endianness { get; }

        /// <summary>
        /// Indicates if the file should be recreated when writing to it
        /// </summary>
        public bool RecreateOnWrite { get; set; }

        /// <summary>
        /// Files can be identified with an alias besides <see cref="FilePath"/>
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The file path relative to the main directory in the context
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// The absolute path to the file
        /// </summary>
        public string AbsolutePath { get; }

        /// <summary>
        /// The base address for the file. If the file is not memory mapped this should be 0.
        /// </summary>
        public long BaseAddress { get; }

        /// <summary>
        /// The length of the file
        /// </summary>
        public abstract long Length { get; }

        /// <summary>
        /// The start pointer for the file
        /// </summary>
        public Pointer StartPointer { get; }

        /// <summary>
        /// Indicates if pointers leading to this file should be saved to the Memory Map
        /// </summary>
        public virtual bool SavePointersToMemoryMap => true;

        /// <summary>
        /// Indicates if objects read from this file should not be cached
        /// </summary>
        public virtual bool IgnoreCacheOnRead => false;

        private PointerSize? _pointerSize;
        public virtual PointerSize PointerSize
        {
            get
            {
                if (_pointerSize == null)
                {
                    if (BaseAddress + Length > UInt32.MaxValue)
                        _pointerSize = PointerSize.Pointer64;
                    else
                        _pointerSize = PointerSize.Pointer32;
                }

                return _pointerSize.Value;
            }
        }

        #endregion

        #region Methods

        public abstract Reader CreateReader();
        public abstract Writer CreateWriter();

        /// <summary>
        /// Retrieves the <see cref="BinaryFile"/> for a serialized <see cref="Pointer"/> value
        /// </summary>
        /// <param name="serializedValue">The serialized pointer value</param>
        /// <param name="anchor">An optional anchor for the pointer</param>
        /// <returns></returns>
        public virtual BinaryFile GetPointerFile(long serializedValue, Pointer anchor = null) => this;

        protected virtual BinaryFile GetLocalPointerFile(long serializedValue, Pointer anchor = null)
        {
            var anchorOffset = anchor?.AbsoluteOffset ?? 0;

            if (serializedValue + anchorOffset >= BaseAddress && serializedValue + anchorOffset <= BaseAddress + Length)
                return this;

            return null;
        }

        protected virtual BinaryFile GetMemoryMappedPointerFile(long serializedValue, Pointer anchor = null)
        {
            // Get all memory mapped files
            List<MemoryMappedFile> files = Context.MemoryMap.Files.OfType<MemoryMappedFile>().ToList();
            
            // Sort based on the base address
            files.Sort((a, b) => b.BaseAddress.CompareTo(a.BaseAddress));

            // Return the first pointer within the range
            return files.Select(f => f.GetLocalPointerFile(serializedValue, anchor)).FirstOrDefault(p => p != null);
        }

        public virtual bool AllowInvalidPointer(long serializedValue, Pointer anchor = null) => false;

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
            var backupPath = AbsolutePath + ".BAK";

            if (Context.CreateBackupOnWrite && !FileManager.FileExists(backupPath) && FileManager.FileExists(AbsolutePath))
            {
                using Stream s = FileManager.GetFileReadStream(AbsolutePath);
                using Stream sb = FileManager.GetFileWriteStream(backupPath);
                s.CopyTo(sb);
            }
        }

        public virtual void Dispose() { }

        #endregion

        #region Override Pointers

        protected Dictionary<long, Pointer> OverridePointers { get; set; }

        public virtual void AddOverridePointer(long offset, Pointer pointer)
        {
            if (OverridePointers == null)
                OverridePointers = new Dictionary<long, Pointer>();

            OverridePointers.Add(offset, pointer);
        }
        public virtual Pointer GetOverridePointer(long offset) => OverridePointers?.ContainsKey(offset) == true ? OverridePointers[offset] : null;

        #endregion

        #region File Map

        public virtual bool[] FileReadMap { get; protected set; }
        public bool ShouldUpdateReadMap => FileReadMap != null;
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
            if (!ShouldUpdateReadMap)
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

        protected SortedList<long, Region> Regions { get; set; }

        public void AddRegion(long offset, long length, string name)
        {
            if (Regions == null)
                Regions = new SortedList<long, Region>();
            Regions[offset] = new Region(offset, length, name);
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

        #region Labels

        protected Dictionary<long, string> Labels { get; set; }

        public void AddLabel(long offset, string label)
        {
            Labels ??= new Dictionary<long, string>();

            Labels[offset] = label;
        }

        public string GetLabel(long offset)
        {
            if (Labels?.ContainsKey(offset) != true)
                return null;

            return Labels[offset];
        }

        #endregion
    }
}