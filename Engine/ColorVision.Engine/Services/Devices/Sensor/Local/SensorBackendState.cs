using System;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal sealed class SensorBackendState(bool preference)
{
    private static readonly object OwnershipSync = new();
    internal object Sync => OwnershipSync;
    private bool preferLocal = preference, serviceMayOwn;
    private int localCommands, serviceCommands;
    private DeviceStatusType serviceStatus = DeviceStatusType.UnInit, localStatus = DeviceStatusType.Closed;
    internal bool LocalOwned { get; private set; }
    public event Action? Changed;
    public bool OpensLocally { get { lock (Sync) return LocalOwned || (preferLocal && !serviceMayOwn && serviceCommands == 0); } }
    public DeviceStatusType Status { get { lock (Sync) return LocalOwned ? localStatus : OpensLocally ? DeviceStatusType.Closed : serviceStatus; } }
    public void SetPreference(bool value) { lock (Sync) preferLocal = value; Changed?.Invoke(); }
    public void SetLocalStatus(DeviceStatusType value) { lock (Sync) { localStatus = value; LocalOwned = value != DeviceStatusType.Closed; } Changed?.Invoke(); }
    public void ObserveService(DeviceStatusType value)
    {
        lock (Sync)
        {
            serviceStatus = value;
            if (value == DeviceStatusType.Closed && serviceCommands == 0) serviceMayOwn = false;
            else if (value is DeviceStatusType.Opening or DeviceStatusType.Opened or DeviceStatusType.Busy or DeviceStatusType.Free or DeviceStatusType.Closing) serviceMayOwn = true;
        }
        Changed?.Invoke();
    }
    public void EnsureLocalAvailable()
    {
        lock (Sync) if (!LocalOwned && (serviceMayOwn || serviceCommands != 0)) throw new InvalidOperationException("服务正在占用传感器，请先关闭服务连接。");
    }
    public void EnsureServiceAvailable()
    {
        lock (Sync) if (LocalOwned || localCommands != 0) throw new InvalidOperationException("本地传感器正在使用，请先关闭本地连接。");
    }
    public void BeginLocalCommand() { lock (Sync) { EnsureLocalAvailable(); localCommands++; } }
    public void EndLocalCommand() { lock (Sync) localCommands--; }
    public void BeginServiceCommand(string operation)
    {
        lock (Sync) { EnsureServiceAvailable(); serviceCommands++; if (operation is "Open" or "Reopen") serviceMayOwn = true; }
    }
    public void EndServiceCommand() { lock (Sync) serviceCommands--; }
}
