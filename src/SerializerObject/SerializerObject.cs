using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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
        public abstract uint CurrentLength { get; }

        /// <summary>
        /// The current pointer
        /// </summary>
        public abstract Pointer CurrentPointer { get; }

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
        public abstract void Log(string logString);

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
            });

            return obj;
        }
        public void DoEncodedIf(IStreamEncoder encoder, bool isEncoded, Action action, Endian? endianness = null)
        {
            if (isEncoded)
                DoEncoded(encoder, action, endianness);
            else
                action();
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
            Pointer ptr = CurrentPointer;

            if ((ptr.AbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes != 0)
                Goto(ptr + (alignBytes - (ptr.AbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes));
        }

        public void DoAt(Pointer offset, Action action)
        {
            if (offset == null) 
                return;

            Pointer off_current = CurrentPointer;
            Goto(offset);
            action();
            Goto(off_current);
        }

        public T DoAt<T>(Pointer offset, Func<T> action)
        {
            if (offset == null) 
                return default;

            Pointer off_current = CurrentPointer;
            Goto(offset);
            var result = action();
            Goto(off_current);
            return result;
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
                Goto(CurrentPointer + Marshal.SizeOf<T>());

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

        public abstract Pointer SerializePointer(Pointer obj, Pointer anchor = null, bool allowInvalid = false, string name = null);

        public abstract Pointer<T> SerializePointer<T>(Pointer<T> obj, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null) where T : BinarySerializable, new();

        public abstract string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null);

        #endregion

        #region Array Serialization

        public abstract T[] SerializeArraySize<T, U>(T[] obj, string name = null) where U : struct;
        public abstract T[] SerializeArray<T>(T[] obj, long count, string name = null);
        public abstract T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize = null, string name = null) where T : BinarySerializable, new();
        public T[] SerializeObjectArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, bool includeLastObj = false, Action<T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new()
        {
            if (obj == null)
            {
                var objects = new List<T>();
                var index = 0;

                while (true)
                {
                    var serializedObj = SerializeObject<T>(default, onPreSerialize: onPreSerialize, name: $"{name}[{index++}]");

                    if (conditionCheckFunc(serializedObj))
                    {
                        if (includeLastObj)
                            objects.Add(serializedObj);

                        break;
                    }

                    objects.Add(serializedObj);
                }

                obj = objects.ToArray();
            }
            else
            {
                SerializeObjectArray<T>(obj, obj.Length, onPreSerialize: onPreSerialize, name: name);
            }

            return obj;
        }
        public Pointer[] SerializePointerArrayUntil(Pointer[] obj, Func<Pointer, bool> conditionCheckFunc, bool includeLastObj = false, string name = null)
        {
            if (obj == null)
            {
                var objects = new List<Pointer>();
                var index = 0;

                while (true)
                {
                    var serializedObj = SerializePointer(default, name: $"{name}[{index++}]");

                    if (conditionCheckFunc(serializedObj))
                    {
                        if (includeLastObj)
                            objects.Add(serializedObj);

                        break;
                    }

                    objects.Add(serializedObj);
                }

                obj = objects.ToArray();
            }
            else
            {
                SerializePointerArray(obj, obj.Length, name: name);
            }

            return obj;
        }

        public abstract Pointer[] SerializePointerArray(Pointer[] obj, long count, Pointer anchor = null, bool allowInvalid = false, string name = null);
        public abstract Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null) where T : BinarySerializable, new();

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
        public T SerializeFromBytes<T>(byte[] bytes, string key, Action<T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new()
        {
            if (bytes == null)
                return default;

            if (!Context.FileExists(key))
            {
                var typeStream = new MemoryStream(bytes);
                Context.AddFile(new StreamFile(key, typeStream, Context));
            }

            return DoAt(Context.GetFile(key).StartPointer, () => SerializeObject<T>(default, onPreSerialize, name: name ?? key));
        }
        public T DoAtBytes<T>(byte[] bytes, string key, Func<T> func)
        {
            if (bytes == null)
                return default;

            if (!Context.FileExists(key))
            {
                var typeStream = new MemoryStream(bytes);
                Context.AddFile(new StreamFile(key, typeStream, Context));
            }

            return DoAt(Context.GetFile(key).StartPointer, func);
        }

        public T DoAtEncoded<T>(Pointer offset, IStreamEncoder encoder, Func<T> action)
        {
            return DoAt(offset, () => DoEncoded(encoder, action));
        }
        public void DoAtEncoded(Pointer offset, IStreamEncoder encoder, Action action)
        {
            DoAt(offset, () => DoEncoded(encoder, action));
        }

        public abstract void DoEndian(Endian endianness, Action action);

        public abstract void SerializeBitValues<T>(Action<SerializeBits> serializeFunc) where T : new();
        public delegate int SerializeBits(int value, int length, string name = null);

        public void SerializePadding(int length, bool logIfNotNull = false, string name = "Padding")
        {
            var a = SerializeArray<byte>(new byte[length], length, name: name);

            if (logIfNotNull && a.Any(x => x != 0))
                Logger.LogWarning($"Padding at {CurrentPointer - length} contains data! Data: {a.ToHexString()}");
        }

        #endregion

        #region Caching

        public virtual Task FillCacheForReadAsync(int length) => Task.CompletedTask;

        #endregion

        #region Settings

        public T GetSettings<T>() where T : class, ISerializerSettings => Context.GetSettings<T>();

        #endregion
    }
}