using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Adamantite.VFS
{
    public class VfsManager
    {
        readonly Dictionary<string, IFileSystem> _mounts = new(StringComparer.OrdinalIgnoreCase);
        readonly string _rootMountPoint = "/";

        public void Mount(string mountPoint, IFileSystem fs)
        {
            if (string.IsNullOrEmpty(mountPoint)) mountPoint = "/";
            mountPoint = NormalizeMountPoint(mountPoint);
            _mounts[mountPoint] = fs ?? throw new ArgumentNullException(nameof(fs));
        }

        public void Unmount(string mountPoint)
        {
            mountPoint = NormalizeMountPoint(mountPoint);
            _mounts.Remove(mountPoint);
        }

        string NormalizeMountPoint(string mp)
        {
            if (string.IsNullOrEmpty(mp)) return "/";
            mp = mp.Replace('\\', '/');
            if (!mp.StartsWith("/")) mp = "/" + mp;
            if (mp.Length > 1 && mp.EndsWith("/")) mp = mp.TrimEnd('/');
            return mp;
        }

        // resolve mount and local path
        (IFileSystem? fs, string localPath) Resolve(string path)
        {
            if (string.IsNullOrEmpty(path)) return (null, path);
            path = path.Replace('\\', '/');
            foreach (var kv in _mounts.OrderByDescending(k => k.Key.Length))
            {
                var mp = kv.Key;
                if (mp == "/")
                {
                    // root mount matches anything
                    return (kv.Value, path.TrimStart('/'));
                }
                if (path.StartsWith(mp.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase) || string.Equals(path, mp, StringComparison.OrdinalIgnoreCase))
                {
                    var local = path.Length > mp.Length ? path.Substring(mp.Length).TrimStart('/') : string.Empty;
                    return (kv.Value, local);
                }
            }
            return (null, path);
        }

        public Stream OpenRead(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new FileNotFoundException(path);
            return fs.OpenRead(local);
        }

        public Stream OpenWrite(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new FileNotFoundException(path);
            return fs.OpenWrite(local);
        }

        public bool Exists(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) return false;
            return fs.Exists(local);
        }

        public IEnumerable<VfsFileInfo> Enumerate(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) return Array.Empty<VfsFileInfo>();
            return fs.Enumerate(local);
        }

        public VfsFileInfo? GetFileInfo(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) return null;
            return fs.GetFileInfo(local);
        }

        public void CreateDirectory(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new DirectoryNotFoundException(path);
            fs.CreateDirectory(local);
        }

        public void Delete(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new FileNotFoundException(path);
            fs.Delete(local);
        }

        public byte[] ReadAllBytes(string path)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new FileNotFoundException(path);
            return fs.ReadAllBytes(local);
        }

        public void WriteAllBytes(string path, byte[] data)
        {
            var (fs, local) = Resolve(path);
            if (fs == null) throw new FileNotFoundException(path);
            fs.WriteAllBytes(local, data);
        }
    }
}
