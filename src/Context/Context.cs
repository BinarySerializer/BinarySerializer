#nullable enable
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

        public Context(
            string basePath, 
            ISerializerSettings? settings = null, 
            ISerializerLog? serializerLog = null, 
            IFileManager? fileManager = null, 
            ISystemLog? systemLog = null)
        {
            // Set properties from parameters
            FileManager = fileManager ?? new DefaultFileManager();
            SystemLog = systemLog;
            BasePath = NormalizePath(basePath, true);
            Settings = settings ?? new SerializerSettings();
            SerializerLog = serializerLog ?? new EmptySerializerLog();

            // Initialize properties
            MemoryMap = new MemoryMap();
            Cache = new SerializableCache(SystemLog);
            ObjectStorage = new Dictionary<string, object>();
            AdditionalSettings = new Dictionary<Type, object>();
        }

        #endregion

        #region Abstraction

        public IFileManager FileManager { get; }
        public ISystemLog? SystemLog { get; }

        #endregion

        #region Internal Fields

        internal object _threadLock = new();

        #endregion

        #region Public Properties

        public string BasePath { get; }
        public MemoryMap MemoryMap { get; }
        public SerializableCache Cache { get; }
        public ISerializerLog SerializerLog { get; }

        #endregion

        #region Settings

        public ISerializerSettings Settings { get; }

        protected Dictionary<Type, object> AdditionalSettings { get; }
        public T GetSettings<T>()
            where T : class
        {
            if (!AdditionalSettings.TryGetValue(typeof(T), out object settings))
                throw new ContextException($"The requested serializer settings of type {typeof(T)} could not be found");
            
            return (T)settings;
        }
        public T? TryGetSettings<T>()
            where T : class
        {
            if (!AdditionalSettings.TryGetValue(typeof(T), out object settings))
                return default;

            return (T)settings;
        }
        public T AddSettings<T>(T settings)
            where T : class
        {
            AdditionalSettings[typeof(T)] = settings;
            return settings;
        }
        public void AddSettings(object settings, Type settingsType)
        {
            AdditionalSettings[settingsType] = settings;
        }
        public void RemoveSettings<T>()
            where T : class
        {
            AdditionalSettings.Remove(typeof(T));
        }
        public bool HasSettings<T>()
            where T : class
        {
            return AdditionalSettings.ContainsKey(typeof(T));
        }

        public Encoding DefaultEncoding => Settings.DefaultStringEncoding;
        public bool CreateBackupOnWrite => Settings.CreateBackupOnWrite;
        public bool SavePointersForRelocation => Settings.SavePointersForRelocation;

        #endregion

#nullable restore

        #region Storage

        protected Dictionary<string, object> ObjectStorage { get; }

        public T GetStoredObject<T>(string id, bool throwIfNotFound = false)
        {
            if (ObjectStorage.ContainsKey(id)) 
                return (T)ObjectStorage[id];

            if (throwIfNotFound)
                throw new ContextException($"The requested object with ID {id} could not be found");

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
                    throw new ContextException($"Pre-defined pointer with key {key} was not found in the context using file {file?.FilePath}");
                else
                    return null;
            }

            return new Pointer(PreDefinedPointers[key], file);
        }
        public Pointer GetPreDefinedPointer(Enum key, BinaryFile file, bool required = true) => GetPreDefinedPointer(key.ToString(), file, required);

        #endregion

        #region Files

        private char SeparatorChar => FileManager.SeparatorCharacter switch
        {
            PathSeparatorChar.ForwardSlash => '/',
            PathSeparatorChar.BackSlash => '\\',
            _ => '/',
        };

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
            // Get the path separator character
            string separatorChar = SeparatorChar.ToString();

            // Normalize the path
            string newPath = FileManager.SeparatorCharacter switch
            {
                PathSeparatorChar.ForwardSlash => path.Replace('\\', '/'),
                PathSeparatorChar.BackSlash => path.Replace('/', '\\'),
                _ => throw new ArgumentOutOfRangeException(nameof(FileManager.SeparatorCharacter), FileManager.SeparatorCharacter, null)
            };

            // Make sure a directory path ends with the separator
            if (isDirectory && !newPath.EndsWith(separatorChar) && !String.IsNullOrWhiteSpace(path)) 
                newPath += separatorChar;
            
            return newPath;
        }

        public T AddFile<T>(T file)
            where T : BinaryFile
        {
            if (MemoryMap.Files.Any(x => x.FilePath == file.FilePath))
                throw new ContextException($"A file with the path '{file.FilePath}' has already been added to the context");

            MemoryMap.Files.Add(file);

            SystemLog?.LogTrace("Added file {0}", file.FilePath);

            return file;
        }
        public void RemoveFile(string filePath) => RemoveFile(GetFile(filePath));
        public void RemoveFile(BinaryFile file)
        {
            if (file is null)
                return;

            MemoryMap.Files.Remove(file);
            deserializer?.DisposeFile(file);
            serializer?.DisposeFile(file);
            file.Dispose();

            SystemLog?.LogTrace("Removed file {0}", file.FilePath);
        }

        public Pointer<T> FilePointer<T>(string relativePath) 
            where T : BinarySerializable, new()
        {
            return new Pointer<T>(FilePointer(relativePath));
        }
        public Pointer FilePointer(string relativePath)
        {
            BinaryFile f = GetFile(relativePath);

            if (f == null)
                throw new ContextException($"File with path {relativePath} is not loaded in this Context!");

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

            SerializerLog.Dispose();
        }
        public void Dispose()
        {
            Close();
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }
}