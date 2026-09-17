using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ColorVision.ImageEditor.Cie;

public static class CieAnalysisIO
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        MaxDepth = 24
    };

    public const string CsvTemplate = "Name,Space,V1,V2,V3,Group,Source,Basis\r\n参考白,XyY,0.31271,0.32902,100,示例,手动输入,Relative\r\n样品 A,XyY,0.3187,0.3434,100,示例,手动输入,Relative\r\n";

    public static string SaveSession(CieAnalysisSession session)
    {
        session.Validate();
        return JsonSerializer.Serialize(session, JsonOptions);
    }

    public static CieAnalysisSession LoadSession(string json)
    {
        CieAnalysisSession session = JsonSerializer.Deserialize<CieAnalysisSession>(json, JsonOptions) ?? throw new ArgumentException("会话文件为空。");
        session.Validate();
        return session;
    }

    // Parse the complete batch before changing the current session. CSV and Excel TSV share this path.
    public static IReadOnlyList<CieAnalysisSample> ImportSamples(string text, CieAnalysisSettings settings)
    {
        if (text.Length > 16 * 1024 * 1024) throw new ArgumentException("导入文本超过 16 MiB。");
        List<string[]> records = ParseDelimited(text.TrimStart('\uFEFF'));
        if (records.Count < 2) throw new ArgumentException("需要表头和至少一行样品；可先下载 CSV 模板。");
        string[] header = records[0].Select(v => v.Trim()).ToArray();
        if (header.Distinct(StringComparer.Ordinal).Count() != header.Length) throw new ArgumentException("表头包含重复列名。");
        bool generic = header.Contains("Space") && header.Contains("V1") && header.Contains("V2") && header.Contains("V3");
        bool xyz = header.Contains("X") && header.Contains("Y") && header.Contains("Z");
        bool xyY = header.Contains("x") && header.Contains("y") && header.Contains("Y");
        bool xy = header.Contains("x") && header.Contains("y");
        if (!generic && !xyz && !xy) throw new ArgumentException("表头需要 Space,V1,V2,V3，或 X,Y,Z，或 x,y,Y，或 x,y（区分大小写）。");
        var samples = new List<CieAnalysisSample>();
        for (int index = 1; index < records.Count; index++)
        {
            string[] fields = records[index];
            if (fields.All(string.IsNullOrWhiteSpace)) continue;
            if (fields.Length != header.Length) throw new ArgumentException($"第 {index + 1} 行的列数与表头不一致。");
            string Field(string name, string fallback = "")
            {
                int column = Array.IndexOf(header, name);
                return column < 0 ? fallback : fields[column];
            }
            double Number(string name)
            {
                if (!double.TryParse(Field(name), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !double.IsFinite(value))
                    throw new ArgumentException($"第 {index + 1} 行 {name} 需要有限数字（小数点使用 .）。");
                return value;
            }
            try
            {
                CieInputSpace space = generic ? ParseEnum<CieInputSpace>(Field("Space")) : xyz ? CieInputSpace.XYZ : xyY ? CieInputSpace.XyY : CieInputSpace.Xy;
                CieSampleBasis basis = ParseEnum<CieSampleBasis>(Field("Basis", "Relative"));
                double a = generic ? Number("V1") : Number(xyz ? "X" : "x");
                double b = generic ? Number("V2") : Number(xyz ? "Y" : "y");
                double c = generic ? Number("V3") : xyz ? Number("Z") : xyY ? Number("Y") : 0;
                samples.Add(CieAnalysisSample.Create(Unprotect(Field("Name", $"样品 {index}")), Unprotect(Field("Group")),
                    Unprotect(Field("Source", "导入")), space, a, b, c, basis, settings));
            }
            catch (ArgumentException ex) { throw new ArgumentException($"第 {index + 1} 行：{ex.Message}", ex); }
            if (samples.Count > CieAnalysisSession.MaximumSamples) throw new ArgumentException($"一次最多导入 {CieAnalysisSession.MaximumSamples} 个样品。");
        }
        if (samples.Count == 0) throw new ArgumentException("没有可导入的样品。");
        return samples;
    }

    private static T ParseEnum<T>(string text) where T : struct, Enum =>
        Enum.TryParse(text, true, out T value) && Enum.IsDefined(value) ? value : throw new ArgumentException($"不支持的 {typeof(T).Name}：{text}");

    private static List<string[]> ParseDelimited(string text)
    {
        char separator = text.Split('\n')[0].Contains('\t') ? '\t' : ',';
        var rows = new List<string[]>();
        var fields = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, closed = false;
        void FinishField() { fields.Add(field.ToString()); field.Clear(); closed = false; }
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') { quoted = false; closed = true; }
                else field.Append(ch);
            }
            else if (ch == separator) FinishField();
            else if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                FinishField(); rows.Add(fields.ToArray()); fields.Clear();
                if (rows.Count > CieAnalysisSession.MaximumSamples + 1) throw new ArgumentException("导入行数过多。");
            }
            else if (ch == '"' && field.Length == 0 && !closed) quoted = true;
            else if (closed || ch == '"') throw new ArgumentException("CSV 引号格式无效。");
            else field.Append(ch);
        }
        if (quoted) throw new ArgumentException("CSV 引号未闭合。");
        if (field.Length > 0 || fields.Count > 0 || closed) { FinishField(); rows.Add(fields.ToArray()); }
        return rows;
    }

    public static string ExportSamples(IEnumerable<CieAnalysisRow> rows)
    {
        var csv = new StringBuilder("Name,Space,V1,V2,V3,Group,Source,Basis,x,y,u_prime,v_prime,CCT_approx_K,Duv,DeltaE00,DeltaE76,DeltaE94,DeltaEuv,CMC11,CMC21,Delta_u_prime_v_prime,JNCD,Result\r\n");
        foreach (CieAnalysisRow row in rows)
        {
            string[] fields = { Protect(row.Name), "XYZ", N(row.Sample.Xyz.X), N(row.Sample.Xyz.Y), N(row.Sample.Xyz.Z),
                Protect(row.Group), Protect(row.Source), row.Sample.Basis.ToString(), N(row.Xy.X), N(row.Xy.Y), N(row.Uv.X), N(row.Uv.Y),
                N(row.Cct.TemperatureKelvin), N(row.Cct.Duv), N(row.DeltaE00), N(row.DeltaE76), N(row.DeltaE94), N(row.DeltaLuv),
                N(row.Cmc11), N(row.Cmc21), N(row.DeltaUv), N(row.Jncd), row.Result };
            csv.AppendLine(string.Join(",", fields.Select(Quote)));
        }
        return csv.ToString();
    }

    private static string N(double? value) => value.HasValue && double.IsFinite(value.Value) ? value.Value.ToString("G17", CultureInfo.InvariantCulture) : "";
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Protect(string value) => value.Length > 0 && "=+-@\t\r\n".Contains(value[0]) ? "'" + value : value;
    private static string Unprotect(string value) => value.Length > 1 && value[0] == '\'' && "=+-@\t\r\n".Contains(value[1]) ? value[1..] : value;

    public static string ExportReport(CieAnalysisSession session, IReadOnlyList<CieAnalysisRow> rows, byte[] png)
    {
        session.Validate();
        string E(string text) => WebUtility.HtmlEncode(text);
        CieAnalysisSettings s = session.Settings;
        string reference = session.Samples.FirstOrDefault(v => v.Id == session.ReferenceId)?.Name ?? "未设置";
        double[] differences = rows.Where(r => !r.IsReference && r.DeltaE00.HasValue).Select(r => r.DeltaE00!.Value).ToArray();
        var html = new StringBuilder("<!doctype html><html lang=\"zh-CN\"><meta charset=\"utf-8\"><title>ColorVision 色度分析报告</title><style>body{font:14px 'Segoe UI','Microsoft YaHei',sans-serif;margin:32px;color:#18222d}h1{font-size:26px}table{border-collapse:collapse;width:100%;font-size:12px}td,th{padding:8px;border-bottom:1px solid #ddd;text-align:right}th{background:#edf3f9}td:first-child{text-align:left}img{max-width:800px;width:100%}p{line-height:1.7}small{color:#526170}@page{size:A4 landscape;margin:12mm}@media print{body{margin:0}tr{break-inside:avoid}thead{display:table-header-group}}</style><body><h1>ColorVision · 色度分析报告</h1>");
        html.Append($"<p>{DateTime.Now:yyyy-MM-dd HH:mm:ss} · 样品 {rows.Count} 个 · 参考样品：{E(reference)}<br>参考白 xy=({N(s.WhiteX)}, {N(s.WhiteY)})；相对参考白 Y=100；绝对参考白 Y={N(s.AbsoluteWhiteLuminance)} cd/m²<br>ΔE00 阈值={N(s.DeltaEThreshold)}；JNCD=Δu′v′/{N(s.JncdStep)}；坐标系={E(s.DiagramKind.ToString())}</p>");
        if (differences.Length > 0) html.Append($"<p>可比较样品 {differences.Length} 个 · ΔE00 平均 {differences.Average():F4} · 最大 {differences.Max():F4} · 超阈值 {differences.Count(v => v > s.DeltaEThreshold)} 个</p>");
        html.Append($"<p><small>图像为导出时的当前视图（含筛选与缩放）；下表和统计包含会话全部样品。</small></p><img alt=\"色度图\" src=\"data:image/png;base64,{Convert.ToBase64String(png)}\"><table><thead><tr>");
        string[] headers = { "名称", "分组", "来源", "尺度", "x", "y", "Y", "CCT≈K", "Duv", "ΔE00", "Δu′v′", "JNCD", "判定" };
        foreach (string header in headers) html.Append($"<th>{E(header)}</th>");
        html.Append("</tr></thead><tbody>");
        foreach (CieAnalysisRow row in rows)
        {
            string[] cells = { row.Name, row.Group, row.Source, row.BasisText, row.XText, row.YText, row.LuminanceText,
                row.CctText, row.DuvText, row.DeltaEText, row.DeltaUvText, row.JncdText, row.Result };
            html.Append("<tr>");
            foreach (string cell in cells) html.Append($"<td>{E(cell)}</td>");
            html.Append("</tr>");
        }
        html.Append("</tbody></table><p><small>CIE 1931 2°；所有 Lab/Luv 使用指定参考白，无隐式色适应。CCT 为 1667–25000 K 范围近似，远离黑体轨迹不报告。仅色坐标和不同亮度尺度不作完整色差判定。JNCD 步长与 ΔE00 阈值为用户约定；图中色彩仅供示意。CSV 包含更多色差结果，会话 JSON 保存全部条件。</small></p></body></html>");
        return html.ToString();
    }
}
