using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BinarySerializer
{
    public class Context : IDisposable
    {
        #region Constructors

        public Context(string basePath, Encoding defaultEncoding = null, ISerializerLog serializerLog = null, IFileManager fileManager = null, ILogger logger = null)
        {
            // Set properties from parameters
            FileManager = fileManager ?? new DefaultFileManager();
            Logger = logger ?? new DefaultLogger();
            BasePath = FileManager.NormalizePath(basePath, true);
            DefaultEncoding = defaultEncoding ?? Encoding.ASCII;
            Log = serializerLog ?? new DefaultSerializerLog();

            // Initialize properties
            MemoryMap = new MemoryMap();
            Cache = new SerializableCache(Logger);
            ObjectStorage = new Dictionary<string, object>();
        }

        #endregion

        #region Abstraction

        public IFileManager FileManager { get; }
        public ILogger Logger { get; }

        #endregion

        #region Public Properties

        public string BasePath { get; }
        public MemoryMap MemoryMap { get; }
        public SerializableCache Cache { get; }
        public ISerializerLog Log { get; }
        public virtual bool CreateBackupOnWrite => false;
        public Encoding DefaultEncoding { get; }

        #endregion

        #region Storage

        protected Dictionary<string, object> ObjectStorage { get; }

        public T GetStoredObject<T>(string id)
        {
            if (ObjectStorage.ContainsKey(id)) 
                return (T)ObjectStorage[id];

            return default;
        }

        public T StoreObject<T>(string id, T obj)
        {
            ObjectStorage[id] = obj;
            return obj;
        }

        #endregion

        #region Files

        public Stream GetFileStream(string relativePath)
        {
            Stream str = FileManager.GetFileReadStream(BasePath + FileManager.NormalizePath(relativePath, false));
            return str;
        }
        public BinaryFile GetFile(string relativePath)
        {
            string path = FileManager.NormalizePath(relativePath, false);
            return MemoryMap.Files.FirstOrDefault<BinaryFile>(f => f.FilePath?.ToLower() == path?.ToLower() || f.Alias?.ToLower() == relativePath?.ToLower());
        }

        public void AddFile(BinaryFile file)
        {
            MemoryMap.Files.Add(file);
        }
        public void RemoveFile(BinaryFile file)
        {
            MemoryMap.Files.Remove(file);
            deserializer?.DisposeFile(file);
            serializer?.DisposeFile(file);
            file?.Dispose();
        }

        public Pointer<T> FilePointer<T>(string relativePath) where T : BinarySerializable, new()
        {
            Pointer p = FilePointer(relativePath);

            if (p == null) 
                return null;

            return new Pointer<T>(p);
        }
        public Pointer FilePointer(string relativePath)
        {
            BinaryFile f = GetFile(relativePath);

            if (f == null)
                throw new Exception($"File with path {relativePath} is not loaded in this Context!");

            return f.StartPointer;
        }
        public bool FileExists(string relativePath)
        {
            BinaryFile f = GetFile(relativePath);
            return f != null;
        }

        public T GetMainFileObject<T>(string relativePath) where T : BinarySerializable
        {
            return GetMainFileObject<T>(GetFile(relativePath));
        }
        public T GetMainFileObject<T>(BinaryFile file) where T : BinarySerializable
        {
            if (file == null)
                return default;

            Pointer ptr = file.StartPointer;
            return Cache.FromOffset<T>(ptr);
        }

        #endregion

        #region Serializers

        private BinaryDeserializer deserializer;
        public BinaryDeserializer Deserializer
        {
            get
            {
                if (deserializer == null)
                {
                    if (serializer != null)
                    {
                        serializer.Dispose();
                        serializer = null;
                    }
                    deserializer = new BinaryDeserializer(this);
                }
                return deserializer;
            }
        }

        private BinarySerializer serializer;
        public BinarySerializer Serializer
        {
            get
            {
                if (serializer == null)
                {
                    if (deserializer != null)
                    {
                        deserializer.Dispose();
                        deserializer = null;
                    }
                    serializer = new BinarySerializer(this);
                }
                return serializer;
            }
        }

        #endregion

        #region Dispose

        public void Close()
        {
            deserializer?.Dispose();
            deserializer = null;
            serializer?.Dispose();
            serializer = null;

            foreach (var file in MemoryMap.Files)
                file?.Dispose();

            Log.Dispose();
        }
        public void Dispose() => Close();

        #endregion
    }
}