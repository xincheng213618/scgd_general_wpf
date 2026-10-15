using MQTTMessageLib.Sensor;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

/// <summary>The template owns command timing and framing; flow nodes add no second timeout.</summary>
public sealed class LocalSensorCommand
{
    public string Name { get; set; } = string.Empty;
    public SensorCmdType CmdType { get; set; } = SensorCmdType.Ascii;
    public string Request { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public int Timeout { get; set; } = 1000;
    public int Delay { get; set; }
    public int RetryCount { get; set; }

    internal LocalSensorCommand Snapshot() => (LocalSensorCommand)MemberwiseClone();

    internal void Validate()
    {
        if (!Enum.IsDefined(CmdType) || CmdType == SensorCmdType.None) throw new ArgumentException("传感器指令类型无效。");
        if (Encode(Request).Length == 0) throw new ArgumentException("发送指令不能为空。");
        if (Delay < 0 || RetryCount < 0) throw new ArgumentException("指令延时和重试次数不能为负数。");
        _ = Encode(Response);
    }

    internal byte[] Encode(string? text)
    {
        text ??= string.Empty;
        if (CmdType == SensorCmdType.Hex)
        {
            string hex = text.Replace("0x", "", StringComparison.OrdinalIgnoreCase);
            foreach (char separator in new[] { ' ', '\t', '\r', '\n', ',', ';', '-' }) hex = hex.Replace(separator.ToString(), "");
            try { return Convert.FromHexString(hex); }
            catch (FormatException ex) { throw new ArgumentException("十六进制指令或回包格式无效。", ex); }
        }
        return GetEncoding().GetBytes(text.Replace("\\r", "\r").Replace("\\n", "\n").Replace("\\t", "\t"));
    }

    internal string Decode(byte[] bytes) => CmdType == SensorCmdType.Hex ? Convert.ToHexString(bytes) : GetEncoding().GetString(bytes);

    private Encoding GetEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return CmdType switch
        {
            SensorCmdType.Ascii => Encoding.ASCII,
            SensorCmdType.UTF8 => Encoding.UTF8,
            SensorCmdType.GBK => Encoding.GetEncoding("GBK"),
#pragma warning disable SYSLIB0001
            SensorCmdType.UTF7 => Encoding.UTF7,
#pragma warning restore SYSLIB0001
            _ => throw new ArgumentException("不支持的传感器指令编码。")
        };
    }

    // Read the existing service format without rewriting the template or using ValueB.
    internal static LocalSensorCommand FromTemplate(string? valueA)
    {
        if (string.IsNullOrWhiteSpace(valueA)) throw new InvalidDataException("传感器模板指令不能为空。");
        string[] parts = (valueA ?? string.Empty).Split(',');
        // Older services also accepted one to four fields with these defaults.
        SensorCmdType type = SensorCmdType.Hex;
        int timeout = 5000, delay = 1000, retries = 3;
        int typeIndex = parts.Length >= 5 ? parts.Length - 3 : 2;
        if (parts.Length >= 3 && (!Enum.TryParse(parts[typeIndex], out type) || !Enum.IsDefined(type)))
            throw new InvalidDataException("传感器模板的编码无效。");
        if (parts.Length >= 4)
        {
            string[] times = parts[typeIndex + 1].Split('/');
            if (times.Length > 2 || !int.TryParse(times[0], out timeout) || (times.Length == 2 && !int.TryParse(times[1], out delay)))
                throw new InvalidDataException("传感器模板的超时或延时无效。");
        }
        if (parts.Length >= 5 && !int.TryParse(parts[^1], out retries)) throw new InvalidDataException("传感器模板的重试次数无效。");
        return new LocalSensorCommand
        {
            CmdType = type, Request = parts[0], Response = parts.Length >= 5 ? string.Join(",", parts[1..^3]) : parts.Length >= 2 ? parts[1] : string.Empty,
            Timeout = timeout, Delay = delay, RetryCount = retries
        };
    }
}

public sealed class LocalSensorCommandResult
{
    public string Name { get; init; } = string.Empty;
    public string RequestHex { get; init; } = string.Empty;
    public string ResponseHex { get; init; } = string.Empty;
    public string ResponseText { get; init; } = string.Empty;
    public int Attempts { get; init; }
    public long ElapsedMilliseconds { get; init; }
}
