using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BinarySerializer
{
    /// <summary>
    /// A base binary serializer used for serializing/deserializing
    /// </summary>
    public abstract class SerializerObject
    {
        #region Constructor

        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="context">The serializer context</param>
        protected SerializerObject(Context context)
        {
            Context = context;
        }

        #endregion

        #region Protected Properties

        protected bool DisableLogForObject { get; set; }

        protected IFileManager FileManager => Context.FileManager;
        protected ILogger Logger => Context.Logger;

        #endregion

        #region Public Properties

        /// <summary>
        /// The serialize context, containing all the open files and the settings
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// The current depth when serializing objects
        /// </summary>
        public int Depth { get; protected set; } = 0;

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public abstract long CurrentLength { get; }

        /// <summary>
        /// The current length as a 32-bit value
        /// </summary>
        public uint CurrentLength32 => (uint)CurrentLength;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public abstract BinaryFile CurrentBinaryFile { get; }

        /// <summary>
        /// The current pointer
        /// </summary>
        public virtual Pointer CurrentPointer => CurrentBinaryFile == null ? null : new Pointer(CurrentAbsoluteOffset, CurrentBinaryFile, size: Context.Settings.LoggingPointerSize ?? CurrentBinaryFile.PointerSize);

        /// <summary>
        /// The current absolute offset
        /// </summary>
        public virtual long CurrentAbsoluteOffset => CurrentFileOffset + CurrentBinaryFile.BaseAddress;
        
        /// <summary>
        /// The current file offset
        /// </summary>
        public abstract long CurrentFileOffset { get; }

        /// <summary>
        /// Default values for some serializer functions
        /// </summary>
        public virtual SerializerDefaults Defaults { get; set; }

        public virtual bool FullSerialize => true;

        #endregion

        #region Logging

        /// <summary>
        /// Indicates if logging is enabled for the serialization
        /// </summary>
        public bool IsLogEnabled => Context.Log.IsEnabled && !DisableLogForObject;

        /// <summary>
        /// Writes a line to the serializer log, if enabled
        /// </summary>
        /// <param name="logString">The string to log</param>
        /// <param name="args">The log string arguments</param>
        [StringFormatMethod("logString")]
        public abstract void Log(string logString, params object[] args);

        /// <summary>
        /// Logs a warning message (to log to the serializer log use <see cref="Log"/> instead)
        /// </summary>
        /// <param name="message">The message to log</param>
        /// <param name="args">The message string arguments</param>
        [StringFormatMethod("message")]
        public void LogWarning(string message, params object[] args) => Logger?.LogWarning(message, args);

        #endregion

        #region Encoding

        protected List<EncodedState> EncodedFiles { get; } = new List<EncodedState>();

        protected class EncodedState
        {
            public Stream Stream { get; set; }
            public StreamFile File { get; set; }
            public IStreamEncoder Encoder { get; set; }
        }

        public abstract Pointer BeginEncoded(IStreamEncoder encoder, Endian? endianness = null, bool allowLocalPointers = false, string filename = null);
        public abstract void EndEncoded(Pointer endPointer);
        public abstract void DoEncoded(IStreamEncoder encoder, Action action, Endian? endianness = null, bool allowLocalPointers = false, string filename = null);
        public virtual T DoEncoded<T>(IStreamEncoder encoder, Func<T> action, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            var obj = default(T);

            DoEncoded(encoder, () =>
            {
                obj = action();
            }, endianness: endianness, allowLocalPointers: allowLocalPointers, filename: filename);

            return obj;
        }
        public virtual void DoEncodedIf(IStreamEncoder encoder, bool isEncoded, Action action, Endian? endianness = null, bool allowLocalPointers = false)
        {
            if (isEncoded)
                DoEncoded(encoder, action, endianness, allowLocalPointers: allowLocalPointers);
            else
                action();
        }
        public virtual T DoEncodedIf<T>(IStreamEncoder encoder, bool isEncoded, Func<T> action, Endian? endianness = null, bool allowLocalPointers = false)
        {
            if (isEncoded)
                return DoEncoded(encoder, action, endianness, allowLocalPointers: allowLocalPointers);
            else
                return action();
        }

        #endregion

        #region XOR

        public virtual void BeginXOR(IXORCalculator xorCalculator) { }
        public virtual void EndXOR() { }
        public virtual IXORCalculator GetXOR() => null;

        public virtual void DoXOR(byte xorKey, Action action) => DoXOR(new XOR8Calculator(xorKey), action);
        public void DoXOR(IXORCalculator xorCalculator, Action action)
        {
            var prevCalculator = GetXOR();

            if (xorCalculator == null)
                EndXOR();
            else
                BeginXOR(xorCalculator);

            action();

            if (prevCalculator == null)
                EndXOR();
            else
                BeginXOR(prevCalculator);
        }

        #endregion

        #region Positioning

        public abstract void Goto(Pointer offset);

        public void Align(int alignBytes = 4, Pointer baseOffset = null)
        {
            if ((CurrentAbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes != 0)
            {
                Pointer ptr = CurrentPointer;

                Goto(ptr + (alignBytes - (ptr.AbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes));
            }
        }

        public virtual void DoAt(Pointer offset, Action action)
        {
            if (offset == null) 
                return;

            Pointer off_current = CurrentPointer;
            Goto(offset);

            try
            {
                action();
            }
            finally
            {
                Goto(off_current);
            }
        }

        // TODO: Remove this? This overload causes issues for the size serializer where DoAt is disabled thus causing this to
        //       always return null. A potential fix is to allow for a default value to be specified.
        public virtual T DoAt<T>(Pointer offset, Func<T> action)
        {
            if (offset == null) 
                return default;

            Pointer off_current = CurrentPointer;
            Goto(offset);

            try
            {
                return action();
            }
            finally
            {
                Goto(off_current);
            }
        }

        #endregion

        #region Checksum

        public abstract T SerializeChecksum<T>(T calculatedChecksum, string name = null);

        /// <summary>
        /// Begins calculating byte checksum for all following serialize operations
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public virtual void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) { }

        /// <summary>
        /// Pauses calculating the checksum and returns the current checksum calculator to be used when resuming
        /// </summary>
        /// <returns>The current checksum calculator or null if none is used</returns>
        public virtual IChecksumCalculator PauseCalculateChecksum() => null;

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <returns>The checksum value</returns>
        public virtual T EndCalculateChecksum<T>() => default;

        public T DoChecksum<T>(IChecksumCalculator<T> c, Action action, ChecksumPlacement placement, bool calculateChecksum = true, string name = null)
        {
            // Get the current pointer
            var p = CurrentPointer;

            // Skip the length of the checksum value if it's before the data
            if (calculateChecksum && placement == ChecksumPlacement.Before)
                Goto(p + Marshal.SizeOf<T>());

            // Begin calculating the checksum
            if (calculateChecksum)
                BeginCalculateChecksum(c);

            // Serialize the block data
            action();

            if (!calculateChecksum)
                return default;

            // End calculating the checksum
            var v = EndCalculateChecksum<T>();

            // Serialize the checksum
            if (placement == ChecksumPlacement.Before)
                return DoAt(p, () => SerializeChecksum(v, name));
            else
                return SerializeChecksum(v, name);
        }

        #endregion

        #region Serialization

        /// <summary>
        /// Serializes a value
        /// </summary>
        /// <typeparam name="T">The type of value to serialize</typeparam>
        /// <param name="obj">The value to be serialized</param>
        /// <param name="name">A name can be provided optionally, for logging or text serialization purposes</param>
        /// <returns>The value that was serialized</returns>
        public abstract T Serialize<T>(T obj, string name = null);

        /// <summary>
        /// Serializes a <see cref="BinarySerializable"/> object
        /// </summary>
        /// <typeparam name="T">The type of object to serialize</typeparam>
        /// <param name="obj">The object to be serialized</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">A name can be provided optionally, for logging or text serialization purposes</param>
        /// <returns>The object that was serialized</returns>
        public abstract T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null) where T : BinarySerializable, new();

        public abstract Pointer SerializePointer(Pointer obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null);

        public abstract Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null) where T : BinarySerializable, new();

        public abstract string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null);

        #endregion

        #region Array Serialization

        public abstract T[] SerializeArraySize<T, U>(T[] obj, string name = null) where U : struct;
        public abstract T[] SerializeArray<T>(T[] obj, long count, string name = null);
        public T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize, string name = null)
            where T : BinarySerializable, new()
        {
            return SerializeObjectArray<T>(obj, count, onPreSerialize == null ? (Action<T, int>)null : (x, _) => onPreSerialize(x), name);
        }
        public abstract T[] SerializeObjectArray<T>(T[] obj, long count, Action<T, int> onPreSerialize = null, string name = null) where T : BinarySerializable, new();

        /// <summary>
        /// Serializes an array of undefined size until a specified condition is met
        /// </summary>
        /// <typeparam name="T">The value type</typeparam>
        /// <param name="obj">The array</param>
        /// <param name="conditionCheckFunc">The condition for ending the array serialization</param>
        /// <param name="getLastObjFunc">If specified the last value when read will be ignored and this will be used to prepend a value when writing</param>
        /// <param name="name">The name</param>
        /// <returns>The array</returns>
        public abstract T[] SerializeArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, string name = null);

        /// <summary>
        /// Serializes an object array of undefined size until a specified condition is met
        /// </summary>
        /// <typeparam name="T">The object type</typeparam>
        /// <param name="obj">The object array</param>
        /// <param name="conditionCheckFunc">The condition for ending the array serialization</param>
        /// <param name="getLastObjFunc">If specified the last object when read will be ignored and this will be used to prepend an object when writing</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">The name</param>
        /// <returns>The object array</returns>
        public abstract T[] SerializeObjectArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new();
        public Pointer[] SerializePointerArrayUntil(Pointer[] obj, Func<Pointer, bool> conditionCheckFunc, PointerSize size = PointerSize.Pointer32, Func<Pointer> getLastObjFunc = null, string name = null)
        {
            if (obj == null)
            {
                var objects = new List<Pointer>();
                var index = 0;

                while (true)
                {
                    var serializedObj = SerializePointer(default, size: size, name: $"{name}[{index++}]");

                    if (conditionCheckFunc(serializedObj))
                    {
                        if (getLastObjFunc == null)
                            objects.Add(serializedObj);

                        break;
                    }

                    objects.Add(serializedObj);
                }

                obj = objects.ToArray();
            }
            else
            {
                if (getLastObjFunc != null)
                    obj = obj.Append(getLastObjFunc()).ToArray();

                SerializePointerArray(obj, obj.Length, size: size, name: name);
            }

            return obj;
        }

        public abstract Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null);
        public abstract Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T, int> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null) where T : BinarySerializable, new();

        public abstract string[] SerializeStringArray(string[] obj, long count, int length, Encoding encoding = null, string name = null);

        #endregion

        #region Other Serialization

        public virtual T SerializeFile<T>(string relativePath, T obj, Action<T> onPreSerialize = null, string name = null) where T : BinarySerializable, new()
        {
            T t = obj;
            DoAt(Context.FilePointer(relativePath), () => {
                t = SerializeObject<T>(obj, onPreSerialize: onPreSerialize, name: name);
            });
            return t;
            //return Context.FilePointer<T>(relativePath)?.Resolve(this, onPreSerialize: onPreSerialize).Value;
        }
        public T SerializeFromBytes<T>(byte[] bytes, string key, Action<T> onPreSerialize = null, bool removeFile = true, string name = null)
            where T : BinarySerializable, new()
        {
            return DoAtBytes(bytes, key, () => SerializeObject<T>(default, onPreSerialize, name: name ?? key), removeFile);
        }
        public T DoAtBytes<T>(byte[] bytes, string key, Func<T> func, bool removeFile = true)
        {
            if (bytes == null)
                return default;

            try
            {
                if (!Context.FileExists(key))
                {
                    var typeStream = new MemoryStream(bytes);
                    Context.AddFile(new StreamFile(Context, key, typeStream, parentPointer: CurrentPointer));
                }

                return DoAt(Context.GetFile(key).StartPointer, func);
            }
            finally
            {
                if (removeFile)
                    Context.RemoveFile(key);
            }
        }

        public T DoAtEncoded<T>(Pointer offset, IStreamEncoder encoder, Func<T> action)
        {
            return DoAt(offset, () => DoEncoded(encoder, action));
        }
        public void DoAtEncoded(Pointer offset, IStreamEncoder encoder, Action action)
        {
            DoAt(offset, () => DoEncoded(encoder, action));
        }
        public void DoWithDefaults(SerializerDefaults defaults, Action action) {
            var curDefaults = Defaults;
            Defaults = defaults;
            try {
                action();
            } finally {
                Defaults = curDefaults;
            }
        }

        public abstract void DoEndian(Endian endianness, Action action);

        public abstract void SerializeBitValues(Action<SerializeBits64> serializeFunc);
        public abstract void DoBits<T>(Action<BitSerializerObject> serializeFunc);

        public delegate long SerializeBits64(long value, int length, string name = null);

        public void SerializePadding(int length, bool logIfNotNull = false, string name = "Padding")
        {
            if (length == 1)
            {
                var a = Serialize<byte>(default, name: name);

                if (logIfNotNull && a != 0)
                    LogWarning("Padding at {0} contains data! Data: 0x{1:X2}", CurrentPointer - length, a);
            }
            else
            {
                var a = SerializeArray<byte>(new byte[length], length, name: name);

                if (logIfNotNull && a.Any(x => x != 0))
                    LogWarning("Padding at {0} contains data! Data: {1}", CurrentPointer - length, a.ToHexString());
            }
        }

        public virtual void SerializeMagic<T>(T magic, bool throwIfNoMatch = true, string name = null)
        {
            T value = Serialize<T>(magic, name: name ?? "Magic");

            if (!value.Equals(magic))
            {
                if (throwIfNoMatch)
                    throw new Exception($"Magic '{value}' does not match expected magic of '{magic}'");
                else
                    LogWarning("Magic '{0}' does not match expected magic of '{1}'", value, magic);
            }
        }

        public virtual void SerializeMagicString(string magic, long length, Encoding encoding = null, bool throwIfNoMatch = true, string name = null)
        {
            var value = SerializeString(magic, length, encoding: encoding, name: name ?? "Magic");

            if (value != magic)
            {
                if (throwIfNoMatch)
                    throw new Exception($"Magic '{value}' does not match expected magic of '{magic}'");
                else
                    LogWarning("Magic '{0}' does not match expected magic of '{1}'", value, magic);
            }
        }

        #endregion

        #region Caching

        public virtual Task FillCacheForReadAsync(long length) => Task.CompletedTask;

        #endregion

        #region Settings

        public T GetSettings<T>() => Context.GetSettings<T>();

        #endregion

        #region Pre-Defined Pointers

        public virtual Pointer GetPreDefinedPointer(string key, bool required = true) => Context.GetPreDefinedPointer(key, CurrentBinaryFile, required);
        public virtual Pointer GetPreDefinedPointer(Enum key, bool required = true) => Context.GetPreDefinedPointer(key, CurrentBinaryFile, required);

        #endregion
    }
}