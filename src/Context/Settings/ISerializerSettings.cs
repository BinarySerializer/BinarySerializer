using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// Interface for serializer settings
    /// </summary>
    public interface ISerializerSettings
    {
        /// <summary>
        /// The default string encoding to use when none is specified
        /// </summary>
        Encoding DefaultStringEncoding { get; }
        
        /// <summary>
        /// Indicates if a backup file should be created when writing to a file
        /// </summary>
        bool CreateBackupOnWrite { get; }

        /// <summary>
        /// Indicates if pointers should be saved in the Memory Map for relocation
        /// </summary>
        bool SavePointersForRelocation { get; }

        /// <summary>
        /// Indicates if caching read objects should be ignored
        /// </summary>
        bool IgnoreCacheOnRead { get; }
    }
}