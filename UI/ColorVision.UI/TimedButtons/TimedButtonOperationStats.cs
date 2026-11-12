#pragma warning disable CA1513,CA1716,CA1863

namespace ColorVision.UI
{
    public sealed class TimedButtonOperationStats
    {
        public int SuccessCount { get; set; }
        public int WarmupCount { get; set; }
        public double WarmupElapsedMs { get; set; }
        public double LastElapsedMs { get; set; }
        public double AverageElapsedMs { get; set; }
        public double BestElapsedMs { get; set; }
        public double WorstElapsedMs { get; set; }
        public DateTime LastCompletedAt { get; set; }

        public void Record(double elapsedMilliseconds, bool treatAsWarmupSample)
        {
            elapsedMilliseconds = Math.Max(0, elapsedMilliseconds);
            LastCompletedAt = DateTime.Now;

            if (treatAsWarmupSample)
            {
                WarmupCount++;
                WarmupElapsedMs = elapsedMilliseconds;
                return;
            }

            SuccessCount++;
            LastElapsedMs = elapsedMilliseconds;

            if (SuccessCount == 1)
            {
                AverageElapsedMs = elapsedMilliseconds;
                BestElapsedMs = elapsedMilliseconds;
                WorstElapsedMs = elapsedMilliseconds;
                return;
            }

            AverageElapsedMs = ((AverageElapsedMs * (SuccessCount - 1)) + elapsedMilliseconds) / SuccessCount;
            BestElapsedMs = Math.Min(BestElapsedMs, elapsedMilliseconds);
            WorstElapsedMs = Math.Max(WorstElapsedMs, elapsedMilliseconds);
        }

        public TimedButtonOperationStats Clone()
        {
            return new TimedButtonOperationStats
            {
                SuccessCount = SuccessCount,
                WarmupCount = WarmupCount,
                WarmupElapsedMs = WarmupElapsedMs,
                LastElapsedMs = LastElapsedMs,
                AverageElapsedMs = AverageElapsedMs,
                BestElapsedMs = BestElapsedMs,
                WorstElapsedMs = WorstElapsedMs,
                LastCompletedAt = LastCompletedAt
            };
        }
    }

    public sealed class TimedButtonOperationStatsConfig : IConfig
    {
        public Dictionary<string, TimedButtonOperationStats> Records { get; set; } = new Dictionary<string, TimedButtonOperationStats>();
    }

    public sealed class TimedButtonOperationStatsEntry
    {
        public string OperationKey { get; set; } = string.Empty;
        public TimedButtonOperationStats Stats { get; set; } = new TimedButtonOperationStats();
    }

    public sealed class TimedButtonOperationStatsChangedEventArgs : EventArgs
    {
        public TimedButtonOperationStatsChangedEventArgs(string? operationKey)
        {
            OperationKey = string.IsNullOrWhiteSpace(operationKey) ? null : operationKey.Trim();
        }

        public string? OperationKey { get; }
        public bool IsGlobalChange => string.IsNullOrWhiteSpace(OperationKey);
    }

    public interface ITimedButtonOperationStatsRepository
    {
        TimedButtonOperationStats? Get(string operationKey);
        IReadOnlyList<TimedButtonOperationStatsEntry> GetAll();
        TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatAsWarmupSample, bool persistImmediately);
        bool Delete(string operationKey);
        int Clear();
    }

    public interface ITimedButtonOperationStatsWindowLauncher
    {
        bool CanOpen { get; }
        void OpenWindow(string? operationKey = null);
    }

    public readonly struct TimedButtonOperationRecordResult
    {
        public TimedButtonOperationRecordResult(TimedButtonOperationStats stats, bool wasWarmup)
        {
            Stats = stats;
            WasWarmup = wasWarmup;
        }

        public TimedButtonOperationStats Stats { get; }
        public bool WasWarmup { get; }
    }

    public static class TimedButtonOperationStatsManager
    {
        public static event EventHandler<TimedButtonOperationStatsChangedEventArgs>? StatsChanged;

        public static IReadOnlyList<TimedButtonOperationStatsEntry> GetAll()
        {
            return TimedButtonOperationStatsRepositoryProvider.GetRepository().GetAll();
        }

        public static bool Delete(string operationKey)
        {
            bool deleted = TimedButtonOperationStatsRepositoryProvider.GetRepository().Delete(operationKey);
            if (!deleted)
            {
                return false;
            }

            TimedButtonOperationWarmupSession.Reset(operationKey);
            NotifyStatsChanged(operationKey);
            return true;
        }

        public static int Clear()
        {
            int cleared = TimedButtonOperationStatsRepositoryProvider.GetRepository().Clear();
            if (cleared <= 0)
            {
                return 0;
            }

            TimedButtonOperationWarmupSession.Clear();
            NotifyStatsChanged(null);
            return cleared;
        }

        internal static void NotifyStatsChanged(string? operationKey)
        {
            StatsChanged?.Invoke(null, new TimedButtonOperationStatsChangedEventArgs(operationKey));
        }
    }

    public static class TimedButtonOperationStatsWindowLauncherProvider
    {
        private static readonly object SyncRoot = new object();
        private static ITimedButtonOperationStatsWindowLauncher? _launcher;

        public static void SetLauncher(ITimedButtonOperationStatsWindowLauncher launcher)
        {
            ArgumentNullException.ThrowIfNull(launcher);

            lock (SyncRoot)
            {
                _launcher = launcher;
            }
        }

        public static ITimedButtonOperationStatsWindowLauncher? GetLauncher()
        {
            lock (SyncRoot)
            {
                return _launcher;
            }
        }
    }

    public static class TimedButtonOperationStatsRepositoryProvider
    {
        private static readonly object SyncRoot = new object();
        private static ITimedButtonOperationStatsRepository? _repository;

        public static void SetRepository(ITimedButtonOperationStatsRepository repository)
        {
            ArgumentNullException.ThrowIfNull(repository);

            lock (SyncRoot)
            {
                _repository = repository;
            }
        }

        internal static ITimedButtonOperationStatsRepository GetRepository()
        {
            lock (SyncRoot)
            {
                return _repository ??= new TimedButtonOperationConfigStatsRepository();
            }
        }
    }

    internal sealed class TimedButtonOperationConfigStatsRepository : ITimedButtonOperationStatsRepository
    {
        public TimedButtonOperationStats? Get(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                return null;
            }

            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();
            config.Records.TryGetValue(operationKey.Trim(), out TimedButtonOperationStats? stats);
            return stats?.Clone();
        }

        public IReadOnlyList<TimedButtonOperationStatsEntry> GetAll()
        {
            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            return config.Records
                .Select(item => new TimedButtonOperationStatsEntry
                {
                    OperationKey = item.Key,
                    Stats = item.Value.Clone()
                })
                .OrderByDescending(item => item.Stats.LastCompletedAt)
                .ThenBy(item => item.OperationKey, StringComparer.Ordinal)
                .ToList();
        }

        public TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatAsWarmupSample, bool persistImmediately)
        {
            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            string normalizedKey = operationKey.Trim();
            if (!config.Records.TryGetValue(normalizedKey, out TimedButtonOperationStats? stats))
            {
                stats = new TimedButtonOperationStats();
                config.Records[normalizedKey] = stats;
            }

            stats.Record(elapsedMilliseconds, treatAsWarmupSample);

            if (persistImmediately)
            {
                ConfigHandler.GetInstance().Save<TimedButtonOperationStatsConfig>();
            }

            return new TimedButtonOperationRecordResult(stats.Clone(), treatAsWarmupSample);
        }

        public bool Delete(string operationKey)
        {
            if (string.IsNullOrWhiteSpace(operationKey))
            {
                return false;
            }

            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            bool removed = config.Records.Remove(operationKey.Trim());
            if (removed)
            {
                ConfigHandler.GetInstance().Save<TimedButtonOperationStatsConfig>();
            }

            return removed;
        }

        public int Clear()
        {
            TimedButtonOperationStatsConfig config = ConfigHandler.GetInstance().GetRequiredService<TimedButtonOperationStatsConfig>();
            config.Records ??= new Dictionary<string, TimedButtonOperationStats>();

            int count = config.Records.Count;
            if (count == 0)
            {
                return 0;
            }

            config.Records.Clear();
            ConfigHandler.GetInstance().Save<TimedButtonOperationStatsConfig>();
            return count;
        }
    }

    internal static class TimedButtonOperationStatsStore
    {
        public static TimedButtonOperationStats? Get(string operationKey)
        {
            return TimedButtonOperationStatsRepositoryProvider.GetRepository().Get(operationKey);
        }

        public static TimedButtonOperationRecordResult Record(string operationKey, double elapsedMilliseconds, bool treatFirstSuccessAsWarmup, bool persistImmediately)
        {
            bool treatAsWarmupSample = TimedButtonOperationWarmupSession.ConsumeWarmup(operationKey, treatFirstSuccessAsWarmup);
            return TimedButtonOperationStatsRepositoryProvider.GetRepository().Record(operationKey, elapsedMilliseconds, treatAsWarmupSample, persistImmediately);
        }
    }
}
