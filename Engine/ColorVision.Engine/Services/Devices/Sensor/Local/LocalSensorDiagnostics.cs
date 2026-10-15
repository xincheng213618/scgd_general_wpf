using log4net;
using ColorVision.Engine.Services.Devices.Sensor.Templates;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal enum LocalSensorDiagnosticKind { Sent, Received, Log, Error }

internal sealed record LocalSensorDiagnosticEntry(long Sequence, DateTime Time, LocalSensorDiagnosticKind Kind,
    string Endpoint, string EventName, string CommandName, long CommandId, int Attempt, string Encoding,
    int ByteCount, int CapturedByteCount, string Text, string Hex)
{
    public string Direction => Kind switch { LocalSensorDiagnosticKind.Sent => "发送", LocalSensorDiagnosticKind.Received => "接收", LocalSensorDiagnosticKind.Error => "错误", _ => "日志" };
    public string DisplayText { get; } = Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received
        ? SensorCommandTextFormatter.ToReadableText(Text, Encoding == "Hex") : Text;
    public string Preview => (DisplayText.Length > 180 ? DisplayText[..180] + "…" : DisplayText).Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    public string CommandDisplay => CommandId == 0 ? CommandName : $"{CommandName} · #{CommandId} / 第 {Attempt} 次";
    public string Label => Kind is LocalSensorDiagnosticKind.Sent or LocalSensorDiagnosticKind.Received ? CommandDisplay : EventName;
    public bool IsTruncated => CapturedByteCount < ByteCount;
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
