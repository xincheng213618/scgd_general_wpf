using log4net;
using SqlSugar;
using System.IO;

namespace ColorVision.Solution.Explorer
{
    /// <summary>
    /// SQLite cached entry representing a file or directory in the solution tree.
    /// </summary>
    [SugarTable("file_tree_cache")]
    public class FileTreeCacheEntry
    {
        [SugarColumn(IsPrimaryKey = true)]
        public string FullPath { get; set; } = string.Empty;

        public string ParentPath { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsDirectory { get; set; }

        public string Extension { get; set; } = string.Empty;

        public long FileSize { get; set; }

        public long LastWriteTicks { get; set; }
    }

    /// <summary>
    /// SQLite-based cache for solution file tree structure.
    /// Stores directory listings to avoid repeated file system enumeration on solution open.
    /// Cache is stored alongside the .cvsln file as .cvsln.cache.db.
    /// </summary>
    public class SolutionCache : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SolutionCache));

        private readonly SqlSugarClient _db;
        private readonly string _dbPath;
        private readonly object _lock = new();
        private bool _disposed;

        public SolutionCache(string solutionFilePath)
        {
            _dbPath = solutionFilePath + ".cache.db";
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={_dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            _db.CodeFirst.InitTables<FileTreeCacheEntry>();
        }

        /// <summary>
        /// Check if cache exists and is not empty.
        /// </summary>
        public bool HasCache()
        {
            if (_disposed) return false;
            lock (_lock)
            {
                try
                {
                    return File.Exists(_dbPath) && _db.Queryable<FileTreeCacheEntry>().Any();
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Get cached children for a given parent directory path.
        /// </summary>
        public List<FileTreeCacheEntry> GetChildren(string parentPath)
        {
            if (_disposed) return new List<FileTreeCacheEntry>();
            lock (_lock)
            {
                try
                {
                    return _db.Queryable<FileTreeCacheEntry>()
                        .Where(e => e.ParentPath == parentPath)
                        .OrderBy(e => e.IsDirectory, OrderByType.Desc)
                        .OrderBy(e => e.Name)
                        .ToList();
                }
                catch (Exception ex)
                {
                    log.Warn($"读取缓存失败: {ex.Message}");
                    return new List<FileTreeCacheEntry>();
                }
            }
        }

        /// <summary>
        /// Rebuild the entire cache from the file system.
        /// Called on first load or when cache is invalidated.
        /// </summary>
        public void RebuildCache(string rootPath)
        {
            if (_disposed) return;
            lock (_lock)
            {
                try
                {
                    _db.Ado.BeginTran();
                    _db.Deleteable<FileTreeCacheEntry>().ExecuteCommand();

                    ScanDirectory(new DirectoryInfo(rootPath), rootPath);

                    _db.Ado.CommitTran();
                    log.Info($"缓存重建完成: {rootPath}");
                }
                catch (Exception ex)
                {
                    try { _db.Ado.RollbackTran(); } catch { }
                    log.Error($"缓存重建失败: {ex.Message}", ex);
                }
            }
        }

        private void ScanDirectory(DirectoryInfo dirInfo, string rootPath)
        {
            try
            {
                foreach (var subDir in dirInfo.GetDirectories())
                {
                    if ((subDir.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                        continue;

                    var entry = new FileTreeCacheEntry
                    {
                        FullPath = subDir.FullName,
                        ParentPath = dirInfo.FullName,
                        Name = subDir.Name,
                        IsDirectory = true,
                        Extension = string.Empty,
                        FileSize = 0,
                        LastWriteTicks = subDir.LastWriteTimeUtc.Ticks
                    };
                    _db.Insertable(entry).ExecuteCommand();

                    ScanDirectory(subDir, rootPath);
                }

                foreach (var file in dirInfo.GetFiles())
                {
                    if (file.Extension.Contains("cvsln")) continue;
                    if (file.Extension.Contains("cvproj")) continue;

                    var entry = new FileTreeCacheEntry
                    {
                        FullPath = file.FullName,
                        ParentPath = dirInfo.FullName,
                        Name = file.Name,
                        IsDirectory = false,
                        Extension = file.Extension,
                        FileSize = file.Length,
                        LastWriteTicks = file.LastWriteTimeUtc.Ticks
                    };
                    _db.Insertable(entry).ExecuteCommand();
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we can't access
            }
        }

        /// <summary>
        /// Validate cache against actual file system for a directory.
        /// Returns true if cache matches file system, false if stale.
        /// </summary>
        public bool ValidateDirectory(string directoryPath)
        {
            if (_disposed) return false;
            lock (_lock)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(directoryPath);
                    if (!dirInfo.Exists) return false;

                    var cachedChildren = _db.Queryable<FileTreeCacheEntry>()
                        .Where(e => e.ParentPath == directoryPath)
                        .ToList();
                    var actualDirs = dirInfo.GetDirectories()
                        .Where(d => (d.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                        .ToList();
                    var actualFiles = dirInfo.GetFiles()
                        .Where(f => !f.Extension.Contains("cvsln") && !f.Extension.Contains("cvproj"))
                        .ToList();

                    int expectedCount = actualDirs.Count + actualFiles.Count;
                    if (cachedChildren.Count != expectedCount)
                        return false;

                    foreach (var cached in cachedChildren)
                    {
                        if (cached.IsDirectory)
                        {
                            if (!Directory.Exists(cached.FullPath)) return false;
                        }
                        else
                        {
                            if (!File.Exists(cached.FullPath)) return false;
                        }
                    }

                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Add a file entry to the cache.
        /// </summary>
        public void AddFile(string fullPath, string parentPath)
        {
            if (_disposed) return;
            lock (_lock)
            {
                try
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (!fileInfo.Exists) return;

                    var entry = new FileTreeCacheEntry
                    {
                        FullPath = fullPath,
                        ParentPath = parentPath,
                        Name = fileInfo.Name,
                        IsDirectory = false,
                        Extension = fileInfo.Extension,
                        FileSize = fileInfo.Length,
                        LastWriteTicks = fileInfo.LastWriteTimeUtc.Ticks
                    };

                    // Upsert
                    if (_db.Queryable<FileTreeCacheEntry>().Any(e => e.FullPath == fullPath))
                        _db.Updateable(entry).ExecuteCommand();
                    else
                        _db.Insertable(entry).ExecuteCommand();
                }
                catch (Exception ex)
                {
                    log.Warn($"缓存添加文件失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Add a directory entry to the cache.
        /// </summary>
        public void AddDirectory(string fullPath, string parentPath)
        {
            if (_disposed) return;
            lock (_lock)
            {
                try
                {
                    var dirInfo = new DirectoryInfo(fullPath);
                    if (!dirInfo.Exists) return;

                    var entry = new FileTreeCacheEntry
                    {
                        FullPath = fullPath,
                        ParentPath = parentPath,
                        Name = dirInfo.Name,
                        IsDirectory = true,
                        Extension = string.Empty,
                        FileSize = 0,
                        LastWriteTicks = dirInfo.LastWriteTimeUtc.Ticks
                    };

                    if (_db.Queryable<FileTreeCacheEntry>().Any(e => e.FullPath == fullPath))
                        _db.Updateable(entry).ExecuteCommand();
                    else
                        _db.Insertable(entry).ExecuteCommand();
                }
                catch (Exception ex)
                {
                    log.Warn($"缓存添加目录失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Remove an entry from the cache.
        /// </summary>
        public void Remove(string fullPath)
        {
            if (_disposed) return;
            lock (_lock)
            {
                try
                {
                    _db.Deleteable<FileTreeCacheEntry>()
                        .Where(e => e.FullPath == fullPath || e.ParentPath.StartsWith(fullPath))
                        .ExecuteCommand();
                }
                catch (Exception ex)
                {
                    log.Warn($"缓存删除失败: {ex.Message}");
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                lock (_lock)
                {
                    _db?.Dispose();
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
