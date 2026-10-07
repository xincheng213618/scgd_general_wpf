using System;
using System.IO.Ports;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

// A connection snapshot only; local options are persisted by DisplaySensorConfig, never the resource database.
internal sealed class LocalSensorConnectionConfig : ConfigSensor
{
    public int ConnectTimeout { get; set; } = 3000;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; }
    public StopBits StopBits { get; set; } = StopBits.One;
    public bool DtrEnable { get; set; }
    public bool RtsEnable { get; set; }

    internal void ValidateLocalConnection()
    {
        if (string.IsNullOrWhiteSpace(Addr)) throw new ArgumentException("请设置传感器地址或串口。");
        if (Port <= 0 || (IsNet && Port > 65535)) throw new ArgumentException("传感器端口或波特率无效。");
        if (ConnectTimeout <= 0) throw new ArgumentException("连接超时必须大于零。");
        if (!IsNet && (DataBits is < 5 or > 8 || !Enum.IsDefined(Parity) || !Enum.IsDefined(StopBits) || StopBits == StopBits.None)) throw new ArgumentException("串口数据位、校验位或停止位无效。");
    }
}
