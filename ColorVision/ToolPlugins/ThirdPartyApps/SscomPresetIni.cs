using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ColorVision.ToolPlugins.ThirdPartyApps;

internal sealed record SscomCommandPreset(string Name, byte[] Payload, int DelayMilliseconds = 0);

internal sealed record SscomTcpEndpoint(string Host, int Port);

internal static class SscomPresetIni
{
    internal const int MaxCommands = 99;
    private const string EndOfLine = "\r\n";
    private static readonly Encoding IniEncoding = CodePagesEncodingProvider.Instance.GetEncoding(
        936, EncoderFallback.ReplacementFallback, DecoderFallback.ExceptionFallback)!;

    internal static void InitializeDefaults(string path)
    {
        var defaults = SscomSensorPresets.BuildDefaults().Concat(SscomArvrPresets.Build("DEBUG-001")).ToArray();
        try { WriteNew(path, defaults); }
        catch (IOException) when (File.Exists(path)) { }
    }

    internal static byte[] Build(IReadOnlyList<SscomCommandPreset> commands, SscomTcpEndpoint? tcpEndpoint = null)
    {
        if (commands.Count is < 1 or > MaxCommands)
            throw new ArgumentOutOfRangeException(nameof(commands), $"SSCOM 允许设置 1 至 {MaxCommands} 条指令。");
        string remoteIp = tcpEndpoint == null ? "127.0.0.1" : ValidateEndpoint(tcpEndpoint);
        int remotePort = tcpEndpoint?.Port ?? 6666;

        StringBuilder ini = new("; ColorVision SSCOM 多条字符串发送配置\r\n");
        for (int i = 0; i < MaxCommands; i++)
        {
            int slot = i + 1;
            if (i >= commands.Count)
            {
                ini.Append(CultureInfo.InvariantCulture, $"N{100 + slot}=0,,1000\r\nN{slot}=A,\r\n\r\n");
                continue;
            }

            var (metadata, payload) = FormatCommand(commands[i], slot);
            ini.Append(CultureInfo.InvariantCulture, $"N{100 + slot}={metadata}\r\nN{slot}={payload}\r\n\r\n");
        }

        // Leave the port closed and keep checksums, suffixes and immediate key sending off.
        string[] settings =
        [
            "N1051=,1", "N1052=,", "N1053=,", "N1054=,", "N1055=,A", "N1056=,1000", "N1057=,N",
            "N1058=,1", "N1059=,N", "N1060=,0", "N1061=,N", "N1062=,N", "N1063=,N", "N1064=,Y",
            "N1065=,20", "N1066=,16777215", "N1067=,1000000", $"N1068=,{remoteIp}",
            string.Create(CultureInfo.InvariantCulture, $"N1069=,{remotePort}"), "N1070=,777", "N1071=,N",
            "N1072=,N", "N1073=,N", "N1074=,1", "N1075=,0", "N1076=,N",
            tcpEndpoint == null ? "N1080=,COM1" : "N1080=,1", "N1081=,115200", "N1082=,792", "N1083=,600",
            "N1084=,0", "N1085=,0", "N1086=,418", "N1087=,120", "N1088=,宋体", "N1089=,9", "N1090=,0",
            "N1100=,汉语", ";end"
        ];
        ini.AppendJoin(EndOfLine, settings).Append(EndOfLine);
        return IniEncoding.GetBytes(ini.ToString());
    }

    internal static void AddPresets(string path, IReadOnlyList<SscomCommandPreset> commands, SscomTcpEndpoint? tcpEndpoint)
    {
        if (!File.Exists(path))
        {
            WriteNew(path, commands, tcpEndpoint);
            return;
        }

        string ini = IniEncoding.GetString(File.ReadAllBytes(path));
        var lines = ini.ReplaceLineEndings(EndOfLine).Split(EndOfLine).ToList();
        Dictionary<string, int> positions = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < lines.Count; i++)
        {
            int separator = lines[i].IndexOf('=');
            if (separator > 1 && lines[i][0] is 'N' or 'n') positions[lines[i][..separator]] = i;
        }

        Dictionary<string, string> additions = new(StringComparer.OrdinalIgnoreCase);
        string? ReadValue(string key) => positions.TryGetValue(key, out int index)
            ? lines[index][(key.Length + 1)..]
            : additions.GetValueOrDefault(key);
        void SetValue(string key, string value)
        {
            if (positions.TryGetValue(key, out int index)) lines[index] = $"{key}={value}";
            else additions[key] = value;
        }

        var slots = Enumerable.Range(1, MaxCommands).ToArray();
        Queue<int> freeSlots = new(slots.Where(slot => ReadValue($"N{slot}") is null or "" or "A," or "H,"));
        foreach (SscomCommandPreset command in commands)
        {
            var (metadata, payload) = FormatCommand(command, freeSlots.TryPeek(out int next) ? next : 1);
            if (slots.Any(existing => ReadValue($"N{100 + existing}") == metadata && ReadValue($"N{existing}") == payload))
                continue;
            if (!freeSlots.TryDequeue(out int slot))
                throw new InvalidOperationException("SSCOM 的发送列表已满。请在 SSCOM 中清出空位后重试。");
            SetValue($"N{100 + slot}", metadata);
            SetValue($"N{slot}", payload);
        }
        if (tcpEndpoint != null)
        {
            SetValue("N1068", "," + ValidateEndpoint(tcpEndpoint));
            SetValue("N1069", "," + tcpEndpoint.Port.ToString(CultureInfo.InvariantCulture));
            SetValue("N1063", ",N");
            SetValue("N1080", ",1");
        }
        int end = lines.FindIndex(line => line.Trim().Equals(";end", StringComparison.OrdinalIgnoreCase));
        if (end < 0) { end = lines.Count; lines.Add(";end"); }
        lines.InsertRange(end, additions.Select(pair => $"{pair.Key}={pair.Value}"));
        if (lines[^1].Length != 0) lines.Add(string.Empty);
        Write(path, IniEncoding.GetBytes(string.Join(EndOfLine, lines)), replace: true);
    }

    internal static void WriteNew(string path, IReadOnlyList<SscomCommandPreset> commands, SscomTcpEndpoint? tcpEndpoint = null)
        => Write(path, Build(commands, tcpEndpoint), replace: false);

    private static void Write(string path, byte[] data, bool replace)
    {
        string destination = Path.GetFullPath(path);
        string temporary = destination + $".{Guid.NewGuid():N}.tmp";
        try
        {
            using (FileStream file = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                file.Write(data);
            File.Move(temporary, destination, overwrite: replace);
        }
        finally { File.Delete(temporary); }
    }

    private static (string Metadata, string Payload) FormatCommand(SscomCommandPreset command, int slot)
    {
        string label = FormatLabel(command.Name, slot);
        if (command.Payload.Length == 0)
            throw new ArgumentException($"指令“{label}”的发送内容为空。", nameof(command));
        if (command.DelayMilliseconds < 0)
            throw new ArgumentException($"指令“{label}”的延时不能为负数。", nameof(command));

        return (string.Create(CultureInfo.InvariantCulture, $"0,{label},{command.DelayMilliseconds}"),
            "H," + string.Join(' ', command.Payload.Select(value => value.ToString("X2", CultureInfo.InvariantCulture))));
    }

    private static string FormatLabel(string name, int slot)
    {
        string label = new string(name.Select(character => character == ',' ? '，' : char.IsControl(character) ? ' ' : character).ToArray()).Trim();
        return label.Length == 0 ? string.Create(CultureInfo.InvariantCulture, $"指令 {slot}") : label;
    }

    private static string ValidateEndpoint(SscomTcpEndpoint endpoint)
    {
        string host = endpoint.Host.Trim();
        if (!IPAddress.TryParse(host, out IPAddress? address) || address.AddressFamily != AddressFamily.InterNetwork || address.ToString() != host)
            throw new ArgumentException("请填写 TCP 服务的 IPv4 地址，例如 127.0.0.1。", nameof(endpoint));
        if (endpoint.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(endpoint), "TCP 端口必须在 1 到 65535 之间。");
        return host;
    }
}
