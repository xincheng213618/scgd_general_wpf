using System.Globalization;
using System.Text.RegularExpressions;

namespace Spectrum.Calibration.Correction;

/// <summary>
/// Parses wavelength/value text exported by common spreadsheet and spectrum tools.
/// </summary>
public static partial class SpectrumTextParser
{
    public static SpectrumSeries Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new FormatException("光谱文本为空。");

        List<double> wavelengths = [];
        List<double> values = [];
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            string line = lines[lineIndex].Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
                continue;

            string[] columns = SplitColumns(line);
            if (columns.Length != 2)
                throw new FormatException($"第 {lineIndex + 1} 行必须只有波长和数值两列。");

            double wavelength = ParseFinite(columns[0], lineIndex + 1, "波长");
            double value = ParseFinite(columns[1], lineIndex + 1, "光谱值");
            if (value < 0)
                throw new FormatException($"第 {lineIndex + 1} 行的光谱值不能为负数。");
            if (wavelengths.Count > 0 && wavelength <= wavelengths[^1])
                throw new FormatException($"第 {lineIndex + 1} 行波长必须严格递增，不能重复或倒序。");

            wavelengths.Add(wavelength);
            values.Add(value);
        }

        if (wavelengths.Count < 2)
            throw new FormatException("至少需要两行有效光谱数据。");

        return new SpectrumSeries(wavelengths, values);
    }

    private static double ParseFinite(string text, int lineNumber, string columnName)
    {
        const NumberStyles styles = NumberStyles.Float;
        if ((!double.TryParse(text, styles, CultureInfo.CurrentCulture, out double value)
             && !double.TryParse(text, styles, CultureInfo.InvariantCulture, out value))
            || !double.IsFinite(value))
        {
            throw new FormatException($"第 {lineNumber} 行的{columnName}不是有效数字: {text}");
        }
        return value;
    }

    private static string[] SplitColumns(string line)
    {
        if (line.Contains('\t'))
            return SplitDelimited(line, '\t');
        if (line.Contains(';'))
            return SplitDelimited(line, ';');

        Match whitespaceMatch = TwoWhitespaceSeparatedColumnsRegex().Match(line);
        if (whitespaceMatch.Success
            && !whitespaceMatch.Groups[1].Value.EndsWith(',')
            && !whitespaceMatch.Groups[2].Value.StartsWith(','))
        {
            return [whitespaceMatch.Groups[1].Value, whitespaceMatch.Groups[2].Value];
        }

        if (line.Contains(','))
            return SplitDelimited(line, ',');

        return WhitespaceSeparatorRegex().Split(line);
    }

    private static string[] SplitDelimited(string line, char delimiter) =>
        line.Split(delimiter, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex(@"^(\S+)\s+(\S+)$", RegexOptions.CultureInvariant)]
    private static partial Regex TwoWhitespaceSeparatedColumnsRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceSeparatorRegex();
}
