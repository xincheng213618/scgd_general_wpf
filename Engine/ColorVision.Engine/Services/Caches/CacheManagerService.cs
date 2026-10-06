using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Caches
{
    internal static class CacheManagerService
    {
        public static IReadOnlyList<ICacheModule> Modules { get; } = Array.AsReadOnly<ICacheModule>(
            new ICacheModule[] { new CalibrationCacheModule(), new ImageFileCacheModule(), new CameraRawBufferCacheModule() });

        public static ICacheModule? GetById(string id)
            => Modules.FirstOrDefault(module => string.Equals(module.Id, id, StringComparison.Ordinal));

        public static Task<IReadOnlyList<CacheModuleSnapshot>> ReadSnapshotsAsync() => ReadSnapshotsAsync(Modules);

        internal static async Task<IReadOnlyList<CacheModuleSnapshot>> ReadSnapshotsAsync(IReadOnlyList<ICacheModule> modules)
        {
            ArgumentNullException.ThrowIfNull(modules);
            CacheModuleSnapshot[] snapshots = await Task.WhenAll(modules.Select(module => Task.Run(() => ReadSnapshot(module)))).ConfigureAwait(false);
            return Array.AsReadOnly(snapshots);
        }

        public static Task<IReadOnlyList<CacheModuleReleaseResult>> ReleaseAllAsync() => ReleaseAllAsync(Modules);

        internal static async Task<IReadOnlyList<CacheModuleReleaseResult>> ReleaseAllAsync(IReadOnlyList<ICacheModule> modules)
        {
            ArgumentNullException.ThrowIfNull(modules);
            List<CacheModuleReleaseResult> results = new();
            foreach (ICacheModule module in modules)
            {
                results.Add(await ReleaseAsync(module).ConfigureAwait(false));
            }
            return results.AsReadOnly();
        }

        public static async Task<CacheModuleReleaseResult> ReleaseAsync(ICacheModule module)
        {
            ArgumentNullException.ThrowIfNull(module);
            try
            {
                return await module.ReleaseAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new CacheModuleReleaseResult(module.Id, 0, EngineLocalization.Format($"释放缓存失败：{ex.Message}"), false);
            }
        }

        private static CacheModuleSnapshot ReadSnapshot(ICacheModule module)
        {
            try
            {
                return module.GetSnapshot();
            }
            catch (Exception ex)
            {
                return new CacheModuleSnapshot(module.Id, module.Name, module.Description, 0, 0, 0, 0, 0, false, false,
                    EngineLocalization.Get("读取失败"), Array.Empty<CacheEntrySnapshot>(), ex.Message);
            }
        }

        internal static string FormatBytes(ulong bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return unitIndex == 0 ? $"{bytes:N0} {units[unitIndex]}" : $"{value:N2} {units[unitIndex]}";
        }
    }
}
