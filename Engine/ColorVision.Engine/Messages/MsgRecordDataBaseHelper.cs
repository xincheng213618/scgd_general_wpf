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

                MessageDatabaseWriteTarget writeTarget = MessagesListManager.GetInstance().CaptureDatabaseWriteTarget();
                Insert(item, writeTarget);
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

            Insert(item, EnsureDatabaseInitialized(configSnapshot.SqliteDbPath));
        }

        internal static Action CreateInsertAction(MsgRecord item, string databasePath)
        {
            ArgumentNullException.ThrowIfNull(item);
            string capturedPath = NormalizeDatabasePath(databasePath);
            return () => Insert(item, capturedPath);
        }

        internal static Action CreateInsertAction(MsgRecord item, MessageDatabaseWriteTarget writeTarget)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(writeTarget);
            return () => Insert(item, writeTarget);
        }

        private static void Insert(MsgRecord item, MessageDatabaseWriteTarget writeTarget)
        {
            Insert(item, writeTarget.DatabasePath, writeTarget);
        }

        internal static void Insert(MsgRecord item, string databasePath)
        {
            Insert(item, databasePath, null);
        }

        private static void Insert(MsgRecord item, string databasePath, MessageDatabaseWriteTarget? writeTarget)
        {
            ArgumentNullException.ThrowIfNull(item);
            databasePath = EnsureDatabaseInitialized(databasePath);
            using var db = CreateDb(databasePath);
            item.Id = db.Insertable(item).ExecuteReturnIdentity();
            item.MsgRecordStateChanged += (s, e) =>
            {
                using var updateDb = CreateDb(databasePath);
                updateDb.Updateable(item).ExecuteCommand();
            };

            InsertedForDatabase?.Invoke(null, new MsgRecordInsertedEventArgs(databasePath, item, writeTarget));
            Inserted?.Invoke(null, item);
        }
    }

    internal sealed class MessageDatabaseWriteTarget
    {
        internal MessageDatabaseWriteTarget(
            string databasePath,
            long generation,
            object stateReference,
            OrderByType orderByType)
        {
            DatabasePath = databasePath;
            Generation = generation;
            StateReference = stateReference;
            OrderByType = orderByType;
        }

        public string DatabasePath { get; }
        public long Generation { get; }
        public object StateReference { get; }
        public OrderByType OrderByType { get; }
    }

    internal sealed class MsgRecordInsertedEventArgs : EventArgs
    {
        public MsgRecordInsertedEventArgs(
            string databasePath,
            MsgRecord item,
            MessageDatabaseWriteTarget? writeTarget)
        {
            DatabasePath = databasePath;
            Item = item;
            WriteTarget = writeTarget;
        }

        public string DatabasePath { get; }
        public MsgRecord Item { get; }
        public MessageDatabaseWriteTarget? WriteTarget { get; }
    }
}
