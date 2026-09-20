using System;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal sealed class SpectrumBackendState
{
    private static readonly object OwnershipSync = new();
    internal object Sync => OwnershipSync;
    public event Action? Changed;
    private bool preferLocal;
    private bool serviceMayOwn;
    private int serviceCommands;
    private int localCommands;
    private DeviceStatusType serviceStatus = DeviceStatusType.UnInit;
    private DeviceStatusType localStatus = DeviceStatusType.Closed;
    public bool LocalOwned { get; private set; }
    public bool OpensLocally { get { lock (Sync) return LocalOwned || (preferLocal && !serviceMayOwn && serviceCommands == 0); } }
    public DeviceStatusType Status { get { lock (Sync) return LocalOwned ? localStatus : OpensLocally ? DeviceStatusType.Closed : serviceStatus; } }

    public SpectrumBackendState(bool preference) => preferLocal = preference;
    public void SetPreference(bool value) { lock (Sync) preferLocal = value; Changed?.Invoke(); }
    public void ObserveService(DeviceStatusType value)
    {
        lock (Sync)
        {
            serviceStatus = value;
            if (value == DeviceStatusType.Closed && serviceCommands == 0) serviceMayOwn = false;
            else if (value is DeviceStatusType.Opening or DeviceStatusType.Opened or DeviceStatusType.Free or DeviceStatusType.Busy or DeviceStatusType.Closing or DeviceStatusType.SP_Continuous_Mode) serviceMayOwn = true;
        }
        Changed?.Invoke();
    }

    public void SetLocalStatus(DeviceStatusType value)
    {
        lock (Sync) { localStatus = value; LocalOwned = value != DeviceStatusType.Closed; }
        Changed?.Invoke();
    }

    public void BeginLocalCommand()
    {
        lock (Sync)
        {
            EnsureLocalAvailable();
            if (localCommands != 0) throw new InvalidOperationException("本地光谱仪正在操作，请完成或停止连续采集后再试。");
            localCommands++;
        }
    }
    public void EndLocalCommand() { lock (Sync) localCommands--; }
    public void BeginServiceCommand(string eventName)
    {
        lock (Sync)
        {
            EnsureServiceAvailable();
            serviceCommands++;
            if (eventName == "Open") serviceMayOwn = true;
        }
    }
    public void EndServiceCommand() { lock (Sync) serviceCommands--; }
    public void EnsureLocalAvailable()
    {
        lock (Sync)
            if (!LocalOwned && (serviceMayOwn || serviceCommands != 0)) throw new InvalidOperationException("服务正在占用光谱仪，请先关闭服务连接后再使用本地光谱仪。");
    }
    public void EnsureServiceAvailable()
    {
        lock (Sync)
            if (LocalOwned || localCommands != 0) throw new InvalidOperationException("本地光谱仪正在使用，不能发送服务指令。");
    }
}
