using System;
using System.Collections.Generic;
using System.IO;

namespace Adamantite.VFS
{
    public interface IFileSystem
    {
        Stream OpenRead(string path);
        Stream OpenWrite(string path);
        bool Exists(string path);
        IEnumerable<VfsFileInfo> Enumerate(string path);
        VfsFileInfo? GetFileInfo(string path);
        void CreateDirectory(string path);
        void Delete(string path);
        byte[] ReadAllBytes(string path);
        void WriteAllBytes(string path, byte[] data);
    }
}
