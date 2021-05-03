using System.IO;
using System.Threading.Tasks;

namespace BinarySerializer
{
    public class DefaultFileManager : IFileManager
    {
        public bool DirectoryExists(string path) => Directory.Exists(path);
        public bool FileExists(string path) => File.Exists(path);

        public Stream GetFileReadStream(string path) => File.OpenRead(path);
        public Stream GetFileWriteStream(string path, bool recreateOnWrite = true) => recreateOnWrite ? File.Create(path) : File.OpenWrite(path);

        public Task FillCacheForReadAsync(int length, Reader reader) => Task.CompletedTask;
    }
}