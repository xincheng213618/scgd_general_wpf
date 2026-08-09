using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace ColorVision.Database
{
    public static class SqliteFileMaintenance
    {
        public static SqliteBackupFileResult CreateVerifiedBackup(
            string databasePath,
            string backupDirectoryName,
            string filePrefix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(backupDirectoryName);
            ArgumentException.ThrowIfNullOrWhiteSpace(filePrefix);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("SQLite 数据库文件不存在。", databasePath);

            string sourceCheck = QuickCheck(databasePath);
            if (!string.Equals(sourceCheck, "ok", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"源数据库完整性检查未通过：{sourceCheck}");

            string databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(databasePath))
                ?? throw new InvalidOperationException("无法确定 SQLite 数据库目录。");
            string backupDirectory = Path.Combine(databaseDirectory, backupDirectoryName);
            Directory.CreateDirectory(backupDirectory);
            string finalPath = Path.Combine(
                backupDirectory,
                $"{filePrefix}.backup-{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");
            string partialPath = finalPath + ".part";

            try
            {
                using (var source = OpenConnection(databasePath, SqliteOpenMode.ReadOnly))
                using (var destination = OpenConnection(partialPath, SqliteOpenMode.ReadWriteCreate))
                    source.BackupDatabase(destination);

                string backupCheck = QuickCheck(partialPath);
                if (!string.Equals(backupCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"备份完整性检查未通过：{backupCheck}");

                File.Move(partialPath, finalPath);
                return new SqliteBackupFileResult(finalPath, new FileInfo(finalPath).Length, backupCheck);
            }
            catch
            {
                if (File.Exists(partialPath))
                    File.Delete(partialPath);
                throw;
            }
        }

        public static string QuickCheck(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("SQLite 数据库文件不存在。", databasePath);

            using SqliteConnection connection = OpenConnection(databasePath, SqliteOpenMode.ReadOnly);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA quick_check;";
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        public static void CheckpointWal(string databasePath, string stage)
        {
            using SqliteConnection connection = OpenConnection(databasePath, SqliteOpenMode.ReadWrite);
            CheckpointWal(connection, stage);
        }

        public static void CheckpointWal(SqliteConnection connection, string stage)
        {
            ArgumentNullException.ThrowIfNull(connection);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            using SqliteDataReader reader = command.ExecuteReader();
            if (reader.Read() && reader.GetInt32(0) != 0)
                throw new InvalidOperationException($"{stage}无法截断 SQLite WAL，请关闭相关查询和写入后重试。");
        }

        public static SqliteVacuumResult VacuumAndCheck(string databasePath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("SQLite 数据库文件不存在。", databasePath);

            long beforeBytes = GetTotalStorageBytes(databasePath);
            EnsureVacuumFreeSpace(databasePath, beforeBytes);
            using (SqliteConnection connection = OpenConnection(databasePath, SqliteOpenMode.ReadWrite))
            {
                CheckpointWal(connection, "VACUUM 前");
                ExecuteNonQuery(connection, "VACUUM;");
                CheckpointWal(connection, "VACUUM 后");
                using SqliteCommand quickCheckCommand = connection.CreateCommand();
                quickCheckCommand.CommandText = "PRAGMA quick_check;";
                string quickCheck = Convert.ToString(quickCheckCommand.ExecuteScalar()) ?? string.Empty;
                if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"SQLite 完整性检查未通过：{quickCheck}");
            }

            return new SqliteVacuumResult(beforeBytes, GetTotalStorageBytes(databasePath), "ok");
        }

        public static void EnsureVacuumFreeSpace(string databasePath, long databaseBytes)
        {
            string fullPath = Path.GetFullPath(databasePath);
            string root = Path.GetPathRoot(fullPath)
                ?? throw new InvalidOperationException("无法确定 SQLite 数据库所在磁盘。");
            long requiredBytes = checked(databaseBytes * 2 + 512L * 1024 * 1024);
            long availableBytes = new DriveInfo(root).AvailableFreeSpace;
            if (availableBytes < requiredBytes)
            {
                throw new IOException(
                    $"SQLite VACUUM 可用磁盘空间不足，需要至少 {FormatSize(requiredBytes)}，当前可用 {FormatSize(availableBytes)}。");
            }
        }

        public static long GetTotalStorageBytes(string databasePath)
        {
            long total = File.Exists(databasePath) ? new FileInfo(databasePath).Length : 0;
            string walPath = databasePath + "-wal";
            string shmPath = databasePath + "-shm";
            if (File.Exists(walPath))
                total += new FileInfo(walPath).Length;
            if (File.Exists(shmPath))
                total += new FileInfo(shmPath).Length;
            return total;
        }

        public static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = Math.Max(0, bytes);
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:0.##} {units[unitIndex]}";
        }

        internal static SqliteConnection OpenConnection(string databasePath, SqliteOpenMode mode)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = mode,
                Pooling = false,
                DefaultTimeout = 30,
            }.ToString());
            connection.Open();
            ExecuteNonQuery(connection, "PRAGMA busy_timeout = 30000;");
            return connection;
        }

        internal static int ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return command.ExecuteNonQuery();
        }
    }

    public sealed record SqliteBackupFileResult(string FilePath, long FileSizeBytes, string IntegrityCheck);
    public sealed record SqliteVacuumResult(long BeforeBytes, long AfterBytes, string IntegrityCheck);
}
