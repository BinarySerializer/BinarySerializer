using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// Default class for serializer settings
    /// </summary>
    public class SerializerSettings : ISerializerSettings
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="defaultStringEncoding">The default string encoding to use when none is specified. Set to null for <see cref="Encoding.UTF8"/></param>
        /// <param name="createBackupOnWrite">Indicates if a backup file should be created when writing to a file</param>
        /// <param name="savePointersForRelocation">Indicates if pointers should be saved in the Memory Map for relocation</param>
        /// <param name="ignoreCacheOnRead">Indicates if caching read objects should be ignored</param>
        /// <param name="loggingPointerSize">The pointer size to use when logging a <see cref="Pointer"/>. Set to <see langword="null"/> to dynamically determine the appropriate size.</param>
        public SerializerSettings(Encoding defaultStringEncoding = null, bool createBackupOnWrite = false, bool savePointersForRelocation = false, bool ignoreCacheOnRead = false, PointerSize? loggingPointerSize = PointerSize.Pointer32)
        {
            DefaultStringEncoding = defaultStringEncoding ?? Encoding.UTF8;
            SavePointersForRelocation = savePointersForRelocation;
            IgnoreCacheOnRead = ignoreCacheOnRead;
            CreateBackupOnWrite = createBackupOnWrite;
            LoggingPointerSize = loggingPointerSize;
        }

        /// <summary>
        /// The default string encoding to use when none is specified
        /// </summary>
        public virtual Encoding DefaultStringEncoding { get; }

        /// <summary>
        /// Indicates if a backup file should be created when writing to a file
        /// </summary>
        public virtual bool CreateBackupOnWrite { get; }

        /// <summary>
        /// Indicates if pointers should be saved in the Memory Map for relocation
        /// </summary>
        public virtual bool SavePointersForRelocation { get; }

        /// <summary>
        /// Indicates if caching read objects should be ignored
        /// </summary>
        public bool IgnoreCacheOnRead { get; }

        /// <summary>
        /// The pointer size to use when logging a <see cref="Pointer"/>. Set to <see langword="null"/> to dynamically determine the appropriate size.
        /// </summary>
        public PointerSize? LoggingPointerSize { get; }
    }
}