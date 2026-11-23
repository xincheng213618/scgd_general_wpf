using ColorVision.UI;
using log4net;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Messages
{
    public static class MsgRecordDataBaseHelper
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MsgRecordDataBaseHelper));
        private static readonly object InitLocker = new();
        private static readonly HashSet<string> InitializedPaths = new(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim TerminalWrites = new(1, 1);

        public static event EventHandler<MsgRecord>? Inserted;

        private static SqlSugarClient CreateDb(string sqliteDbPath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = sqliteDbPath }.ToString(),
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        public static void EnsureDatabaseInitialized(MsgRecordManagerConfig? config = null)
        {
            config ??= ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>();
            EnsureDatabaseAtPath(Path.GetFullPath(config.SqliteDbPath));
        }

        private static void EnsureDatabaseAtPath(string sqliteDbPath)
        {
            lock (InitLocker)
            {
                if (InitializedPaths.Contains(sqliteDbPath) && File.Exists(sqliteDbPath)) return;
                Directory.CreateDirectory(Path.GetDirectoryName(sqliteDbPath)!);
                using var db = CreateDb(sqliteDbPath);
                db.CodeFirst.InitTables<MsgRecord>();
                InitializedPaths.Add(sqliteDbPath);
            }
        }

        public static void Insert(MsgRecord item)
        {
            if (item == null) return;
            try
            {
                string sqliteDbPath = ConfigService.Instance.GetRequiredService<MsgRecordManagerConfig>().SqliteDbPath;
                _ = InsertAndTrackTerminal(item, sqliteDbPath);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
        }

        internal static Task InsertAndTrackTerminal(MsgRecord item, string sqliteDbPath)
        {
            sqliteDbPath = Path.GetFullPath(sqliteDbPath);
            string messageId = item.MsgID;
            var terminal = new TaskCompletionSource<TerminalRecord>(TaskCreationOptions.RunContinuationsAsynchronously);

            void CaptureTerminal(MsgRecordState state)
            {
                if (MsgRecord.IsTerminal(state) && terminal.TrySetResult(new(state, item.MsgReturn, item.ReciveTime, DateTime.Now)))
                    item.TerminalStateCommitted -= CaptureTerminal;
            }

            item.TerminalStateCommitted += CaptureTerminal;
            int id;
            try
            {
                // Recover a reply or timeout that preceded registration on the database worker.
                CaptureTerminal(item.MsgRecordState);
                EnsureDatabaseAtPath(sqliteDbPath);
                using var db = CreateDb(sqliteDbPath);
                id = db.Insertable(item).ExecuteReturnIdentity();
                item.Id = id;
            }
            catch
            {
                item.TerminalStateCommitted -= CaptureTerminal;
                throw;
            }

            Task persistence = PersistTerminalAsync(sqliteDbPath, id, messageId, terminal.Task);
            Inserted?.Invoke(null, item);
            return persistence;
        }

        private static async Task PersistTerminalAsync(string sqliteDbPath, int id, string messageId, Task<TerminalRecord> completion)
        {
            // A fast response may already be available. Never write on the caller's thread.
            TerminalRecord terminal = await completion.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
            await TerminalWrites.WaitAsync().ConfigureAwait(false);
            try
            {
                using var db = CreateDb(sqliteDbPath);
                string replyJson = JsonConvert.SerializeObject(terminal.Reply);
                db.Updateable<MsgRecord>()
                    .SetColumns(_ => new MsgRecord
                    {
                        MsgRecordState = terminal.State,
                        MsgReturnJson = replyJson,
                        ReciveTime = terminal.ReceivedAt,
                        UpdateTime = terminal.UpdatedAt,
                    })
                    .Where(record => record.Id == id && record.MsgID == messageId)
                    .ExecuteCommand();
            }
            catch (Exception ex)
            {
                log.Error($"Failed to persist terminal MQTT record. MsgID={messageId}, State={terminal.State}.", ex);
            }
            finally
            {
                TerminalWrites.Release();
            }
        }

        private readonly record struct TerminalRecord(MsgRecordState State, MsgReturn? Reply, DateTime ReceivedAt, DateTime UpdatedAt);
    }
}
