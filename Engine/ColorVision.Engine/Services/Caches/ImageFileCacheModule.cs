using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Caches
{
    internal sealed class ImageFileCacheModule : ICacheModule
    {
        public string Id => "ImageFile";
        public string Name => EngineLocalization.Get("图像文件缓存");
        public string Description => EngineLocalization.Get("默认保留 1 个完整 CVRAW 图像文件，可调整；减少数量后释放多余缓存，磁盘文件保留。");

        public CacheModuleSnapshot GetSnapshot()
        {
            CVFileReadCacheSnapshot snapshot = CVFileReadCache.GetSnapshot();
            ulong memoryBytes = checked((ulong)snapshot.CapacityBytes);
            CacheEntrySnapshot[] entries = snapshot.Entries.Select(entry => new CacheEntrySnapshot(
                string.IsNullOrEmpty(entry.FilePath) ? EngineLocalization.Get("图像文件槽位") : Path.GetFileName(entry.FilePath), entry.FilePath ?? string.Empty,
                checked((ulong)entry.ContentBytes), checked((ulong)entry.CapacityBytes), checked((ulong)entry.HitCount), checked((ulong)entry.ActiveReaders),
                entry.IsRetired ? EngineLocalization.Get("等待读取完成后释放") : entry.ActiveReaders > 0
                    ? EngineLocalization.Format($"正在读取（{entry.ActiveReaders} 个引用）")
                    : EngineLocalization.Get(entry.FilePath == null ? "等待加载" : "已缓存（可释放）"))).ToArray();
            string detailSummary = EngineLocalization.Format($"缓存上限：{snapshot.MaximumEntries:N0} 个 · 已分配 {snapshot.AllocationCount:N0} 次");
            return new CacheModuleSnapshot(Id, Name, Description, memoryBytes, entries.Length, checked((ulong)snapshot.HitCount),
                checked((ulong)snapshot.MissCount), checked((ulong)snapshot.ActiveReaders), snapshot.IsEnabled, true, detailSummary, Array.AsReadOnly(entries));
        }

        public async Task<CacheModuleReleaseResult> ReleaseAsync()
        {
            long releasedBytes = await LocalCalibrationCacheService.ReleaseImageFileAsync().ConfigureAwait(false);
            string message = EngineLocalization.Format($"图像文件缓存槽位已释放 {CacheManagerService.FormatBytes(checked((ulong)releasedBytes))}，磁盘文件保留。");
            return new CacheModuleReleaseResult(Id, checked((ulong)releasedBytes), message, true);
        }

        public void SetEnabled(bool enabled)
        {
            CvRawFileCacheConfig.Current.IsEnabled = enabled;
            CvRawFileCacheConfig.SaveCurrent();
        }

        public void SetMaximumEntries(int maximumEntries)
        {
            CvRawFileCacheConfig.Current.MaximumEntries = maximumEntries;
            CvRawFileCacheConfig.SaveCurrent();
        }
    }
}
