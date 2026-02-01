using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Adamantite.VFS
{
    public class PhysicalFileSystem : IFileSystem
    {
        readonly string _rootPath;

        public PhysicalFileSystem(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)) throw new ArgumentNullException(nameof(rootPath));
            _rootPath = Path.GetFullPath(rootPath);
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);
        }

        string ToPhysical(string path)
        {
            path = path?.Replace('/', Path.DirectorySeparatorChar) ?? string.Empty;
            var combined = Path.Combine(_rootPath, path ?? string.Empty);
            var full = Path.GetFullPath(combined);
            if (!full.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("Path escapes VFS root");
            return full;
        }

        public Stream OpenRead(string path)
        {
            var phys = ToPhysical(path);
            if (!File.Exists(phys)) throw new FileNotFoundException(path);
            return File.OpenRead(phys);
        }

        public Stream OpenWrite(string path)
        {
            var phys = ToPhysical(path);
            var dir = Path.GetDirectoryName(phys);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return File.Open(phys, FileMode.Create, FileAccess.Write, FileShare.Read);
        }

        public bool Exists(string path)
        {
            var phys = ToPhysical(path);
            return File.Exists(phys) || Directory.Exists(phys);
        }

        public IEnumerable<VfsFileInfo> Enumerate(string path)
        {
            var phys = ToPhysical(path);
            if (!Directory.Exists(phys)) return Enumerable.Empty<VfsFileInfo>();
            var dirs = Directory.GetDirectories(phys).Select(d => new VfsFileInfo(Path.GetFileName(d), true, 0, Directory.GetLastWriteTimeUtc(d)));
            var files = Directory.GetFiles(phys).Select(f => new VfsFileInfo(Path.GetFileName(f), false, new FileInfo(f).Length, File.GetLastWriteTimeUtc(f)));
            return dirs.Concat(files);
        }

        public VfsFileInfo? GetFileInfo(string path)
        {
            var phys = ToPhysical(path);
            if (Directory.Exists(phys)) return new VfsFileInfo(Path.GetFileName(phys), true, 0, Directory.GetLastWriteTimeUtc(phys));
            if (File.Exists(phys)) return new VfsFileInfo(Path.GetFileName(phys), false, new FileInfo(phys).Length, File.GetLastWriteTimeUtc(phys));
            return null;
        }

        public void CreateDirectory(string path)
        {
            var phys = ToPhysical(path);
            Directory.CreateDirectory(phys);
        }

        public void Delete(string path)
        {
            var phys = ToPhysical(path);
            if (Directory.Exists(phys)) Directory.Delete(phys, true);
            if (File.Exists(phys)) File.Delete(phys);
        }

        public byte[] ReadAllBytes(string path)
        {
            var phys = ToPhysical(path);
            return File.ReadAllBytes(phys);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            var phys = ToPhysical(path);
            var dir = Path.GetDirectoryName(phys);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(phys, data);
        }
    }
}
