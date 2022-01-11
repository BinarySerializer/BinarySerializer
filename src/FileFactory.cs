using System;

namespace BinarySerializer
{
    /// <summary>
    /// Handles reading and writing serializable files
    /// </summary>
    public static class FileFactory
    {
        #region Public Static Methods

        /// <summary>
        /// Reads a file or gets it from the cache
        /// </summary>
        /// <typeparam name="T">The type to serialize to</typeparam>
        /// <param name="context">The context</param>
        /// <param name="filePath">The file path to read from</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <returns>The file data</returns>
        public static T Read<T>(Context context, string filePath, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            // Try cached version, to avoid creating the deserializer unless necessary
            T mainObj = context.GetMainFileObject<T>(filePath);

            if (mainObj != null)
                return mainObj;

            // Get a deserializer
            BinaryDeserializer s = context.Deserializer;

            // Use the deserializer to serialize the file
            return s.SerializeFile<T>(
                relativePath: filePath,
                obj: null,
                onPreSerialize: onPreSerialize == null 
                    ? (Action<T>)null 
                    : x => onPreSerialize(s, x),
                name: filePath);
        }

        /// <summary>
        /// Reads a file from an offset or gets it from the cache
        /// </summary>
        /// <typeparam name="T">The type to serialize to</typeparam>
        /// <param name="context">The context</param>
        /// <param name="offset">The offset to read from</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">Optional name for logging</param>
        /// <returns>The file data</returns>
        public static T Read<T>(Context context, Pointer offset, Action<SerializerObject, T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new()
        {
            if (context == null) 
                throw new ArgumentNullException(nameof(context));
            if (offset == null) 
                throw new ArgumentNullException(nameof(offset));
            
            T mainObj = default;

            // Get a deserializer
            BinaryDeserializer s = context.Deserializer;

            // Use the deserializer to serialize the data at the specified offset
            context.Deserializer.DoAt(offset, () =>
            {
                mainObj = context.Deserializer.SerializeObject<T>(
                    obj: mainObj,
                    onPreSerialize: onPreSerialize == null
                        ? (Action<T>)null
                        : x => onPreSerialize(s, x),
                    name: name);
            });

            return mainObj;
        }

        /// <summary>
        /// Writes the data from the cache to the specified path
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="filePath">The file path to write to</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        public static T Write<T>(Context context, string filePath, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));

            T obj = context.Cache.FromOffset<T>(context.FilePointer(filePath));

            if (obj == null)
                throw new ContextException($"There is no cached object of type {typeof(T)} for {filePath}");

            // Get a serializer
            BinarySerializer s = context.Serializer;

            return s.SerializeFile<T>(
                relativePath: filePath,
                obj: obj,
                onPreSerialize: onPreSerialize == null 
                    ? (Action<T>)null
                    : x => onPreSerialize(s, x),
                name: filePath);
        }

        /// <summary>
        /// Writes the data to the specified path
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="filePath">The file path to write to</param>
        /// <param name="obj">The object to write</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        public static T Write<T>(Context context, string filePath, T obj, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (filePath == null)
                throw new ArgumentNullException(nameof(filePath));
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            // Get a serializer
            BinarySerializer s = context.Serializer;

            return s.SerializeFile<T>(
                relativePath: filePath,
                obj: obj,
                onPreSerialize: onPreSerialize == null
                    ? (Action<T>)null
                    : x => onPreSerialize(s, x),
                name: filePath);
        }

        /// <summary>
        /// Writes the data to the specified offset
        /// </summary>
        /// <param name="context">The context</param>
        /// <param name="offset">The offset to write to</param>
        /// <param name="obj">The object to write</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">Optional name for logging</param>
        public static T Write<T>(Context context, Pointer offset, T obj, Action<SerializerObject, T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new()
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (offset == null)
                throw new ArgumentNullException(nameof(offset));
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            // Get a serializer
            BinarySerializer s = context.Serializer;

            s.DoAt(offset, () =>
            {
                obj = s.SerializeObject(
                    obj: obj,
                    onPreSerialize: onPreSerialize == null
                        ? (Action<T>)null
                        : x => onPreSerialize(s, x),
                    name: name);
            });

            return obj;
        }

        #endregion

        #region Obsolete

        /// <summary>
        /// Reads a file or gets it from the cache
        /// </summary>
        /// <typeparam name="T">The type to serialize to</typeparam>
        /// <param name="filePath">The file path to read from</param>
        /// <param name="context">The context</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <returns>The file data</returns>
        [Obsolete("Use Read<T>(Context, String, Action<SerializerObject, T>) instead")]
        public static T Read<T>(string filePath, Context context, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new() => Read<T>(context, filePath, onPreSerialize);

        /// <summary>
        /// Reads a file from an offset or gets it from the cache
        /// </summary>
        /// <typeparam name="T">The type to serialize to</typeparam>
        /// <param name="offset">The offset to read from</param>
        /// <param name="context">The context</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">Optional name for logging</param>
        /// <returns>The file data</returns>
        [Obsolete("Use Read<T>(Context, Pointer, Action<SerializerObject, T>, string) instead")]
        public static T Read<T>(Pointer offset, Context context, Action<SerializerObject, T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new() => Read<T>(context, offset, onPreSerialize, name);

        /// <summary>
        /// Writes the data from the cache to the specified path
        /// </summary>
        /// <param name="filePath">The file path to write to</param>
        /// <param name="context">The context</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        [Obsolete("Use Write<T>(Context, String, Action<SerializerObject, T>) instead")]
        public static T Write<T>(string filePath, Context context, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new() => Write<T>(context, filePath, onPreSerialize);

        /// <summary>
        /// Writes the data to the specified path
        /// </summary>
        /// <param name="filePath">The file path to write to</param>
        /// <param name="obj">The object to write</param>
        /// <param name="context">The context</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        [Obsolete("Use Write<T>(Context, String, T, Action<SerializerObject, T>) instead")]
        public static T Write<T>(string filePath, T obj, Context context, Action<SerializerObject, T> onPreSerialize = null)
            where T : BinarySerializable, new() => Write<T>(context, filePath, obj, onPreSerialize);

        /// <summary>
        /// Writes the data to the specified offset
        /// </summary>
        /// <param name="offset">The offset to write to</param>
        /// <param name="obj">The object to write</param>
        /// <param name="context">The context</param>
        /// <param name="onPreSerialize">Optional action to run before serializing</param>
        /// <param name="name">Optional name for logging</param>
        [Obsolete("Use Write<T>(Context, Pointer, T, Action<SerializerObject, T>, String) instead")]
        public static T Write<T>(Pointer offset, T obj, Context context, Action<SerializerObject, T> onPreSerialize = null, string name = null)
            where T : BinarySerializable, new() => Write<T>(context, offset, obj, onPreSerialize, name);

        #endregion
    }
}