using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Solution.MultiImageViewer;

public sealed class ThumbnailCacheMaintenanceSnapshot
{
    internal ThumbnailCacheMaintenanceSnapshot(string path, bool exists, int count, long size, long writeVersion,
        string signature, IReadOnlyList<ThumbnailCacheManager.CacheFileState> files, string? error = null)
    {
        FilePath = path;
        Exists = exists;
        EntryCount = count;
        SizeBytes = size;
        WriteVersion = writeVersion;
        Signature = signature;
        Files = files;
        Error = error;
    }

    public string FilePath { get; }
    public bool Exists { get; }
    public int EntryCount { get; }
    public long SizeBytes { get; }
    public string? Error { get; }
    public bool CanCleanup => Error == null && Exists && EntryCount > 0;
    internal long WriteVersion { get; }
    internal string Signature { get; }
    internal IReadOnlyList<ThumbnailCacheManager.CacheFileState> Files { get; }
}

public sealed record ThumbnailCacheMaintenanceResult(bool Succeeded, bool RequiresRescan, int DeletedEntryCount, long ReleasedBytes, string Message);

public partial class ThumbnailCacheManager
{
    private static long _cacheWriteVersion;
    private static long _cacheMaintenanceGeneration;

    /// <summary>Inspects only an existing database; does not initialize the singleton, directory or schema.</summary>
    public static ThumbnailCacheMaintenanceSnapshot ScanCacheForMaintenance() => ScanCacheForMaintenanceAtPath(SqliteDbPath);

    // Kept separate so isolated tests can exercise a disposable database, never the user's cache.
    internal static ThumbnailCacheMaintenanceSnapshot ScanCacheForMaintenanceAtPath(string path)
    {
        lock (_locker)
        {
            try
            {
                path = Path.GetFullPath(path);
                if (!File.Exists(path))
                    return new(path, false, 0, 0, _cacheWriteVersion, string.Empty, Array.Empty<CacheFileState>());
                using IDisposable lease = PinCachePath(path);
                IReadOnlyList<CacheFileState> before = ReadCacheFiles(path);
                using SqliteConnection connection = OpenMaintenanceConnection(path, SqliteOpenMode.ReadOnly);
                (int count, string signature) = ReadCacheSignature(connection);
                IReadOnlyList<CacheFileState> after = ReadCacheFiles(path);
                if (!before.SequenceEqual(after))
                    throw new IOException("缓存在扫描期间发生变化，请重新扫描。");
                return new(path, true, count, after.Sum(file => file.Length), _cacheWriteVersion, signature, after);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return new(path, File.Exists(path), 0, 0, _cacheWriteVersion, string.Empty, Array.Empty<CacheFileState>(), exception.Message);
            }
        }
    }

    /// <summary>Clears exactly the confirmed cache state; changed or busy databases are left intact.</summary>
    public static ThumbnailCacheMaintenanceResult ClearCacheForMaintenance(ThumbnailCacheMaintenanceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_locker)
        {
            if (!snapshot.CanCleanup)
                return new(false, false, 0, 0, snapshot.Error ?? "没有已扫描且可清理的缩略图缓存。");
            bool committed = false;
            int deleted = 0;
            try
            {
                using IDisposable lease = PinCachePath(snapshot.FilePath);
                _ = ReadCacheFiles(snapshot.FilePath);
                using SqliteConnection connection = OpenMaintenanceConnection(snapshot.FilePath, SqliteOpenMode.ReadWrite);
                using SqliteTransaction transaction = connection.BeginTransaction();
                IReadOnlyList<CacheFileState> currentFiles = ReadCacheFiles(snapshot.FilePath);
                (int count, string signature) = ReadCacheSignature(connection, transaction);
                if (_cacheWriteVersion != snapshot.WriteVersion || count != snapshot.EntryCount
                    || signature != snapshot.Signature || !snapshot.Files.SequenceEqual(currentFiles))
                    return new(false, true, 0, 0, "缩略图缓存在扫描后发生变化，请重新扫描后再清理。");

                // Earlier thumbnail generation may finish later, but must not refill this cleared snapshot.
                _cacheMaintenanceGeneration++;
                using SqliteCommand delete = connection.CreateCommand();
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM ThumbnailCache;";
                deleted = delete.ExecuteNonQuery();
                transaction.Commit();
                committed = true;
                _cacheWriteVersion++;

                string message = $"已清理 {deleted:N0} 个缩略图缓存，原始图片未修改。";
                try
                {
                    using SqliteCommand vacuum = connection.CreateCommand();
                    vacuum.CommandText = "VACUUM;";
                    vacuum.ExecuteNonQuery();
                }
                catch (SqliteException exception)
                {
                    // DELETE has committed. A VACUUM failure must not be presented as a rollback.
                    message += $" 空间整理未完成：{exception.Message}";
                }
                long remaining = ReadCacheFiles(snapshot.FilePath).Sum(file => file.Length);
                return new(true, false, deleted, Math.Max(0, snapshot.SizeBytes - remaining), message);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                if (committed)
                    return new(true, false, deleted, 0, $"已清理 {deleted:N0} 个缩略图缓存；空间整理或统计未完成：{exception.Message}");
                return new(false, false, 0, 0, $"缩略图缓存未能完成清理：{exception.Message}");
            }
        }
    }

    private static SqliteConnection OpenMaintenanceConnection(string path, SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
            DefaultTimeout = 1,
        }.ToString());
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static (int Count, string Signature) ReadCacheSignature(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        // Identity and metadata are sufficient for local saves (all also advance WriteVersion).
        // File/sidecar stamps additionally detect writes from other processes.
        command.CommandText = "SELECT Id, FilePath, FileLastModified, ThumbnailWidth, ThumbnailHeight, OriginalWidth, OriginalHeight, FileSize, CreateDate, length(ThumbnailData) FROM ThumbnailCache ORDER BY Id;";
        using SqliteDataReader reader = command.ExecuteReader();
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int count = 0;
        while (reader.Read())
        {
            count++;
            for (int column = 0; column < reader.FieldCount; column++)
            {
                string value = reader.IsDBNull(column) ? "<null>" : Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture) ?? string.Empty;
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                hash.AppendData(BitConverter.GetBytes(bytes.Length));
                hash.AppendData(bytes);
            }
        }
        return (count, Convert.ToHexString(hash.GetHashAndReset()));
    }

    private static IReadOnlyList<CacheFileState> ReadCacheFiles(string path)
    {
        var result = new List<CacheFileState>();
        foreach (string suffix in new[] { string.Empty, "-wal", "-shm", "-journal" })
        {
            string filePath = path + suffix;
            var file = new FileInfo(filePath);
            if (!file.Exists)
            {
                result.Add(new(filePath, false, 0, DateTime.MinValue, DateTime.MinValue));
                continue;
            }
            if ((file.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
                throw new IOException("缩略图缓存包含链接或非普通文件，已保护该路径。");
            result.Add(new(filePath, true, file.Length, file.CreationTimeUtc, file.LastWriteTimeUtc));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static IDisposable PinCachePath(string path)
    {
        var paths = new Stack<(string Path, bool Directory)>();
        paths.Push((path, false));
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(path)!); directory != null; directory = directory.Parent)
            paths.Push((directory.FullName, true));
        var lease = new CachePathLease();
        try
        {
            while (paths.Count > 0)
            {
                (string target, bool directory) = paths.Pop();
                SafeFileHandle handle = OpenCachePath(target, 0x80, 3, IntPtr.Zero, 3, 0x00200000 | (directory ? 0x02000000u : 0), IntPtr.Zero);
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw new Win32Exception(error);
                }
                lease.Handles.Add(handle);
                if (!GetCachePathAttributes(handle, 9, out CachePathAttributes attributes, 8))
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                if (((FileAttributes)attributes.Attributes & FileAttributes.ReparsePoint) != 0
                    || (((FileAttributes)attributes.Attributes & FileAttributes.Directory) != 0) != directory)
                    throw new IOException("缩略图缓存路径包含链接或目标类型已变化。");
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal sealed record CacheFileState(string Path, bool Exists, long Length, DateTime CreationTimeUtc, DateTime LastWriteTimeUtc);
    private sealed class CachePathLease : IDisposable
    {
        public List<SafeFileHandle> Handles { get; } = new();
        public void Dispose()
        {
            for (int index = Handles.Count - 1; index >= 0; index--)
                Handles[index].Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CachePathAttributes { public uint Attributes; public uint ReparseTag; }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenCachePath(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandleEx", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCachePathAttributes(SafeFileHandle handle, int informationClass, out CachePathAttributes information, uint size);
}
