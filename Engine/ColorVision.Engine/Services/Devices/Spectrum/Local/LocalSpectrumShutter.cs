using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal interface ILocalSpectrumShutterPort : IDisposable
{
    bool IsOpen { get; }
    void Open();
    void DiscardInBuffer();
    void Write(string command);
    string ReadExisting();
}

internal sealed class LocalSpectrumShutterPort(ShutterConfig config) : ILocalSpectrumShutterPort
{
    private readonly SerialPort port = new(config.Addr, config.BaudRate) { ReadTimeout = 1000, WriteTimeout = 1000 };
    public bool IsOpen => port.IsOpen;
    public void Open() => port.Open();
    public void DiscardInBuffer() => port.DiscardInBuffer();
    public void Write(string command) => port.Write(command);
    public string ReadExisting() => port.ReadExisting();
    public void Dispose() => port.Dispose();
}

// Owned by LocalSpectrumSession and always called under its operation gate.
internal sealed class LocalSpectrumShutter(Func<ShutterConfig, ILocalSpectrumShutterPort>? createPort = null,
    Func<ShutterConfig, ILocalSpectrumFilterWheelOperation>? acquireFilterWheel = null)
{
    private readonly Func<ShutterConfig, ILocalSpectrumShutterPort> createPort = createPort ?? (config => new LocalSpectrumShutterPort(config));
    private ILocalSpectrumShutterPort? port;
    private string? address;
    private int baudRate;
    private ILocalSpectrumFilterWheelOperation? filterWheel;

    public IDisposable? AcquireOperation(ShutterConfig config)
    {
        if (!config.UseFilterWheel || filterWheel != null) return null;
        Disconnect();
        filterWheel = acquireFilterWheel?.Invoke(config) ?? new LocalSpectrumFilterWheel(config);
        return new FilterWheelScope(this);
    }

    private sealed class FilterWheelScope(LocalSpectrumShutter owner) : IDisposable
    {
        public void Dispose()
        {
            var operation = owner.filterWheel;
            owner.filterWheel = null;
            operation?.Dispose();
        }
    }

    public void Connect(ShutterConfig config)
    {
        if (config.UseFilterWheel) { using var operation = AcquireOperation(config); return; }
        if (port?.IsOpen == true && string.Equals(address, config.Addr, StringComparison.OrdinalIgnoreCase) && baudRate == config.BaudRate) return;
        Disconnect();
        try
        {
            port = createPort(config);
            port.Open();
            address = config.Addr;
            baudRate = config.BaudRate;
        }
        catch (Exception ex)
        {
            Disconnect();
            throw new InvalidOperationException($"连接快门串口 {config.Addr} 失败：{ex.Message}", ex);
        }
    }

    public void SetOpen(bool open, ShutterConfig config)
    {
        if (config.UseFilterWheel)
        {
            using var operation = AcquireOperation(config);
            filterWheel!.SetOpen(open);
            return;
        }
        string command = open ? config.OpenCmd : config.CloseCmd;
        if (string.IsNullOrEmpty(command)) throw new InvalidOperationException("请先配置快门开关指令。");
        Connect(config);
        string expected = open ? "turn on" : "turn off";
        string opposite = open ? "turn off" : "turn on";
        try
        {
            // A delayed acknowledgement from a previous command must not confirm this command.
            port!.DiscardInBuffer();
            port.Write(command);
            var response = new StringBuilder();
            var elapsed = Stopwatch.StartNew();
            int timeout = config.DelayTime > 0 ? config.DelayTime : 1000;
            do
            {
                response.Append(port.ReadExisting());
                string text = response.ToString();
                if (text.Contains(opposite, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"快门返回了相反状态：{opposite}。");
                if (text.Contains(expected, StringComparison.OrdinalIgnoreCase)) return;
                Thread.Sleep(10);
            } while (elapsed.ElapsedMilliseconds < timeout);
            throw new TimeoutException($"快门未在 {timeout} ms 内确认{(open ? "打开" : "关闭")}。");
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    public void Disconnect()
    {
        var previous = port;
        port = null;
        address = null;
        previous?.Dispose();
    }
}
