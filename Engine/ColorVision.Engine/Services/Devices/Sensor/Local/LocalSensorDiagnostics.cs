using log4net;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using MQTTMessageLib.Sensor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal enum LocalSensorDiagnosticKind { Sent, Received, Log, Error }

internal sealed record LocalSensorDiagnosticEntry(long Sequence, DateTime Time, LocalSensorDiagnosticKind Kind,
    string Endpoint, string EventName, string CommandName, long CommandId, int Attempt, string Encoding,
    int ByteCount, int CapturedByteCount, string Text, string Hex)
{
    public string Direction => EngineLocalization.Get(Kind switch { LocalSensorDiagnosticKind.Sent => "发送", LocalSensorDiagnosticKind.Received => "接收", LocalSensorDiagnosticKind.Error => "错误", _ => "日志" });
    public string DisplayText { get; } = Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received
        ? SensorCommandTextFormatter.ToReadableText(Text, Encoding == "Hex") : Text;
    public string Preview => (DisplayText.Length > 180 ? DisplayText[..180] + "…" : DisplayText).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    public string CommandDisplay => CommandId == 0 ? CommandName : EngineLocalization.Format($"{CommandName} · #{CommandId} / 第 {Attempt} 次");
    public string Label => Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received ? CommandDisplay : EngineLocalization.Get(EventName);
    public bool IsTruncated => CapturedByteCount < ByteCount;
    public int FragmentCount { get; init; } = 1;
}

/// <summary>Per-device history survives window closure. Payload and entry caps bound diagnostic memory.</summary>
internal sealed class LocalSensorDiagnostics
{
    internal const int MaxEntries = 500;
    internal const int MaxPayloadBytes = 16 * 1024;
    private static readonly ILog log = LogManager.GetLogger(typeof(LocalSensorDiagnostics));
    private readonly object sync = new();
    private readonly Queue<LocalSensorDiagnosticEntry> entries = new();
    private long revision;
    public string DeviceCode { get; set; } = string.Empty;

    internal LocalSensorDiagnosticEntry[]? GetSnapshot(long knownRevision, out long currentRevision)
    {
        lock (sync)
        {
            currentRevision = revision;
            return knownRevision == revision ? null : entries.ToArray();
        }
    }

    internal void Clear() { lock (sync) { entries.Clear(); ++revision; } }

    internal static LocalSensorDiagnosticEntry[] SummarizeResponses(LocalSensorDiagnosticEntry[] snapshot)
    {
        var responses = snapshot.Where(IsCommandReceive).GroupBy(ResponseKey).ToDictionary(group => group.Key, group => group.ToArray());
        var summary = new List<LocalSensorDiagnosticEntry>(snapshot.Length);
        foreach (var entry in snapshot)
        {
            if (!IsCommandReceive(entry)) summary.Add(entry);
            else if (responses.Remove(ResponseKey(entry), out var fragments))
                summary.Add(fragments.Length == 1 ? entry : CombineFragments(fragments));
        }
        return summary.ToArray();
    }

    private static bool IsCommandReceive(LocalSensorDiagnosticEntry entry) => entry.Kind == LocalSensorDiagnosticKind.Received && entry.CommandId != 0;
    private static (string Endpoint, long CommandId, int Attempt) ResponseKey(LocalSensorDiagnosticEntry entry) => (entry.Endpoint, entry.CommandId, entry.Attempt);

    private static LocalSensorDiagnosticEntry CombineFragments(LocalSensorDiagnosticEntry[] fragments)
    {
        var first = fragments[0];
        byte[] bytes = new byte[Math.Min(fragments.Sum(fragment => fragment.CapturedByteCount), MaxPayloadBytes)];
        int captured = 0;
        foreach (var fragment in fragments)
        {
            int length = Math.Min(bytes.Length - captured, fragment.CapturedByteCount);
            Convert.FromHexString(fragment.Hex.AsSpan(0, length * 2), bytes.AsSpan(captured), out _, out _);
            captured += length;
            if (captured == bytes.Length || fragment.IsTruncated) break;
        }

        if (captured != bytes.Length) bytes = bytes[..captured];
        var command = new LocalSensorCommand { CmdType = Enum.Parse<SensorCmdType>(first.Encoding) };
        return new LocalSensorDiagnosticEntry(first.Sequence, first.Time, first.Kind, first.Endpoint, "累计接收",
            first.CommandName, first.CommandId, first.Attempt, first.Encoding, fragments.Sum(fragment => fragment.ByteCount),
            captured, command.Decode(bytes), Convert.ToHexString(bytes)) { FragmentCount = fragments.Length };
    }

    internal void AddLog(string endpoint, string eventName, string message, bool error = false)
        => Add(error ? LocalSensorDiagnosticKind.Error : LocalSensorDiagnosticKind.Log, endpoint, eventName, "", 0, 0, "", 0, 0,
            message.Length > MaxPayloadBytes ? message[..MaxPayloadBytes] + "…（日志已截断）" : message, "");

    internal void AddTraffic(LocalSensorDiagnosticKind kind, string endpoint, LocalSensorCommand? command, long commandId, int attempt, ReadOnlySpan<byte> bytes)
    {
        byte[] captured = bytes[..Math.Min(bytes.Length, MaxPayloadBytes)].ToArray();
        // Preserve raw Hex for unsolicited bytes; display uses the template editor's byte-wise echo.
        string hex = Convert.ToHexString(captured);
        string text = command?.Decode(captured) ?? hex;
        Add(kind, endpoint, kind == LocalSensorDiagnosticKind.Sent ? "发送指令" : "接收分片", command?.Name ?? "无等待指令",
            commandId, attempt, command?.CmdType.ToString() ?? "Hex", bytes.Length, captured.Length, text, hex);
    }

    private void Add(LocalSensorDiagnosticKind kind, string endpoint, string eventName, string commandName, long commandId,
        int attempt, string encoding, int byteCount, int capturedByteCount, string text, string hex)
    {
        LocalSensorDiagnosticEntry entry;
        lock (sync)
        {
            entry = new(++revision, DateTime.Now, kind, endpoint, eventName, commandName, commandId, attempt, encoding, byteCount, capturedByteCount, text, hex);
            entries.Enqueue(entry);
            while (entries.Count > MaxEntries) entries.Dequeue();
        }
        // Use the ordinary application log so feedback collection includes communication evidence.
        try
        {
            string escaped = text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            string message = $"LocalSensor [{DeviceCode}] [{endpoint}] {entry.Direction} {eventName} {entry.CommandDisplay} Encoding={encoding} Bytes={byteCount} Captured={capturedByteCount} Text={escaped} Hex={hex}";
            if (kind == LocalSensorDiagnosticKind.Error) log.Error(message);
            else log.Info(message);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"LocalSensor diagnostic log failed: {ex.Message}"); }
    }
}
