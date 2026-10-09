using System;
using System.IO;
using SMC.BhoomiMalmatta.Business.Abstractions;

namespace SMC.BhoomiMalmatta.Business.Storage
{
    /// <summary>
    /// File store rooted OUTSIDE the web content folder (configure an absolute path, e.g. D:\SMC_Uploads). Only relative paths are
    /// stored in the database. Every path is validated so it cannot escape the root (no "..", no rooted paths, no drive letters).
    /// </summary>
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly string _root;
        public LocalFileStorage(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("File storage root is not configured.");
            _root = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public string Resolve(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) throw new InvalidOperationException("Empty path");
            var rel = relativePath.Replace('\\', '/');
            if (rel.StartsWith("/") || rel.IndexOf(':') >= 0 || rel.IndexOf('\0') >= 0) throw new InvalidOperationException("Invalid path");
            foreach (var seg in rel.Split('/')) if (seg == ".." || seg == ".") throw new InvalidOperationException("Invalid path");
            var full = Path.GetFullPath(Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar)));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Path escapes storage root");
            return full;
        }

        public void Save(string relativePath, byte[] content)
        {
            var full = Resolve(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, content);
        }
        public byte[] Read(string relativePath) { return File.ReadAllBytes(Resolve(relativePath)); }
        public bool Exists(string relativePath) { return File.Exists(Resolve(relativePath)); }
        public void Delete(string relativePath) { var f = Resolve(relativePath); if (File.Exists(f)) File.Delete(f); }
    }
}
