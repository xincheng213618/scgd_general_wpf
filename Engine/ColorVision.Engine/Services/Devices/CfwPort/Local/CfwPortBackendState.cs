using System;

namespace ColorVision.Engine.Services.Devices.CfwPort.Local;

internal sealed class CfwPortBackendState(bool preference)
{
    internal static object Sync { get; } = new();
    private bool preferLocal = preference, serviceMayOwn;
    private int localCommands, serviceCommands;
    private DeviceStatusType localStatus = DeviceStatusType.Closed, serviceStatus = DeviceStatusType.Closed;
    internal event Action? Changed;
    internal string? Endpoint { get; private set; }
    internal bool LocalOwned { get { lock (Sync) return localStatus != DeviceStatusType.Closed || localCommands != 0; } }
    internal bool OpensLocally { get { lock (Sync) return LocalOwned || (preferLocal && !serviceMayOwn && serviceCommands == 0); } }
    internal DeviceStatusType Status { get { lock (Sync) return OpensLocally ? localStatus : serviceStatus; } }
    internal void SetPreference(bool value) { lock (Sync) preferLocal = value; Changed?.Invoke(); }
    internal void ObserveService(DeviceStatusType value)
    {
        lock (Sync)
        {
            serviceStatus = value;
            if (value == DeviceStatusType.Closed && serviceCommands == 0) { serviceMayOwn = false; if (!LocalOwned) Endpoint = null; }
            else if (value is DeviceStatusType.Opening or DeviceStatusType.Opened or DeviceStatusType.LiveOpened or DeviceStatusType.Busy or DeviceStatusType.Free or DeviceStatusType.Closing) serviceMayOwn = true;
        }
        Changed?.Invoke();
    }
    internal void SetLocalStatus(DeviceStatusType value) { lock (Sync) localStatus = value; Changed?.Invoke(); }
    internal void EnsureAvailable(bool local)
    {
        lock (Sync)
        {
            if (local && (serviceMayOwn || serviceCommands != 0)) throw new InvalidOperationException("服务正在占用滤色轮，请先关闭服务连接。");
            if (!local && LocalOwned) throw new InvalidOperationException("本地滤色轮正在使用，请先关闭本地连接。");
        }
    }
    internal void Begin(bool local, string operation, string endpoint)
    {
        lock (Sync)
        {
            EnsureAvailable(local);
            Endpoint ??= endpoint;
            if (local) localCommands++;
            else { serviceCommands++; if (operation == "Open") serviceMayOwn = true; }
        }
        Changed?.Invoke();
    }
    internal void End(bool local)
    {
        lock (Sync)
        {
            if (local) localCommands--; else serviceCommands--;
            if (!LocalOwned && !serviceMayOwn && serviceCommands == 0) Endpoint = null;
        }
        Changed?.Invoke();
    }
}
