using ColorVision.ImageEditor.Output;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Caches
{
    internal sealed class SnapshotCacheModule : ICacheModule
    {
        public string Id => "Snapshot";
        public string Name => EngineLocalization.Get("截图缓存");
        public string Description => EngineLocalization.Get("按图像视图统计截图导出缓冲。空闲缓冲可立即释放，在用缓冲归还后释放；已保存的截图文件保留。");

        public CacheModuleSnapshot GetSnapshot()
        {
            CacheEntrySnapshot[] entries = SnapshotBufferCache.GetSnapshot().Select(entry => new CacheEntrySnapshot(
                GetSourceName(entry), string.Empty, 0, (ulong)(entry.IdleBytes + entry.ActiveBytes), (ulong)entry.HitCount,
                (ulong)entry.ActiveCount, EngineLocalization.Get(entry.PendingReleaseBytes > 0 ? "等待归还后释放"
                    : entry.ActiveCount > 0 ? "正在使用" : entry.IdleBytes > 0 ? "已缓存（可释放）" : "暂无缓存"),
                entry.Id, entry.IdleBytes > 0 ? $"{entry.IdleWidth} × {entry.IdleHeight} · {entry.PixelFormat}" : "—",
                (ulong)entry.IdleBytes, (ulong)entry.ActiveBytes, (ulong)entry.PendingReleaseBytes)).ToArray();
            ulong total = entries.Aggregate(0UL, (sum, entry) => sum + entry.MemoryBytes);
            ulong idle = entries.Aggregate(0UL, (sum, entry) => sum + entry.IdleBytes!.Value);
            ulong pending = entries.Aggregate(0UL, (sum, entry) => sum + entry.PendingReleaseBytes!.Value);
            ulong activeCount = entries.Aggregate(0UL, (sum, entry) => sum + entry.ActiveReferences);
            string summary = EngineLocalization.Format($"来源视图：{entries.Length:N0} 个 · 空闲可释放：{CacheManagerService.FormatBytes(idle)} · 在用缓冲：{activeCount:N0} 个 · 待归还释放：{CacheManagerService.FormatBytes(pending)}");
            return new(Id, Name, Description, total, entries.Length, entries.Aggregate(0UL, (sum, entry) => sum + entry.HitCount),
                0, activeCount, true, false, summary, entries);
        }

        private static string GetSourceName(SnapshotBufferCacheEntry entry)
        {
            string source = string.IsNullOrWhiteSpace(entry.SourceName) ? EngineLocalization.Get("图像视图") : entry.SourceName;
            return entry.IsClosed ? EngineLocalization.Format($"{source} · #{entry.Id}（已关闭，等待导出完成）") : $"{source} · #{entry.Id}";
        }

        public Task<CacheModuleReleaseResult> ReleaseAsync() => Task.Run(() => ReleaseSource(null));

        internal CacheModuleReleaseResult ReleaseSource(long? sourceId)
        {
            SnapshotBufferReleaseResult result = SnapshotBufferCache.Release(sourceId);
            string message = EngineLocalization.Format($"截图缓存已释放 {CacheManagerService.FormatBytes((ulong)result.ReleasedBytes)}，待归还后释放 {CacheManagerService.FormatBytes((ulong)result.PendingReleaseBytes)}。已保存的截图文件保留。");
            return new(Id, (ulong)result.ReleasedBytes, message, true);
        }

        public void SetEnabled(bool enabled) => throw new NotSupportedException();
    }
}
