using ColorVision.Engine.Services.Types;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public readonly record struct CalibrationDefectPoint(uint Row, uint Column);

/// <summary>Legacy/current calibration binary formats. V1 map values are interleaved by channel.</summary>
public sealed class CalibrationBinaryDocument
{
    private readonly byte[] original;
    private readonly List<CalibrationDefectPoint> points = new();
    private readonly Dictionary<int, float> edits = new();
    private int dataOffset;
    private byte[] trailing = Array.Empty<byte>();
    public ServiceTypes Type { get; }
    public uint Width { get; private set; }
    public uint Height { get; private set; }
    public uint Channels { get; private set; } = 1;
    public uint SourceBits { get; private set; }
    public int Version { get; private set; }
    public int ValueCount { get; private set; }
    public int PointCount => points.Count;
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public bool IsMap => Type is ServiceTypes.DSNU or ServiceTypes.Uniformity;
    private CalibrationBinaryDocument(ServiceTypes type, byte[] bytes) { Type = type; original = (byte[])bytes.Clone(); }

    public static CalibrationBinaryDocument Parse(ServiceTypes type, byte[] bytes)
    {
        var doc = new CalibrationBinaryDocument(type, bytes);
        try
        {
            using var stream = new MemoryStream(bytes, false);
            using var reader = new BinaryReader(stream);
            if (type == ServiceTypes.DefectPoint)
            {
                uint count = reader.ReadUInt32();
                long required = checked(4L + count * 8L);
                if (required > bytes.Length || count > int.MaxValue) throw new FormatException("缺陷点数量超出文件长度。");
                for (int i = 0; i < count; i++) doc.points.Add(new(reader.ReadUInt32(), reader.ReadUInt32()));
                doc.trailing = reader.ReadBytes(bytes.Length - (int)required);
                return doc;
            }
            if (type == ServiceTypes.LineArity)
            {
                uint count = reader.ReadUInt32();
                if (bytes.Length <= 16 || count > int.MaxValue || 4L + count * 4L != bytes.Length) throw new FormatException("线性度表数量与文件长度不符。");
                doc.ValueCount = (int)count; doc.dataOffset = 4;
            }
            else if (doc.IsMap)
            {
                if (bytes.Length < 20) throw new FormatException("校正映射文件头不完整。");
                doc.Height = reader.ReadUInt32(); doc.Width = reader.ReadUInt32();
                uint bits = reader.ReadUInt32(), channels = reader.ReadUInt32(), source = reader.ReadUInt32();
                if (doc.Height == 0 || doc.Width == 0) throw new FormatException("映射尺寸必须大于零。");
                long pixels = checked((long)doc.Height * doc.Width);
                int size = type == ServiceTypes.DSNU ? 2 : 4;
                if (checked(8L + pixels * size) == bytes.Length) { doc.Version = 0; doc.dataOffset = 8; doc.ValueCount = checked((int)pixels); }
                else
                {
                    long count = checked(pixels * channels);
                    if (channels == 0 || (source != 8 && source != 16) || bits != size * 8 || checked(20L + count * size) != bytes.Length) throw new FormatException("映射头或文件长度与 V0/V1 格式不符。");
                    doc.Version = 1; doc.dataOffset = 20; doc.Channels = channels; doc.SourceBits = source; doc.ValueCount = checked((int)count);
                }
            }
            else throw new NotSupportedException("此校正不是支持的二进制格式。");
            doc.Minimum = double.PositiveInfinity; doc.Maximum = double.NegativeInfinity;
            for (int i = 0; i < doc.ValueCount; i++)
            {
                double value = doc.GetValue(i);
                if (!double.IsFinite(value)) throw new FormatException($"第 {i} 项必须是有限数值。");
                doc.Minimum = Math.Min(doc.Minimum, value); doc.Maximum = Math.Max(doc.Maximum, value);
            }
            return doc;
        }
        catch (Exception ex) when (ex is EndOfStreamException or OverflowException) { throw new FormatException("校正文件被截断或尺寸溢出。", ex); }
    }

    public float GetValue(int index)
    {
        if (index < 0 || index >= ValueCount) throw new ArgumentOutOfRangeException(nameof(index));
        if (edits.TryGetValue(index, out var edited)) return edited;
        return Type == ServiceTypes.DSNU ? BitConverter.ToUInt16(original, dataOffset + index * 2) : BitConverter.ToSingle(original, dataOffset + index * 4);
    }

    public void SetValue(int index, float value)
    {
        if (Type != ServiceTypes.LineArity) throw new InvalidOperationException("映射仅支持查看。");
        if (index < 0 || index >= ValueCount) throw new ArgumentOutOfRangeException(nameof(index));
        if (!float.IsFinite(value)) throw new FormatException("线性度系数必须是有限浮点数。");
        edits[index] = value;
    }
    public CalibrationDefectPoint GetPoint(int index) => points[index];
    public void SetPoint(int index, CalibrationDefectPoint point) { EnsurePoints(); points[index] = point; }
    public void AddPoint(CalibrationDefectPoint point) { EnsurePoints(); points.Add(point); }
    public void RemovePoint(int index) { EnsurePoints(); points.RemoveAt(index); }
    private void EnsurePoints() { if (Type != ServiceTypes.DefectPoint) throw new InvalidOperationException("仅缺陷点文件支持坐标编辑。"); }

    public byte[] BuildBytes()
    {
        if (Type == ServiceTypes.DefectPoint)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write((uint)points.Count);
            foreach (var p in points) { writer.Write(p.Row); writer.Write(p.Column); }
            writer.Write(trailing); writer.Flush(); return stream.ToArray();
        }
        byte[] result = (byte[])original.Clone();
        foreach (var edit in edits) BitConverter.GetBytes(edit.Value).CopyTo(result, dataOffset + edit.Key * 4);
        return result;
    }
}

public sealed class CalibrationBinaryFileSnapshot
{
    public string Path { get; }
    public byte[] Bytes { get; }
    public string Hash { get; }
    public string? LastBackupPath { get; internal set; }
    internal CalibrationBinaryFileSnapshot(string path, byte[] bytes) { Path = path; Bytes = bytes; Hash = Convert.ToHexString(SHA256.HashData(bytes)); }
}

public static class CalibrationBinaryFileStore
{
    public static CalibrationBinaryFileSnapshot Load(string path) { path = System.IO.Path.GetFullPath(path); return new(path, File.ReadAllBytes(path)); }
    private static FileStream LockSource(CalibrationBinaryFileSnapshot source)
    {
        var stream = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try { if (Convert.ToHexString(SHA256.HashData(stream)) != source.Hash) throw new IOException("源校正文件已被修改，请重新加载。"); return stream; }
        catch { stream.Dispose(); throw; }
    }
    public static CalibrationBinaryFileSnapshot Save(CalibrationBinaryFileSnapshot source, byte[] bytes)
    {
        using var locked = LockSource(source);
        string temp = source.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = source.Path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N") + ".bak";
        try { WriteNew(temp, bytes); CheckPath(source); File.Replace(temp, source.Path, backup); var result = Load(source.Path); result.LastBackupPath = backup; return result; }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static CalibrationBinaryFileSnapshot SaveAs(string path, byte[] bytes, CalibrationBinaryFileSnapshot? source = null)
    {
        path = System.IO.Path.GetFullPath(path); using var locked = source == null ? null : LockSource(source);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { WriteNew(temp, bytes); if (source != null) CheckPath(source); File.Move(temp, path, false); return Load(path); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void CheckPath(CalibrationBinaryFileSnapshot source) { if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source.Path))) != source.Hash) throw new IOException("源文件已被替换，请重新加载。"); }
    private static void WriteNew(string path, byte[] bytes) { using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
}
