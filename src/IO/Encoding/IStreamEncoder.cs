using System.IO;

namespace BinarySerializer
{
    /// <summary>
    /// Encodes/decodes serializer data
    /// </summary>
    public interface IStreamEncoder
    {
        /// <summary>
        /// The name of the encoder, for use in logging
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Decodes the data and returns it in a stream
        /// </summary>
        /// <param name="s">The serializer object</param>
        /// <returns>The stream with the decoded data</returns>
        Stream DecodeStream(Stream s);

        /// <summary>
        /// Encodes the data and returns it in a stream
        /// </summary>
        /// <param name="s">The serializer object</param>
        /// <returns>The stream with the encoded data</returns>
        Stream EncodeStream(Stream s);
    }
}