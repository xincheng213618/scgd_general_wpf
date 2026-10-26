using ColorVision.Engine.Services.Devices.Sensor.Local;
using MQTTMessageLib.Sensor;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Engine.Services.Devices.PG.Local;

internal sealed class LocalPGProtocol(ConfigPG config, int timeout, IReadOnlyDictionary<string, string> commands)
{
    internal bool IsGecs => config.Category == "GECS.V2.4";
    private string Channel => config.Channel;

    internal static void Validate(ConfigPG config, DisplayPGConfig display)
    {
        if (config.Category == "CH431.I2C") throw new NotSupportedException("本地 PG 暂不支持 CH431.I²C，请使用 PG 服务。");
        if (string.IsNullOrWhiteSpace(config.Category)) throw new ArgumentException("请选择 PG 类型。");
        if (config.Category == "GECS.V2.4" && (!int.TryParse(config.Channel, NumberStyles.None, CultureInfo.InvariantCulture, out int channel) || channel is < 0 or > 255)) throw new ArgumentException("GECS 通道必须是 0～255 的整数，例如 01。");
        if (display.ConnectTimeout <= 0 || display.CommandTimeout <= 0) throw new ArgumentException("PG 连接和指令超时必须大于零。");
    }

    internal LocalSensorCommand[] Build(object? parameters)
    {
        var functions = parameters is JArray array ? array : parameters == null ? new JArray() : JArray.FromObject(parameters);
        if (functions.Count == 0) throw new ArgumentException("PG 指令不能为空。");
        // Validate the entire request before opening or sending anything.
        return functions.Select(function => BuildOne(function.Value<string>("Name") ?? "", function["params"] as JObject ?? function["Params"] as JObject ?? new JObject())).ToArray();
    }

    private LocalSensorCommand BuildOne(string name, JObject args)
    {
        if (!IsGecs)
        {
            string text;
            if (name == "CM_CustomCmd") text = args.Value<string>("cmdTxt") ?? "";
            else
            {
                if (!commands.TryGetValue(name, out text!) || string.IsNullOrWhiteSpace(text)) throw new NotSupportedException($"PG 类型 {config.Category} 未配置指令 {name}。");
                if (name == "CM_SwitchFramePG") text = string.Format(CultureInfo.InvariantCulture, text, Index(args));
            }
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("PG 指令不能为空。");
            return new LocalSensorCommand { Name = name, CmdType = SensorCmdType.Ascii, Request = text, Timeout = 0 };
        }

        if (name is not ("CM_SwitchFramePG" or "EFUSE.DieId") && commands.TryGetValue(name, out string? custom) && !string.IsNullOrWhiteSpace(custom))
        {
            var command = LocalSensorCommand.FromTemplate(custom);
            command.Name = name;
            return command;
        }
        string suffix = name switch
        {
            "CM_StartPG" => "POWER,ON",
            "CM_StopPG" => "POWER,OFF",
            "CM_SwitchUpPG" => "PATTERN,BACK",
            "CM_SwitchDownPG" => "PATTERN,NEXT",
            "CM_SwitchFramePG" => "PATTERN,INDEX," + Index(args),
            "KeyReset" => "KEY,RESET",
            "KeyNext" => "KEY,NEXT", "KeyBack" => "KEY,BACK", "KeyAuto" => "KEY,AUTO",
            "KeyEnter" => "KEY,ENTER", "KeyRepeat" => "KEY,REPEAT", "KeyUp" => "KEY,UP", "KeyDown" => "KEY,DOWN",
            "DEMURA.ERASE.START" => "DEMURA,ERASE,START", "DEMURA.WRITE.START" => "DEMURA,WRITE,START",
            "DEMURA.ON" => "DEMURA,ON", "DEMURA.OFF" => "DEMURA,OFF",
            "POWER.MEASURE" => "POWER,MEASURE", "OSC.TRIM" => "OSC,TRIM",
            "EFUSE.DieId" => "EFUSE,DIEID," + Argument(args, "DieId"),
            _ => throw new NotSupportedException($"GECS 不支持指令 {name}。")
        };
        string request = $"PG,{Channel},{suffix}";
        string expected = name is "DEMURA.ERASE.START" or "DEMURA.WRITE.START" ? request[..^6] + ",END,OK" : request + ",END,OK";
        return new LocalSensorCommand
        {
            Name = name, CmdType = SensorCmdType.Hex, Request = Convert.ToHexString(Packet(request)), Timeout = timeout,
            CreateResponseParser = () => new GecsResponseParser(request, expected, name == "POWER.MEASURE")
        };
    }

    private static string Argument(JObject args, string key)
    {
        string value = args.Value<string>(key)?.Trim() ?? "";
        if (value.Length == 0 || value.Any(character => character < 0x20 || character > 0x7e || character == ',')) throw new ArgumentException($"PG 参数 {key} 无效。");
        return value;
    }
    private static string Index(JObject args)
    {
        if (!int.TryParse(args["index"]?.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int value)) throw new ArgumentException("PG 画面编号必须是非负整数。");
        return value.ToString(CultureInfo.InvariantCulture);
    }

    internal static byte[] Packet(string text)
    {
        if (text.Length > ushort.MaxValue || text.Any(character => character < 0x20 || character > 0x7e)) throw new ArgumentException("GECS 指令必须为 ASCII，且不超过 65535 字节。");
        byte[] packet = new byte[text.Length + 7];
        packet[0] = 2; packet[1] = 255;
        Encoding.ASCII.GetBytes(text.Length.ToString("X4", CultureInfo.InvariantCulture) + text).CopyTo(packet, 2);
        packet[^1] = 3;
        return packet;
    }

    internal static string Payload(byte[] packet) => Encoding.ASCII.GetString(packet, 6, packet.Length - 7);

    internal static JObject Measurement(string payload)
    {
        string[] parts = payload.Split(',');
        string[] rails = ["ELVDD", "ELVSS", "VDIO", "VCI", "VBAT2", "AVDD", "VEXT1", "VEXT2", "VEXT3"];
        if ((parts.Length != 22 && (parts.Length != 24 || parts[22] != "END" || parts[23] != "OK")) || parts[0] != "PG" || parts[2] != "POWER" || parts[3] != "MEASURE" || !int.TryParse(parts[1], out int channel)) throw new InvalidDataException("GECS 电压电流回包格式无效。");
        JArray Values(int offset, string unit) => new(rails.Select((rail, index) =>
        {
            string text = parts[offset + index].Trim();
            if (text.EndsWith(unit, StringComparison.Ordinal)) text = text[..^unit.Length];
            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value)) throw new InvalidDataException($"GECS {rail} 测量值无效。");
            return new JObject { ["Name"] = rail, ["Value"] = value };
        }));
        return new JObject { ["Channel"] = channel, ["Voltages"] = Values(4, "V"), ["Currents"] = Values(13, "mA") };
    }

    private sealed class GecsResponseParser(string request, string expected, bool measurement) : ILocalSensorResponseParser
    {
        private readonly List<byte> buffer = [];
        public byte[]? Receive(ReadOnlySpan<byte> bytes)
        {
            buffer.AddRange(bytes.ToArray());
            while (buffer.Count >= 6)
            {
                if (buffer[0] != 2 || buffer[1] != 255 || !ushort.TryParse(Encoding.ASCII.GetString(buffer.Skip(2).Take(4).ToArray()), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort length)) throw new InvalidDataException("GECS 回包帧头或长度无效。");
                int total = length + 7;
                if (buffer.Count < total) return null;
                byte[] packet = buffer.Take(total).ToArray();
                buffer.RemoveRange(0, total);
                if (packet[^1] != 3) throw new InvalidDataException("GECS 回包帧尾无效。");
                string payload = Payload(packet);
                if (payload == "ALIVE" || payload.StartsWith("ALIVE,", StringComparison.Ordinal)) continue;
                if (payload.Equals(request + ",PROCESSING", StringComparison.OrdinalIgnoreCase)) continue;
                if (measurement)
                {
                    if (!payload.StartsWith(request + ",", StringComparison.Ordinal)) throw new InvalidDataException($"GECS 回包与指令不匹配：{payload}");
                    _ = Measurement(payload);
                }
                else if (payload != expected) throw new InvalidDataException($"GECS 指令失败或回包不匹配：{payload}");
                return packet;
            }
            return null;
        }
    }
}
