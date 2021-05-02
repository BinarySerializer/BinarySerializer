using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// Default class for serializer settings
    /// </summary>
    public class DefaultSerializerSettings : ISerializerSettings
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        /// <param name="defaultStringEncoding">The default string encoding to use when none is specified. Set to null for <see cref="Encoding.UTF8"/></param>
        /// <param name="createBackupOnWrite">Indicates if a backup file should be created when writing to a file</param>
        public DefaultSerializerSettings(Encoding defaultStringEncoding = null, bool createBackupOnWrite = false)
        {
            DefaultStringEncoding = defaultStringEncoding ?? Encoding.UTF8;
            CreateBackupOnWrite = createBackupOnWrite;
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
    }
}