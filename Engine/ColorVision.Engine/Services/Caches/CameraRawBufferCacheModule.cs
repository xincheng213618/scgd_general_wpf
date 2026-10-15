using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Caches
{
    internal sealed class CameraRawBufferCacheModule(Func<IReadOnlyList<(string DeviceCode, LocalCameraRawBufferPool Pool)>>? getPools = null) : ICacheModule
    {
        private readonly Func<IReadOnlyList<(string DeviceCode, LocalCameraRawBufferPool Pool)>> getPools = getPools ?? ReadCameraPools;

        public string Id => "CameraRawBuffer";
        public string Name => EngineLocalization.Get("相机取图缓冲");
        public string Description => EngineLocalization.Get("默认关闭，流程加速时可手动开启。开启后每台相机最多保留一块空闲 RAW 缓冲；关闭时释放空闲缓冲，在用图像归还后释放。");

        public CacheModuleSnapshot GetSnapshot()
        {
            CacheEntrySnapshot[] entries = getPools().Select(item => (item.DeviceCode, Bytes: item.Pool.IdleBytes))
                .Where(item => item.Bytes > 0)
                .Select(item => new CacheEntrySnapshot(item.DeviceCode, string.Empty, 0, (ulong)item.Bytes, 0, 0,
                    EngineLocalization.Get("已缓存（可释放）"))).ToArray();
            ulong bytes = entries.Aggregate(0UL, (total, entry) => checked(total + entry.MemoryBytes));
            return new CacheModuleSnapshot(Id, Name, Description, bytes, entries.Length, 0, 0, 0, LocalCameraRawBufferPool.IsCacheEnabled, true,
                EngineLocalization.Get("仅统计空闲取图缓冲，不含正在使用的图像。"), Array.AsReadOnly(entries));
        }

        public Task<CacheModuleReleaseResult> ReleaseAsync()
        {
            IReadOnlyList<(string DeviceCode, LocalCameraRawBufferPool Pool)> pools = getPools();
            return Task.Run(() =>
            {
                ulong releasedBytes = 0;
                foreach (var item in pools) releasedBytes = checked(releasedBytes + (ulong)item.Pool.ReleaseIdle());
                string message = EngineLocalization.Format($"已释放 {CacheManagerService.FormatBytes(releasedBytes)} 空闲取图缓冲。在用图像不受影响，后续取图可重新申请和复用。");
                return new CacheModuleReleaseResult(Id, releasedBytes, message, true);
            });
        }

        public void SetEnabled(bool enabled)
        {
            CameraRawBufferCacheConfig.Current.IsEnabled = enabled;
            CameraRawBufferCacheConfig.SaveCurrent();
        }

        private static IReadOnlyList<(string DeviceCode, LocalCameraRawBufferPool Pool)> ReadCameraPools()
        {
            // DeviceServices belongs to the UI thread; do not hold a camera's capture lock to read or clear idle memory.
            return Application.Current.Dispatcher.Invoke(() => ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                .Distinct().Select(camera => (camera.Code, camera.LocalCameraSession.RawBufferPool)).ToArray());
        }
    }
}
