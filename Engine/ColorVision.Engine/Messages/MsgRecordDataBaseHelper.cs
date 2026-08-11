#pragma warning disable CS8625
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Engine.Messages
{
    public static class MsgRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MsgRecordDataBaseHelper));
        private static readonly object InitLocker = new();
        private static readonly HashSet<string> InitializedDatabasePaths = new(StringComparer.OrdinalIgnoreCase);

        public static event EventHandler<MsgRecord> Inserted;
        internal static event EventHandler<MsgRecordInsertedEventArgs>? InsertedForDatabase;

        private static SqlSugarClient CreateDb(string sqliteDbPath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={sqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public static string EnsureDatabaseInitialized(MsgRecordManagerConfig? config = null)
        {
            config ??= ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            return EnsureDatabaseInitialized(config.SqliteDbPath);
        }

        internal static string EnsureDatabaseInitialized(string sqliteDbPath)
        {
            string normalizedPath = NormalizeDatabasePath(sqliteDbPath);

            lock (InitLocker)
            {
                if (InitializedDatabasePaths.Contains(normalizedPath) && File.Exists(normalizedPath))
                    return normalizedPath;

                InitializedDatabasePaths.Remove(normalizedPath);

                string? directoryPath = Path.GetDirectoryName(normalizedPath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using var db = CreateDb(normalizedPath);
                db.CodeFirst.InitTables<MsgRecord>();
                InitializedDatabasePaths.Add(normalizedPath);
                return normalizedPath;
            }
        }

        internal static string NormalizeDatabasePath(string sqliteDbPath)
        {
            if (string.IsNullOrWhiteSpace(sqliteDbPath))
                throw new ArgumentException("SQLite database path cannot be empty.", nameof(sqliteDbPath));

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(sqliteDbPath.Trim()));
        }

        public static void Insert(MsgRecord item)
        {
            try
            {
                if (item == null) return;

                MsgRecordManagerConfig configSnapshot = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
                Insert(item, configSnapshot);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                return;
            }
        }

        internal static void Insert(MsgRecord item, MsgRecordManagerConfig configSnapshot)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(configSnapshot);

            string databasePath = EnsureDatabaseInitialized(configSnapshot.SqliteDbPath);
            using var db = CreateDb(databasePath);
            item.Id = db.Insertable(item).ExecuteReturnIdentity();
            item.MsgRecordStateChanged += (s, e) =>
            {
                using var updateDb = CreateDb(databasePath);
                updateDb.Updateable(item).ExecuteCommand();
            };

            InsertedForDatabase?.Invoke(null, new MsgRecordInsertedEventArgs(databasePath, item));
            Inserted?.Invoke(null, item);
        }
    }

    internal sealed class MsgRecordInsertedEventArgs : EventArgs
    {
        public MsgRecordInsertedEventArgs(string databasePath, MsgRecord item)
        {
            DatabasePath = databasePath;
            Item = item;
        }

        public string DatabasePath { get; }
        public MsgRecord Item { get; }
    }
}
