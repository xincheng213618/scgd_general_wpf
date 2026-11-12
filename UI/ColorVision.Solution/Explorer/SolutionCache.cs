using log4net;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.Globalization;
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
        private const int CacheWriteBatchSize = 128;
        private const string EntryColumns = "FullPath, ParentPath, Name, IsDirectory, Extension, FileSize, LastWriteTicks";
        private const string SubtreePredicate = "(FullPath = @cachePath OR FullPath LIKE @cacheDescendants ESCAPE '!')";

        private readonly SqlSugarClient _db;
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly object _lock = new();
        private bool _disposed;

        public SolutionCache(string solutionFilePath)
        {
            _dbPath = Path.GetFullPath(solutionFilePath + ".cache.db");
            _connectionString = $"Data Source={_dbPath}";
            _db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = _connectionString,
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true
            });

            EnsureSchema();
        }

        private void EnsureSchema()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA table_info(file_tree_cache)";
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasPrimaryKey = false;
            using (SqliteDataReader reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    string name = reader.GetString(1);
                    columns.Add(name);
                    if (name.Equals(nameof(FileTreeCacheEntry.FullPath), StringComparison.OrdinalIgnoreCase))
                        hasPrimaryKey = reader.GetInt64(5) != 0;
                }
            }

            // Reuse an existing cache without repeating ORM schema discovery on every startup.
            // Keep CodeFirst for first creation and for older schemas with missing columns/keys.
            if (!hasPrimaryKey || !columns.IsSupersetOf([
                nameof(FileTreeCacheEntry.FullPath), nameof(FileTreeCacheEntry.ParentPath),
                nameof(FileTreeCacheEntry.Name), nameof(FileTreeCacheEntry.IsDirectory),
                nameof(FileTreeCacheEntry.Extension), nameof(FileTreeCacheEntry.FileSize),
                nameof(FileTreeCacheEntry.LastWriteTicks)]))
                _db.CodeFirst.InitTables<FileTreeCacheEntry>();

            // Directory expansion must read only that directory's rows, even in a large cache.
            // Run after schema repair so existing caches receive the index without a rebuild.
            command.CommandText = "CREATE INDEX IF NOT EXISTS IX_file_tree_cache_ParentPath_IsDirectory_Name ON file_tree_cache (ParentPath, IsDirectory DESC, Name)";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Check if cache exists and is not empty.
        /// </summary>
        public bool HasCache()
        {
            if (_disposed) return false;
            lock (_lock)
            {
                if (_disposed) return false;
                try
                {
                    if (!File.Exists(_dbPath)) return false;
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();
                    using var command = connection.CreateCommand();
                    command.CommandText = "SELECT EXISTS(SELECT 1 FROM file_tree_cache)";
                    return Convert.ToInt64(command.ExecuteScalar()) != 0;
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
                if (_disposed) return new List<FileTreeCacheEntry>();
                try
                {
                    return ReadChildren(parentPath);
                }
                catch (Exception ex)
                {
                    log.Warn($"读取缓存失败: {ex.Message}");
                    return new List<FileTreeCacheEntry>();
                }
            }
        }

        private List<FileTreeCacheEntry> ReadChildren(string parentPath)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT {EntryColumns} FROM file_tree_cache WHERE ParentPath = $parent ORDER BY IsDirectory DESC, Name";
            command.Parameters.AddWithValue("$parent", parentPath);
            using SqliteDataReader reader = command.ExecuteReader();
            var entries = new List<FileTreeCacheEntry>();
            while (reader.Read())
                entries.Add(ReadEntry(reader, reader.GetString(0)));
            return entries;
        }

        private static FileTreeCacheEntry ReadEntry(IDataRecord row, string fullPath) => new()
        {
            FullPath = fullPath,
            ParentPath = row.GetString(1),
            Name = row.GetString(2),
            IsDirectory = row.GetBoolean(3),
            Extension = row.IsDBNull(4) ? string.Empty : row.GetString(4),
            FileSize = row.IsDBNull(5) ? 0 : row.GetInt64(5),
            LastWriteTicks = row.IsDBNull(6) ? 0 : row.GetInt64(6),
        };

        public List<FileTreeCacheEntry> Search(
            string[] keywords,
            int maxResults = 500,
            string? rootPath = null) => Search(keywords, maxResults, rootPath, CancellationToken.None);

        internal List<FileTreeCacheEntry> Search(
            string[] keywords,
            int maxResults,
            string? rootPath,
            CancellationToken cancellationToken,
            string? nameQuery = null,
            Predicate<string>? includesPath = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_disposed || keywords.Length == 0) return new List<FileTreeCacheEntry>();
            lock (_lock)
            {
                if (_disposed) return new List<FileTreeCacheEntry>();
                try
                {
                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ISugarQueryable<FileTreeCacheEntry> query = CreateSearchQuery(keywords, rootPath, nameQuery)
                            .OrderBy(entry => entry.IsDirectory, OrderByType.Desc)
                            .OrderBy(entry => entry.Name);
                        List<FileTreeCacheEntry> entries = includesPath == null
                            ? query.Take(maxResults).ToList()
                            : ReadFilteredSearchEntries(query, maxResults, includesPath, cancellationToken);
                        List<FileTreeCacheEntry>? staleEntries = null;
                        foreach (FileTreeCacheEntry entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!(entry.IsDirectory ? Directory.Exists(entry.FullPath) : File.Exists(entry.FullPath)))
                                (staleEntries ??= new List<FileTreeCacheEntry>()).Add(entry);
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (staleEntries == null) return entries;

                        RemoveStaleSearchEntries(staleEntries, cancellationToken);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.Warn($"搜索缓存失败: {ex.Message}");
                    return new List<FileTreeCacheEntry>();
                }
            }
        }

        private List<FileTreeCacheEntry> ReadFilteredSearchEntries(ISugarQueryable<FileTreeCacheEntry> query,
            int maxResults, Predicate<string> includesPath, CancellationToken cancellationToken)
        {
            var sql = query.Select(EntryColumns).ToSql();
            using IDataReader reader = _db.Ado.GetDataReader(sql.Key, sql.Value);
            var entries = new List<FileTreeCacheEntry>();
            while (entries.Count < maxResults && reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                string fullPath = reader.GetString(0);
                if (includesPath(fullPath))
                    entries.Add(ReadEntry(reader, fullPath));
            }
            return entries;
        }

        private void RemoveStaleSearchEntries(List<FileTreeCacheEntry> staleEntries, CancellationToken cancellationToken)
        {
            try
            {
                _db.Ado.BeginTran();
                foreach (FileTreeCacheEntry directory in staleEntries.Where(entry => entry.IsDirectory))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    RemoveSubtreeEntries(directory.FullPath);
                }

                foreach (string[] paths in staleEntries.Where(entry => !entry.IsDirectory)
                    .Select(entry => entry.FullPath).Chunk(CacheWriteBatchSize))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _db.Deleteable<FileTreeCacheEntry>().Where(entry => paths.Contains(entry.FullPath)).ExecuteCommand();
                }
                cancellationToken.ThrowIfCancellationRequested();
                _db.Ado.CommitTran();
            }
            catch
            {
                try { _db.Ado.RollbackTran(); } catch { }
                throw;
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
                if (_disposed) return;
                try
                {
                    _db.Ado.BeginTran();
                    _db.Deleteable<FileTreeCacheEntry>().ExecuteCommand();

                    ScanDirectory(new DirectoryInfo(rootPath));

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

        private void ScanDirectory(DirectoryInfo dirInfo)
        {
            foreach (FileTreeCacheEntry[] batch in EnumerateDirectoryEntries(dirInfo).Chunk(CacheWriteBatchSize))
                _db.Insertable(batch).ExecuteCommand();
        }

        private static IEnumerable<FileTreeCacheEntry> EnumerateDirectoryEntries(DirectoryInfo dirInfo)
        {
            DirectoryInfo[] subDirectories;
            try
            {
                subDirectories = dirInfo.GetDirectories();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                yield break;
            }

            foreach (DirectoryInfo subDirectory in subDirectories)
            {
                FileTreeCacheEntry entry;
                bool scanChildren;
                try
                {
                    FileAttributes attributes = subDirectory.Attributes;
                    if ((attributes & FileAttributes.Hidden) != 0)
                        continue;

                    entry = CreateDirectoryEntry(subDirectory, dirInfo.FullName);
                    scanChildren = (attributes & FileAttributes.ReparsePoint) == 0;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // Keep indexing sibling entries when one path changes or becomes inaccessible.
                    continue;
                }

                yield return entry;
                if (scanChildren)
                    foreach (FileTreeCacheEntry child in EnumerateDirectoryEntries(subDirectory))
                        yield return child;
            }

            FileInfo[] files;
            try
            {
                files = dirInfo.GetFiles();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                yield break;
            }

            foreach (FileInfo file in files)
            {
                FileTreeCacheEntry entry;
                try
                {
                    if (SolutionNodeFactory.IsInternalFile(file.Name))
                        continue;

                    entry = CreateFileEntry(file, dirInfo.FullName);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    // The file can disappear while an imported directory is still being copied.
                    continue;
                }
                yield return entry;
            }
        }

        /// <summary>
        /// Validate cache against actual file system for a directory.
        /// Returns true if cache matches file system, false if stale.
        /// </summary>
        public bool ValidateDirectory(string directoryPath) => GetValidatedChildren(directoryPath) != null;

        internal List<FileTreeCacheEntry>? GetValidatedChildren(string directoryPath)
        {
            if (_disposed) return null;
            lock (_lock)
            {
                if (_disposed) return null;
                try
                {
                    var dirInfo = new DirectoryInfo(directoryPath);
                    if (!dirInfo.Exists) return null;

                    var cachedChildren = ReadChildren(directoryPath);
                    // Older incremental writers could cache solution/backup files. Repair only
                    // those derived rows so a nonempty legacy cache can become valid again.
                    List<string> internalPaths = cachedChildren
                        .Where(entry => !entry.IsDirectory && SolutionNodeFactory.IsInternalFile(entry.Name))
                        .Select(entry => entry.FullPath)
                        .ToList();
                    if (internalPaths.Count > 0)
                    {
                        _db.Deleteable<FileTreeCacheEntry>().Where(entry => internalPaths.Contains(entry.FullPath)).ExecuteCommand();
                        cachedChildren.RemoveAll(entry => internalPaths.Contains(entry.FullPath));
                    }
                    var remaining = new Dictionary<string, bool>(cachedChildren.Count, StringComparer.OrdinalIgnoreCase);
                    foreach (var cached in cachedChildren)
                        if (!remaining.TryAdd(cached.FullPath, cached.IsDirectory)) return null;

                    foreach (FileSystemInfo entry in dirInfo.EnumerateFileSystemInfos())
                    {
                        bool isDirectory = entry is DirectoryInfo;
                        if (isDirectory ? (entry.Attributes & FileAttributes.Hidden) != 0 : SolutionNodeFactory.IsInternalFile(entry.Name))
                            continue;
                        if (!remaining.Remove(entry.FullName, out bool cachedIsDirectory) || cachedIsDirectory != isDirectory)
                            return null;
                    }

                    return remaining.Count == 0 ? cachedChildren : null;
                }
                catch
                {
                    return null;
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
                if (_disposed) return;
                try
                {
                    var fileInfo = new FileInfo(fullPath);
                    if (!fileInfo.Exists || SolutionNodeFactory.IsInternalFile(fileInfo.Name)) return;

                    FileTreeCacheEntry entry = CreateFileEntry(fileInfo, parentPath);

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
                if (_disposed) return;
                try
                {
                    var dirInfo = new DirectoryInfo(fullPath);
                    if (!dirInfo.Exists) return;

                    FileTreeCacheEntry entry = CreateDirectoryEntry(dirInfo, parentPath);

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

        public void AddDirectoryTree(string fullPath, string parentPath)
        {
            if (_disposed) return;
            lock (_lock)
            {
                if (_disposed) return;
                try
                {
                    var directoryInfo = new DirectoryInfo(fullPath);
                    if (!directoryInfo.Exists)
                        return;

                    _db.Ado.BeginTran();
                    RemoveSubtreeEntries(directoryInfo.FullName);
                    _db.Insertable(CreateDirectoryEntry(directoryInfo, parentPath)).ExecuteCommand();
                    if ((directoryInfo.Attributes & FileAttributes.ReparsePoint) == 0)
                        ScanDirectory(directoryInfo);
                    _db.Ado.CommitTran();
                }
                catch (Exception ex)
                {
                    try { _db.Ado.RollbackTran(); } catch { }
                    log.Warn($"缓存添加目录树失败: {ex.Message}");
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
                if (_disposed) return;
                try
                {
                    string normalizedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullPath));
                    if (string.Equals(normalizedPath, _dbPath, StringComparison.OrdinalIgnoreCase))
                        return;

                    bool isOwnSidecar = string.Equals(normalizedPath, _dbPath + "-journal", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalizedPath, _dbPath + "-wal", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(normalizedPath, _dbPath + "-shm", StringComparison.OrdinalIgnoreCase);
                    // A directory can use a sidecar name in DELETE journal mode.
                    // Skip file cleanup writes, but retain those real tree deletions.
                    if (isOwnSidecar && !_db.Queryable<FileTreeCacheEntry>().Any(entry => entry.FullPath == normalizedPath && entry.IsDirectory))
                        return;

                    RemoveSubtreeEntries(fullPath);
                }
                catch (Exception ex)
                {
                    log.Warn($"缓存删除失败: {ex.Message}");
                }
            }
        }

        private ISugarQueryable<FileTreeCacheEntry> CreateSearchQuery(
            IEnumerable<string> keywords,
            string? rootPath,
            string? nameQuery)
        {
            var query = _db.Queryable<FileTreeCacheEntry>();
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                string normalizedRootPath = Path.GetFullPath(rootPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                query = query.Where(SubtreePredicate, new
                {
                    cachePath = normalizedRootPath,
                    cacheDescendants = DescendantPattern(normalizedRootPath),
                });
            }
            List<string>? nameFilters = nameQuery != null ? [] : null;
            int termIndex = 0;
            foreach (string keyword in keywords.Where(keyword => !string.IsNullOrWhiteSpace(keyword)))
            {
                string parameterName = "@cacheTerm" + termIndex.ToString(CultureInfo.InvariantCulture);
                termIndex++;
                bool needsEscape = keyword.AsSpan().IndexOfAny('%', '_') >= 0;
                string operand = needsEscape ? $"{parameterName} ESCAPE '!'" : parameterName;
                string term = needsEscape ? EscapeLikePattern(keyword) : keyword;
                string nameFilter = $"Name LIKE {operand}";
                nameFilters?.Add(nameFilter);
                query = query.Where($"({nameFilter} OR FullPath LIKE {operand})",
                    new[] { new SugarParameter(parameterName, $"%{term}%") });
            }

            if (nameQuery != null && nameFilters is { Count: > 0 })
            {
                string allTermsInName = string.Join(" AND ", nameFilters);
                query = query.AddParameters(new { cacheExactName = nameQuery, cacheNamePrefix = EscapeLikePattern(nameQuery) + "%" })
                    .OrderBy($"CASE WHEN Name COLLATE NOCASE = @cacheExactName THEN 0 "
                        + $"WHEN Name LIKE @cacheNamePrefix ESCAPE '!' THEN 1 WHEN {allTermsInName} THEN 2 ELSE 3 END");
            }
            return query;
        }

        private void RemoveSubtreeEntries(string fullPath)
        {
            string normalizedPath = fullPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            _db.Ado.ExecuteCommand($"DELETE FROM file_tree_cache WHERE {SubtreePredicate}",
                new SugarParameter("@cachePath", normalizedPath),
                new SugarParameter("@cacheDescendants", DescendantPattern(normalizedPath)));
        }

        private static string DescendantPattern(string normalizedPath) =>
            EscapeLikePattern(normalizedPath) + Path.DirectorySeparatorChar + "%";

        private static string EscapeLikePattern(string text) => text
            .Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);

        private static FileTreeCacheEntry CreateFileEntry(FileInfo fileInfo, string parentPath)
        {
            return new FileTreeCacheEntry
            {
                FullPath = fileInfo.FullName,
                ParentPath = parentPath,
                Name = fileInfo.Name,
                IsDirectory = false,
                Extension = fileInfo.Extension,
                FileSize = fileInfo.Length,
                LastWriteTicks = fileInfo.LastWriteTimeUtc.Ticks,
            };
        }

        private static FileTreeCacheEntry CreateDirectoryEntry(
            DirectoryInfo directoryInfo,
            string parentPath)
        {
            return new FileTreeCacheEntry
            {
                FullPath = directoryInfo.FullName,
                ParentPath = parentPath,
                Name = directoryInfo.Name,
                IsDirectory = true,
                Extension = string.Empty,
                FileSize = 0,
                LastWriteTicks = directoryInfo.LastWriteTimeUtc.Ticks,
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                lock (_lock)
                {
                    _db?.Dispose();
                    using var poolKey = new SqliteConnection(_connectionString);
                    SqliteConnection.ClearPool(poolKey);
                }
            }
            GC.SuppressFinalize(this);
        }
    }
}
