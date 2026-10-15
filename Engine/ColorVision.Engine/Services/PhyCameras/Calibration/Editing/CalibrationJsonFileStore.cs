using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.Engine.Services.PhyCameras.Calibration.Editing;

public sealed class CalibrationJsonFileSnapshot
{
    public string Path { get; }
    public string Json { get; }
    public Encoding Encoding { get; }
    public string Hash { get; }
    public string? LastBackupPath { get; internal set; }
    internal CalibrationJsonFileSnapshot(string path, string json, Encoding encoding, string hash) { Path = path; Json = json; Encoding = encoding; Hash = hash; }
}

/// <summary>File writes refuse collisions and edits made since the editor loaded the source.</summary>
public static class CalibrationJsonFileStore
{
    public static CalibrationJsonFileSnapshot Load(string path)
    {
        path = System.IO.Path.GetFullPath(path);
        byte[] bytes = File.ReadAllBytes(path);
        Encoding encoding = DetectEncoding(bytes);
        byte[] preamble = encoding.GetPreamble();
        int skip = preamble.Length != 0 && bytes.Take(preamble.Length).SequenceEqual(preamble) ? preamble.Length : 0;
        string json = encoding.GetString(bytes, skip, bytes.Length - skip);
        CalibrationJsonDocument.ParseObject(json);
        return new(path, json, encoding, Convert.ToHexString(SHA256.HashData(bytes)));
    }

    private static Encoding DetectEncoding(byte[] bytes)
    {
        if (bytes.Length >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0) return new UTF32Encoding(false, true, true);
        if (bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF) return new UTF32Encoding(true, true, true);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) return new UTF8Encoding(true, true);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE) return new UnicodeEncoding(false, true, true);
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF) return new UnicodeEncoding(true, true, true);
        return new UTF8Encoding(false, true);
    }

    private static FileStream LockSource(CalibrationJsonFileSnapshot source)
    {
        // Permit our atomic replace, but prohibit ordinary concurrent write handles.
        var stream = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try
        {
            string actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, source.Hash, StringComparison.Ordinal)) throw new IOException("源校正文件已被其他程序修改，请重新加载后保存。");
            stream.Position = 0;
            return stream;
        }
        catch { stream.Dispose(); throw; }
    }

    public static CalibrationJsonFileSnapshot Save(CalibrationJsonFileSnapshot source, string json)
    {
        CalibrationJsonDocument.ParseObject(json);
        using var lockedSource = LockSource(source);
        string temporary = source.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = source.Path + "." + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N") + ".bak";
        try
        {
            WriteNew(temporary, json, source.Encoding);
            // Recheck path identity immediately before replacing (another editor may replace it).
            if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source.Path))) != source.Hash) throw new IOException("源校正文件已被替换，请重新加载后保存。");
            File.Replace(temporary, source.Path, backup);
            var result = Load(source.Path);
            result.LastBackupPath = backup;
            return result;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static CalibrationJsonFileSnapshot SaveAs(string path, string json, CalibrationJsonFileSnapshot? source = null)
    {
        CalibrationJsonDocument.ParseObject(json);
        path = System.IO.Path.GetFullPath(path);
        using var lockedSource = source == null ? null : LockSource(source);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            WriteNew(temporary, json, source?.Encoding ?? new UTF8Encoding(false, true));
            if (source != null && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(source.Path))) != source.Hash) throw new IOException("源校正文件已被替换，请重新加载后另存为。");
            File.Move(temporary, path, false);
            return Load(path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void WriteNew(string path, string json, Encoding encoding)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        byte[] preamble = encoding.GetPreamble();
        stream.Write(preamble);
        stream.Write(encoding.GetBytes(json));
        stream.Flush(true);
    }
}
