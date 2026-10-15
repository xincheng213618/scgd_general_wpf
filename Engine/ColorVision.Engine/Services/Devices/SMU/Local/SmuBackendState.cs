using System;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal sealed class SmuBackendState(bool preference)
{
    private static readonly object OwnershipSync = new();
    internal object Sync => OwnershipSync;
    private bool preferLocal = preference, serviceMayOwn;
    private int localCommands, serviceCommands;
    private DeviceStatusType localStatus = DeviceStatusType.Closed, serviceStatus = DeviceStatusType.UnInit;
    internal event Action? Changed;
    internal bool LocalOwned { get; private set; }
    internal bool CanSwitch { get { lock (Sync) return !LocalOwned && !serviceMayOwn && localCommands == 0 && serviceCommands == 0; } }
    internal bool OpensLocally { get { lock (Sync) return LocalOwned || localCommands != 0 || (preferLocal && !serviceMayOwn && serviceCommands == 0); } }
    internal DeviceStatusType Status { get { lock (Sync) return LocalOwned ? localStatus : OpensLocally ? DeviceStatusType.Closed : serviceStatus; } }
    internal void SetPreference(bool value) { lock (Sync) preferLocal = value; Changed?.Invoke(); }
    internal void ObserveService(DeviceStatusType value)
    {
        lock (Sync)
        {
            serviceStatus = value;
            if (value == DeviceStatusType.Closed && serviceCommands == 0) serviceMayOwn = false;
            else if (value is DeviceStatusType.Opening or DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Busy or DeviceStatusType.Free or DeviceStatusType.Closing) serviceMayOwn = true;
        }
        Changed?.Invoke();
    }
    internal void SetLocalStatus(DeviceStatusType value) { lock (Sync) { localStatus = value; LocalOwned = value != DeviceStatusType.Closed; } Changed?.Invoke(); }
    internal void EnsureLocalAvailable()
    {
        lock (Sync) if (!LocalOwned && (serviceMayOwn || serviceCommands != 0)) throw new InvalidOperationException("服务正在占用源表，请先关闭服务连接。");
    }
    internal void EnsureServiceAvailable()
    {
        lock (Sync) if (LocalOwned || localCommands != 0) throw new InvalidOperationException("本地源表正在使用，请先关闭本地连接。");
    }
    internal void BeginLocalCommand() { lock (Sync) { EnsureLocalAvailable(); localCommands++; } }
    internal void EndLocalCommand() { lock (Sync) localCommands--; }
    internal void BeginServiceCommand(string operation) { lock (Sync) { EnsureServiceAvailable(); serviceCommands++; if (operation is "Open" or "Reopen") serviceMayOwn = true; } }
    internal void EndServiceCommand() { lock (Sync) serviceCommands--; }
}
