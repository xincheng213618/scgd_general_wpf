using ColorVision.UI;
using SqlSugar;
using System.IO;
using System.Linq;

namespace ColorVision.UI.Desktop.TimedButtons
{
    [SugarTable("timed_button_operation_stats")]
    internal sealed class TimedButtonOperationStatRecord
    {
        [SugarColumn(IsPrimaryKey = true, ColumnDataType = "TEXT")]
        public string OperationKey { get; set; } = string.Empty;

        public int SuccessCount { get; set; }
        public int WarmupCount { get; set; }
        public double WarmupElapsedMs { get; set; }
        public double LastElapsedMs { get; set; }
        public double AverageElapsedMs { get; set; }
        public double BestElapsedMs { get; set; }
        public double WorstElapsedMs { get; set; }
        public DateTime LastCompletedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public sealed class TimedButtonOperationStatsSqliteRepository : ITimedButtonOperationStatsRepository
    {
        private static readonly Lazy<TimedButtonOperationStatsSqliteRepository> InstanceHolder =
            new Lazy<TimedButtonOperationStatsSqliteRepository>(() => new TimedButtonOperationStatsSqliteRepository());

        private readonly object _syncRoot = new object();
        private readonly Dictionary<string, TimedButtonOperationStats> _cache = new Dictionary<string, TimedButtonOperationStats>(StringComparer.Ordinal);

        public static string DirectoryPath { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ColorVision",
            "Desktop");

        public static string DbPath { get; set; } = Path.Combine(DirectoryPath, "TimedButtonOperationStats.db");

        public static TimedButtonOperationStatsSqliteRepository GetInstance()
        {
            return InstanceHolder.Value;
        }

        private TimedButtonOperationStatsSqliteRepository()
        {
            Directory.CreateDirectory(DirectoryPath);

            using SqlSugarClient db = CreateDbClient();
            db.CodeFirst.InitTables<TimedButtonOperationStatRecord>();

            foreach (TimedButtonOperationStatRecord record in db.Queryable<TimedButtonOperationStatRecord>().ToList())
            {
                _cache[record.OperationKey] = ToStats(record);
            }
        }

        public TimedButtonOperationStats? Get(string operationKey)
        {
            string normalizedKey = NormalizeKey(operationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return null;
            }

            lock (_syncRoot)
            {
                if (_cache.TryGetValue(normalizedKey, out TimedButtonOperationStats? stats))
                {
                    return stats.Clone();
                }
            }

            return null;
        }

        public IReadOnlyList<TimedButtonOperationStatsEntry> GetAll()
        {
            lock (_syncRoot)
            {
                return _cache
                    .Select(item => new TimedButtonOperationStatsEntry
                    {
                        OperationKey = item.Key,
                        Stats = item.Value.Clone()
                    })
                    .OrderByDescending(item => item.Stats.LastCompletedAt)
                    .ThenBy(item => item.OperationKey, StringComparer.Ordinal)
                    .ToList();
            }
        }

        public TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatAsWarmupSample, bool persistImmediately)
        {
            _ = persistImmediately;

            string normalizedKey = NormalizeKey(operationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                throw new ArgumentException("Operation key cannot be empty.", nameof(operationKey));
            }

            lock (_syncRoot)
            {
                if (!_cache.TryGetValue(normalizedKey, out TimedButtonOperationStats? stats))
                {
                    stats = TryReadFromDatabase(normalizedKey) ?? new TimedButtonOperationStats();
                    _cache[normalizedKey] = stats;
                }

                stats.Record(elapsedMilliseconds, treatAsWarmupSample);
                WriteToDatabase(normalizedKey, stats);
                return new TimedButtonOperationRecordResult(stats.Clone(), treatAsWarmupSample);
            }
        }

        public bool Delete(string operationKey)
        {
            string normalizedKey = NormalizeKey(operationKey);
            if (string.IsNullOrEmpty(normalizedKey))
            {
                return false;
            }

            lock (_syncRoot)
            {
                bool removedFromCache = _cache.Remove(normalizedKey);

                using SqlSugarClient db = CreateDbClient();
                int deleted = db.Deleteable<TimedButtonOperationStatRecord>()
                    .Where(it => it.OperationKey == normalizedKey)
                    .ExecuteCommand();

                return removedFromCache || deleted > 0;
            }
        }

        public int Clear()
        {
            lock (_syncRoot)
            {
                int count = _cache.Count;
                if (count == 0)
                {
                    return 0;
                }

                _cache.Clear();

                using SqlSugarClient db = CreateDbClient();
                db.Deleteable<TimedButtonOperationStatRecord>().ExecuteCommand();
                return count;
            }
        }

        public static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        private TimedButtonOperationStats? TryReadFromDatabase(string operationKey)
        {
            using SqlSugarClient db = CreateDbClient();
            TimedButtonOperationStatRecord? record = db.Queryable<TimedButtonOperationStatRecord>()
                .First(it => it.OperationKey == operationKey);

            return record == null ? null : ToStats(record);
        }

        private void WriteToDatabase(string operationKey, TimedButtonOperationStats stats)
        {
            using SqlSugarClient db = CreateDbClient();
            TimedButtonOperationStatRecord record = ToRecord(operationKey, stats);

            bool exists = db.Queryable<TimedButtonOperationStatRecord>().Any(it => it.OperationKey == operationKey);
            if (exists)
            {
                db.Updateable(record).ExecuteCommand();
            }
            else
            {
                db.Insertable(record).ExecuteCommand();
            }
        }

        private static string NormalizeKey(string operationKey)
        {
            return operationKey?.Trim() ?? string.Empty;
        }

        private static TimedButtonOperationStats ToStats(TimedButtonOperationStatRecord record)
        {
            return new TimedButtonOperationStats
            {
                SuccessCount = record.SuccessCount,
                WarmupCount = record.WarmupCount,
                WarmupElapsedMs = record.WarmupElapsedMs,
                LastElapsedMs = record.LastElapsedMs,
                AverageElapsedMs = record.AverageElapsedMs,
                BestElapsedMs = record.BestElapsedMs,
                WorstElapsedMs = record.WorstElapsedMs,
                LastCompletedAt = record.LastCompletedAt
            };
        }

        private static TimedButtonOperationStatRecord ToRecord(string operationKey, TimedButtonOperationStats stats)
        {
            return new TimedButtonOperationStatRecord
            {
                OperationKey = operationKey,
                SuccessCount = stats.SuccessCount,
                WarmupCount = stats.WarmupCount,
                WarmupElapsedMs = stats.WarmupElapsedMs,
                LastElapsedMs = stats.LastElapsedMs,
                AverageElapsedMs = stats.AverageElapsedMs,
                BestElapsedMs = stats.BestElapsedMs,
                WorstElapsedMs = stats.WorstElapsedMs,
                LastCompletedAt = stats.LastCompletedAt,
                UpdatedAt = DateTime.Now
            };
        }
    }

    public sealed class TimedButtonOperationStatsInitializer : InitializerBase
    {
        public override int Order => 12;
        public override string Name => nameof(TimedButtonOperationStatsInitializer);

        public override Task InitializeAsync()
        {
            TimedButtonOperationStatsRepositoryProvider.SetRepository(TimedButtonOperationStatsSqliteRepository.GetInstance());
            return Task.CompletedTask;
        }
    }
}