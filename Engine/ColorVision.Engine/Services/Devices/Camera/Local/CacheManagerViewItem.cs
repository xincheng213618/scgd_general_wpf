using ColorVision.Engine.Services.Caches;
using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed class CacheModuleViewItem : ViewModelBase
    {
        internal CacheModuleViewItem(CacheModuleSnapshot snapshot, ulong totalBytes)
        {
            Snapshot = snapshot;
            MemoryShare = totalBytes == 0 ? 0 : (double)snapshot.MemoryBytes / totalBytes;
        }

        internal CacheModuleSnapshot Snapshot { get; }
        public string Id => Snapshot.Id;
        public string Name => Snapshot.Name;
        public string Description => Snapshot.Description;
        public ulong MemoryBytes => Snapshot.MemoryBytes;
        public string MemoryText => Snapshot.Error == null ? CacheManagerService.FormatBytes(MemoryBytes) : "—";
        public double MemoryShare { get; }
        public int EntryCount => Snapshot.EntryCount;
        public ulong HitCount => Snapshot.HitCount;
        public ulong ActiveReferences => Snapshot.ActiveReferences;
        private bool isMarkedForRelease;
        public bool IsMarkedForRelease
        {
            get => isMarkedForRelease;
            set { if (isMarkedForRelease != value) SetProperty(ref isMarkedForRelease, value); }
        }
        public bool CanRelease => Snapshot.Error != null || MemoryBytes > 0 || ActiveReferences > 0 || (Id != "Snapshot" && EntryCount > 0);
        public string EntryCountText => Snapshot.Error == null ? EntryCount.ToString("N0") : "—";
        public string UsageText => EngineLocalization.Get(Snapshot.Error != null ? "读取失败"
            : ActiveReferences > 0 ? "正在使用" : !Snapshot.IsEnabled ? "已停用"
            : MemoryBytes > 0 ? "已缓存（可释放）" : "暂无缓存");
    }

    internal sealed record CacheNavigationItem(string Id, string Name, string MemoryText);

    internal sealed class CacheEntryViewItem(CacheEntrySnapshot entry, long? currentSourceId = null)
    {
        public string Name => entry.SourceId.HasValue && entry.SourceId == currentSourceId
            ? EngineLocalization.Format($"当前窗口 · {entry.Name}") : entry.Name;
        public long? SourceId => entry.SourceId;
        public string BufferInfo => entry.BufferInfo;
        public ulong? IdleBytes => entry.IdleBytes;
        public ulong? ActiveBytes => entry.ActiveBytes;
        public ulong? PendingReleaseBytes => entry.PendingReleaseBytes;
        public string IdleText => entry.IdleBytes.HasValue ? CacheManagerService.FormatBytes(entry.IdleBytes.Value) : "—";
        public string ActiveText => entry.ActiveBytes.HasValue ? CacheManagerService.FormatBytes(entry.ActiveBytes.Value) : "—";
        public string PendingText => entry.PendingReleaseBytes.HasValue ? CacheManagerService.FormatBytes(entry.PendingReleaseBytes.Value) : "—";
        public string FilePath => entry.FilePath;
        public ulong FileBytes => entry.FileBytes;
        public ulong MemoryBytes => entry.MemoryBytes;
        public string FileSizeText => CacheManagerService.FormatBytes(FileBytes);
        public string MemoryText => CacheManagerService.FormatBytes(MemoryBytes);
        public ulong HitCount => entry.HitCount;
        public ulong ActiveReferences => entry.ActiveReferences;
        public string UsageText => entry.State;
    }
}
