using ColorVision.Core;
using ColorVision.UI;
using cvColorVision;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed record LocalCalibrationWarmupFile(CalibrationType Type, string DisplayName, string FullPath,
        ulong EstimatedMemoryBytes = 0, string? Sha256 = null, LocalCalibrationFileRevision? Revision = null)
    {
        internal (int Type, string Path) CacheKey => ((int)Type, FullPath.ToUpperInvariant());
    }
    internal sealed record LocalCalibrationWarmupPlan(string DeviceCode, LocalCalibrationWarmupFile[] Files);

    internal static class LocalCalibrationWarmupHistory
    {
        public static bool Enabled
        {
            get => Current?.Enabled ?? true;
            set
            {
                if (Current is { } history && history.SetEnabled(value))
                    history.Save();
            }
        }

        public static LocalCalibrationWarmupPlan[] GetExitPlans(string deviceCode)
            => Current?.GetExitPlans(deviceCode) ?? [];

        private static LocalCalibrationWarmupConfig? Current
            => ConfigService.Instance?.GetRequiredService<LocalCalibrationWarmupConfig>();

        internal static LocalCalibrationWarmupPlan[] FitPlans(IEnumerable<LocalCalibrationWarmupPlan> plans, ulong budgetBytes)
        {
            if (budgetBytes == 0) return [];
            var retained = new HashSet<((int Type, string Path) Key, string? Sha256)>();
            var fitting = new List<LocalCalibrationWarmupPlan>();
            foreach (var plan in plans)
            {
                if (plan == null || string.IsNullOrWhiteSpace(plan.DeviceCode) || plan.Files is not { Length: > 0 }
                    || !plan.Files.All(file => file != null && file.EstimatedMemoryBytes > 0 && !string.IsNullOrWhiteSpace(file.Sha256)
                        && !string.IsNullOrWhiteSpace(file.FullPath) && Path.IsPathFullyQualified(file.FullPath))) continue;

                var additional = plan.Files.GroupBy(file => (file.CacheKey, file.Sha256))
                    .Where(group => !retained.Contains(group.Key)).ToArray();
                if (!FitsWithin(additional.Select(group => group.Max(file => file.EstimatedMemoryBytes)), budgetBytes, out ulong remaining))
                    continue;
                retained.UnionWith(additional.Select(group => group.Key));
                budgetBytes = remaining;
                fitting.Add(plan);
            }
            return fitting.ToArray();
        }

        internal static bool FitsWithin(IEnumerable<ulong> costs, ulong budgetBytes, out ulong remaining)
        {
            remaining = budgetBytes;
            foreach (ulong bytes in costs)
            {
                if (bytes > remaining) return false;
                remaining -= bytes;
            }
            return true;
        }
    }

    public sealed class LocalCalibrationWarmupConfig : IConfig
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LocalCalibrationWarmupConfig));

        private readonly object sync = new();
        private LocalCalibrationWarmupPlan[] exitPlans = [];
        private volatile bool enabled = true;

        [JsonProperty]
        public bool Enabled { get => enabled; private set => enabled = value; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? MemoryBudgetMiB { get; private set; }

        // The serializer sees an immutable snapshot, including during an application-wide save.
        [JsonProperty("ExitPlans")]
        private LocalCalibrationWarmupPlan[] ExitPlans
        {
            get { lock (sync) return exitPlans; }
            set => SetExitPlans(value ?? [], ulong.MaxValue);
        }

        internal bool SetEnabled(bool value)
        {
            lock (sync)
            {
                if (Enabled == value) return false;
                Enabled = value;
                return true;
            }
        }

        internal LocalCalibrationWarmupPlan[] GetExitPlans(string deviceCode)
        {
            lock (sync)
                return exitPlans.Where(plan => plan.DeviceCode == deviceCode).ToArray();
        }

        internal void SetExitPlans(IEnumerable<LocalCalibrationWarmupPlan> plans, ulong budgetBytes)
        {
            lock (sync) exitPlans = LocalCalibrationWarmupHistory.FitPlans(plans, budgetBytes);
        }

        internal static void SetMemoryBudgetMiB(int budgetMiB)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(budgetMiB);
            IConfigService service = ConfigService.Instance;
            var config = service.GetRequiredService<LocalCalibrationWarmupConfig>();
            int? previous = config.MemoryBudgetMiB;
            ulong previousBudget = OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
            ApplyBudget(budgetMiB);
            try
            {
                config.MemoryBudgetMiB = budgetMiB;
                if (service is ConfigHandler handler)
                {
                    if (!handler.TrySave(config, out string error)) throw new IOException(error);
                }
                else service.Save<LocalCalibrationWarmupConfig>();
            }
            catch
            {
                config.MemoryBudgetMiB = previous;
                ApplyBudgetBytes(previousBudget);
                throw;
            }
        }

        internal static void ApplyBudget(int budgetMiB) =>
            ApplyBudgetBytes(checked((ulong)budgetMiB * 1024 * 1024));

        private static void ApplyBudgetBytes(ulong bytes) =>
            LocalCalibrationCacheManager.RunWithExclusiveSharedCacheAccess(() =>
            {
                OpenCVCalibration.SetCalibrationSharedCacheBudget(bytes);
                return true;
            });

        /// <summary>Record the resident calibration plans at normal application shutdown.</summary>
        public static void SaveAtApplicationExit()
        {
            if (ServiceManager.Current is not { } manager || ConfigService.Instance is not ConfigHandler { IsAutoSave: true } configHandler)
                return;
            var config = configHandler.GetRequiredService<LocalCalibrationWarmupConfig>();
            var (plans, budgetBytes) = LocalCalibrationCacheManager.RunWithExclusiveSharedCacheAccess(() =>
            {
                var caches = manager.DeviceServices.OfType<DeviceCamera>().Distinct()
                    .Select(camera => camera.LocalCalibrationCacheManager).ToArray();
                foreach (var cache in caches)
                    cache.OnMeasurementClosed();
                return (caches.SelectMany(cache => cache.CaptureWarmupPlans()).OrderByDescending(entry => entry.LastAccess)
                    .Select(entry => entry.Plan).ToArray(), OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes);
            });
            config.SetExitPlans(plans, budgetBytes);
            if (!configHandler.TrySave(config, out string error))
                Log.Warn($"Unable to save the calibration cache at application exit: {error}");
        }

        internal void Save() => ConfigService.Instance?.Save<LocalCalibrationWarmupConfig>();
    }

    public sealed class LocalCalibrationBudgetInitializer : InitializerBase
    {
        public override Task InitializeAsync()
        {
            if (ConfigService.Instance.GetRequiredService<LocalCalibrationWarmupConfig>().MemoryBudgetMiB is int budgetMiB && budgetMiB >= 0)
                LocalCalibrationWarmupConfig.ApplyBudget(budgetMiB);
            return Task.CompletedTask;
        }
    }
}
