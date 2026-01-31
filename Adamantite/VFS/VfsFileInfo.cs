using System;

namespace Adamantite.VFS
{
    public class VfsFileInfo
    {
        public string Path { get; set; }
        public bool IsDirectory { get; set; }
        public long Length { get; set; }
        public DateTime LastModified { get; set; }

        public VfsFileInfo(string path, bool isDirectory, long length, DateTime modified)
        {
            Path = path;
            IsDirectory = isDirectory;
            Length = length;
            LastModified = modified;
        }
    }
}
