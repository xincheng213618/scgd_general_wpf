using System;
using System.IO;
using System.Linq;

namespace YoloObjectDetection;

public static class ClassNames
{
    public static string[] Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("找不到类别文件", path);

        var names = File.ReadLines(path)
            .Select(ParseLine)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        if (names.Length == 0)
            throw new InvalidOperationException($"类别文件为空: {path}");

        return names;
    }

    private static string ParseLine(string line)
    {
        string value = line.Trim();
        if (value.Length == 0 || value.StartsWith('#'))
            return string.Empty;

        int separatorIndex = value.IndexOfAny([':', ',']);
        if (separatorIndex > 0 && int.TryParse(value[..separatorIndex], out _))
            value = value[(separatorIndex + 1)..].Trim();

        return value;
    }
}