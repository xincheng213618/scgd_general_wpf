using ColorVision.Engine.Services.Caches;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    internal sealed class CacheModuleViewItem
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
        public string UsageText => EngineLocalization.Get(Snapshot.Error != null ? "读取失败"
            : ActiveReferences > 0 ? "正在使用" : !Snapshot.IsEnabled ? Snapshot.Id == "ImageFile" ? "已关闭（直接读写文件）" : "已关闭"
            : MemoryBytes > 0 ? "已缓存（可释放）" : "等待加载");
    }

    internal sealed class CacheEntryViewItem(CacheEntrySnapshot entry)
    {
        public string Name => entry.Name;
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
