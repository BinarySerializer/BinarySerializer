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

        public Context(string basePath, ISerializerSettings settings = null, ISerializerLog serializerLog = null, IFileManager fileManager = null, ILogger logger = null)
        {
            // Set properties from parameters
            FileManager = fileManager ?? new DefaultFileManager();
            Logger = logger;
            BasePath = NormalizePath(basePath, true);
            Settings = settings ?? new SerializerSettings();
            Log = serializerLog ?? new DefaultSerializerLog();

            // Initialize properties
            MemoryMap = new MemoryMap();
            Cache = new SerializableCache(Logger);
            ObjectStorage = new Dictionary<string, object>();
            AdditionalSettings = new Dictionary<Type, object>();
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

        #endregion

        #region Settings

        public ISerializerSettings Settings { get; }

        protected Dictionary<Type, object> AdditionalSettings { get; }
        public T GetSettings<T>(bool throwIfNotFound = true) 
        {
            var s = AdditionalSettings.TryGetValue(typeof(T), out object settings) ? settings : null;

            if (s != null) 
                return (T)s;

            if (throwIfNotFound)
                throw new Exception($"The requested serializer settings of type {typeof(T)} could not be found");

            return default;
        }
        public T AddSettings<T>(T settings)
        {
            AdditionalSettings[typeof(T)] = settings;
            return settings;
        }
        public void RemoveSettings<T>()
        {
            AdditionalSettings.Remove(typeof(T));
        }

        public Encoding DefaultEncoding => Settings.DefaultStringEncoding;
        public bool CreateBackupOnWrite => Settings.CreateBackupOnWrite;
        public bool SavePointersForRelocation => Settings.SavePointersForRelocation;

        #endregion

        #region Storage

        protected Dictionary<string, object> ObjectStorage { get; }

        public T GetStoredObject<T>(string id)
        {
            if (ObjectStorage.ContainsKey(id)) 
                return (T)ObjectStorage[id];

            return default;
        }

        public void RemoveStoredObject(string id)
        {
            ObjectStorage.Remove(id);
        }

        public T StoreObject<T>(string id, T obj)
        {
            ObjectStorage[id] = obj;
            return obj;
        }

        #endregion

        #region Pre-Defined Pointers

        protected Dictionary<string, long> PreDefinedPointers { get; set; }

        public void AddPreDefinedPointer(string key, long pointer)
        {
            PreDefinedPointers ??= new Dictionary<string, long>();

            PreDefinedPointers[key] = pointer;
        }
        public void AddPreDefinedPointer(Enum key, long pointer)
        {
            PreDefinedPointers ??= new Dictionary<string, long>();

            PreDefinedPointers[key.ToString()] = pointer;
        }
        public void AddPreDefinedPointers(IReadOnlyCollection<KeyValuePair<string, long>> pointers)
        {
            PreDefinedPointers ??= new Dictionary<string, long>();

            foreach (var p in pointers)
                PreDefinedPointers[p.Key] = p.Value;
        }
        public void AddPreDefinedPointers<T>(IReadOnlyCollection<KeyValuePair<T, long>> pointers)
            where T : Enum
        {
            PreDefinedPointers ??= new Dictionary<string, long>();

            foreach (var p in pointers)
                PreDefinedPointers[p.Key.ToString()] = p.Value;
        }

        public Pointer GetPreDefinedPointer(string key, BinaryFile file, bool required = true)
        {
            if (PreDefinedPointers?.ContainsKey(key) != true)
            {
                if (required)
                    throw new Exception($"Pre-defined pointer with key {key} was not found in the context using file {file?.FilePath}");
                else
                    return null;
            }

            return new Pointer(PreDefinedPointers[key], file);
        }
        public Pointer GetPreDefinedPointer(Enum key, BinaryFile file, bool required = true) => GetPreDefinedPointer(key.ToString(), file, required);

        #endregion

        #region Files

        public Stream GetFileStream(string relativePath)
        {
            Stream str = FileManager.GetFileReadStream(GetAbsoluteFilePath(NormalizePath(relativePath, false)));
            return str;
        }
        public BinaryFile GetFile(string relativePath)
        {
            string path = NormalizePath(relativePath, false);
            return MemoryMap.Files.FirstOrDefault<BinaryFile>(f => f.FilePath?.ToLower() == path?.ToLower() || f.Alias?.ToLower() == relativePath?.ToLower());
        }

        public virtual string GetAbsoluteFilePath(string relativePath) => BasePath + relativePath;
        public virtual string NormalizePath(string path, bool isDirectory)
        {
            string newPath = path.Replace("\\", "/");
            
            if (isDirectory && !newPath.EndsWith("/") && !String.IsNullOrWhiteSpace(path)) 
                newPath += "/";
            
            return newPath;
        }

        public void AddFile(BinaryFile file)
        {
            MemoryMap.Files.Add(file);
        }
        public void RemoveFile(string filePath) => RemoveFile(GetFile(filePath));
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
        public bool FileExists(BinaryFile file)
        {
            return MemoryMap.Files.Contains(file);
        }
        public bool FileExists(string relativePath)
        {
            BinaryFile f = GetFile(relativePath);
            return f != null;
        }

        public T GetMainFileObject<T>(string relativePath) 
            where T : BinarySerializable
        {
            return GetMainFileObject<T>(GetFile(relativePath));
        }
        public T GetMainFileObject<T>(BinaryFile file) 
            where T : BinarySerializable
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

        #region Events

        public event EventHandler Disposed;

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
        public void Dispose()
        {
            Close();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}