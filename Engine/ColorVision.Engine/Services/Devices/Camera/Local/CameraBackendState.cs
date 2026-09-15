using System;

namespace ColorVision.Engine.Services.Devices.Camera.Local
{
    // Service heartbeats describe the remote owner only. Never overwrite local ownership.
    internal sealed class CameraBackendState
    {
        internal static object OwnershipSync { get; } = new();
        public event EventHandler? Changed;
        public bool PreferLocal { get; private set; }
        public bool LocalOwned { get; private set; }
        public bool VideoOwned { get; private set; }
        public bool ServiceMayOwnCamera { get; private set; }
        public DeviceStatusType ServiceStatus { get; private set; } = DeviceStatusType.UnInit;
        private DeviceStatusType localStatus = DeviceStatusType.Closed;
        private int localCommands;
        private int serviceCommands;

        public bool RoutesLocally => LocalOwned;
        public bool OpensLocally => LocalOwned || (!ServiceMayOwnCamera && PreferLocal);
        public DeviceStatusType Status => LocalOwned ? localStatus : ServiceStatus;

        public CameraBackendState(bool preferLocal) => PreferLocal = preferLocal;

        public void SetPreference(bool value)
        {
            lock (OwnershipSync)
            {
                if (PreferLocal == value) return;
                PreferLocal = value;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void ObserveService(DeviceStatusType status)
        {
            lock (OwnershipSync)
            {
                ServiceStatus = status;
                if (status == DeviceStatusType.Closed && serviceCommands == 0) ServiceMayOwnCamera = false;
                else if (status is DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Opening
                    or DeviceStatusType.Closing or DeviceStatusType.Busy or DeviceStatusType.Free)
                    ServiceMayOwnCamera = true;
                // Offline/unknown after an open does not prove the remote handle was released.
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void BeginLocalOpen(bool video = false)
        {
            lock (OwnershipSync)
            {
                EnsureLocalAvailable();
                if (LocalOwned || VideoOwned) throw new InvalidOperationException("本机已有相机会话，请先关闭后再打开另一种采集模式。");
                if (video) VideoOwned = true;
                else { LocalOwned = true; localStatus = DeviceStatusType.Opening; }
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void EnsureLocalAvailable()
        {
            lock (OwnershipSync)
            {
                // An already-open native session remains authoritative even if a logical service heartbeat is stale.
                if (!LocalOwned && (ServiceMayOwnCamera || serviceCommands != 0))
                    throw new InvalidOperationException("服务相机正在占用或操作尚未完成。请先关闭当前相机，确认已关闭后再使用本地相机。");
                if (VideoOwned) throw new InvalidOperationException("请先关闭主面板的本地视频。");
            }
        }

        public void SetLocalStatus(DeviceStatusType status)
        {
            lock (OwnershipSync)
            {
                LocalOwned = status != DeviceStatusType.Closed;
                localStatus = status;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void EndVideo()
        {
            lock (OwnershipSync) VideoOwned = false;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void BeginLocalCommand(bool closing = false)
        {
            lock (OwnershipSync)
            {
                if (!closing) EnsureLocalAvailable();
                if (localCommands != 0) throw new InvalidOperationException("本地相机操作正在执行，请等待完成。");
                localCommands++;
            }
        }

        public void EndLocalCommand() { lock (OwnershipSync) localCommands--; }

        public void BeginServiceCommand(string eventName)
        {
            lock (OwnershipSync)
            {
                EnsureServiceAvailable();
                serviceCommands++;
                if (eventName == "Open") ServiceMayOwnCamera = true;
            }
        }

        public void EnsureServiceAvailable()
        {
            lock (OwnershipSync)
                if (RoutesLocally || VideoOwned || localCommands != 0)
                    throw new InvalidOperationException("当前相机由本地后端管理，不支持发送服务相机指令。");
        }

        public void EndServiceCommand() { lock (OwnershipSync) serviceCommands--; }
    }
}
