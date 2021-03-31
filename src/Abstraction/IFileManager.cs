using System.IO;
using System.Threading.Tasks;

namespace BinarySerializer
{
    public interface IFileManager
    {
        bool DirectoryExists(string path);
        bool FileExists(string path);

        Stream GetFileReadStream(string path);
        Stream GetFileWriteStream(string path, bool recreateOnWrite = true);

        string NormalizePath(string path, bool isDirectory);

        Task FillCacheForReadAsync(int length, Reader reader);
    }
}