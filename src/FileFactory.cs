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
            // Try cached version, to avoid creating the deserializer unless necessary
            T mainObj = context.GetMainFileObject<T>(filePath);

            if (mainObj != null)
                return mainObj;

            // Use deserializer
            return context.Deserializer.SerializeFile<T>(
                relativePath: filePath,
                obj: null,
                onPreSerialize: (t) => onPreSerialize?.Invoke(context.Deserializer, t),
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
            // Try cached version, to avoid creating the deserializer unless necessary
            T mainObj = default;

            // Use deserializer
            context.Deserializer.DoAt(offset, () =>
                mainObj = context.Deserializer.SerializeObject<T>(
                    obj: mainObj,
                    onPreSerialize: (t) => onPreSerialize?.Invoke(context.Deserializer, t),
                    name: name));

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
            return context.FilePointer<T>(filePath)?.Resolve(
                s: context.Serializer,
                onPreSerialize: (t) => onPreSerialize?.Invoke(context.Serializer, t)).Value;
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
            return context.Serializer.SerializeFile<T>(
                relativePath: filePath,
                obj: obj,
                onPreSerialize: (t) => onPreSerialize?.Invoke(context.Serializer, t),
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
            BinarySerializer s = context.Serializer;

            return s.DoAt(offset, () => s.SerializeObject(
                obj: obj,
                onPreSerialize: t => onPreSerialize?.Invoke(context.Deserializer, t),
                name: name));
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