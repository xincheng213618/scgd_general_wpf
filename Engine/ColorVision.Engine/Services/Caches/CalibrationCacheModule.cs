using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Caches
{
    internal sealed class CalibrationCacheModule : ICacheModule
    {
        public string Id => "Calibration";
        public string Name => EngineLocalization.Get("校正缓存");
        public string Description => EngineLocalization.Get("复用校正文件和相机上下文，减少重复加载。释放后会在下次校正时重新加载。");

        public CacheModuleSnapshot GetSnapshot()
        {
            CalibrationSharedCacheSnapshot snapshot = LocalCalibrationCacheManager.GetEntries();
            CalibrationSharedCacheStatistics statistics = snapshot.Statistics;
            CacheEntrySnapshot[] entries = snapshot.Entries
                .OrderBy(entry => entry.CalibrationType)
                .ThenBy(entry => entry.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(entry => new CacheEntrySnapshot(GetCalibrationTypeText(entry.CalibrationType), entry.FilePath, entry.FileBytes,
                    entry.EstimatedMemoryBytes, entry.HitCount, entry.ActiveOwnerCount, GetState(entry)))
                .ToArray();
            ulong activeReferences = entries.Aggregate(0UL, (total, entry) => checked(total + entry.ActiveReferences));
            string detailSummary = EngineLocalization.Format($"缓存预算：{CacheManagerService.FormatBytes(statistics.BudgetBytes)}");
            return new CacheModuleSnapshot(Id, Name, Description, statistics.EstimatedMemoryBytes, checked((int)statistics.EntryCount),
                statistics.HitCount, statistics.MissCount, activeReferences, true, false, detailSummary, Array.AsReadOnly(entries));
        }

        public async Task<CacheModuleReleaseResult> ReleaseAsync()
        {
            LocalCalibrationCacheReleaseSummary summary = await LocalCalibrationCacheService.ReleaseCalibrationAsync().ConfigureAwait(false);
            List<string> lines = new()
            {
                EngineLocalization.Format($"已检查 {summary.DeviceCount} 台相机，释放 {summary.ContextsReleased} 个本地校正上下文缓存项。"),
            };
            ulong releasedBytes = 0;
            bool hasActiveEntries = false;
            if (summary.NativeRelease is CalibrationSharedCacheReleaseResult nativeRelease)
            {
                hasActiveEntries = nativeRelease.ActiveEntryCount > 0;
                releasedBytes = nativeRelease.ReleasedEstimatedMemoryBytes >= nativeRelease.ActiveEstimatedMemoryBytes
                    ? nativeRelease.ReleasedEstimatedMemoryBytes - nativeRelease.ActiveEstimatedMemoryBytes
                    : 0;
                lines.Add(EngineLocalization.Format($"共享文件缓存已移除 {nativeRelease.ReleasedEntryCount} 项，涉及驻留内存约 {CacheManagerService.FormatBytes(nativeRelease.ReleasedEstimatedMemoryBytes)}。"));
                if (hasActiveEntries)
                {
                    lines.Add(EngineLocalization.Format($"其中仍有 {nativeRelease.ActiveEntryCount} 项被 {nativeRelease.ActiveOwnerCount} 个活动引用使用，约 {CacheManagerService.FormatBytes(nativeRelease.ActiveEstimatedMemoryBytes)} 暂未物理释放。完成相关执行后可再次释放。"));
                }
                else
                {
                    lines.Add(EngineLocalization.Get("没有共享文件缓存仍被活动上下文占用。"));
                }
            }
            else
            {
                lines.Add(EngineLocalization.Get("opencv_helper 共享文件缓存未能执行释放。"));
            }
            if (summary.Errors.Count > 0)
            {
                lines.Add(EngineLocalization.Get("释放错误：") + string.Join(EngineLocalization.Get("；"), summary.Errors.Select(error => $"{error.DeviceCode}: {error.Message}")));
            }
            return new CacheModuleReleaseResult(Id, releasedBytes, string.Join(Environment.NewLine, lines), summary.Succeeded && !hasActiveEntries);
        }

        public void SetEnabled(bool enabled) => throw new NotSupportedException(EngineLocalization.Get("校正缓存不支持停用，请使用释放缓存。"));

        private static string GetCalibrationTypeText(int calibrationType)
            => Enum.IsDefined(typeof(CalibrationType), calibrationType)
                ? EngineLocalization.Get(((CalibrationType)calibrationType).ToString())
                : EngineLocalization.Format($"未知 ({calibrationType})");

        private static string GetState(CalibrationSharedCacheEntry entry)
        {
            if ((entry.Flags & CalibrationSharedCacheEntryStates.Loading) != 0) return EngineLocalization.Get("正在加载");
            if (entry.ActiveOwnerCount > 0) return EngineLocalization.Format($"仍被使用（{entry.ActiveOwnerCount} 个引用）");
            return EngineLocalization.Get((entry.Flags & CalibrationSharedCacheEntryStates.Ready) != 0 ? "已缓存（可释放）" : "等待加载");
        }
    }
}
