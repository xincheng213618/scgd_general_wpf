using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;

namespace ColorVision.ImageEditor.Cie;

public enum CieSampleBasis { Relative, Absolute, ChromaticityOnly }
public enum CieInputSpace { XyY, XYZ, UvY, Lab, Luv, SRgb, Xy }

public sealed record CieAnalysisSample(Guid Id, string Name, string Group, string Source, CieXyz Xyz, CieSampleBasis Basis)
{
    [JsonIgnore]
    public CieChromaticity Xy => CieColorConverter.XyzToCie1931xy(Xyz);

    public static CieAnalysisSample Create(string name, string group, string source, CieInputSpace space,
        double v1, double v2, double v3, CieSampleBasis basis, CieAnalysisSettings settings)
    {
        if (!double.IsFinite(v1) || !double.IsFinite(v2) || !double.IsFinite(v3))
            throw new ArgumentException("输入不能包含 NaN 或无穷大。");
        if (space == CieInputSpace.Xy) basis = CieSampleBasis.ChromaticityOnly;
        if (space == CieInputSpace.SRgb) basis = CieSampleBasis.Relative;
        CieXyz white = settings.WhiteFor(basis);
        CieXyz xyz;
        switch (space)
        {
            case CieInputSpace.XYZ: xyz = new(v1, v2, v3); break;
            case CieInputSpace.XyY: xyz = CieAnalysisMath.XyYToXyz(v1, v2, v3); break;
            case CieInputSpace.Xy: xyz = CieAnalysisMath.XyYToXyz(v1, v2, 100); break;
            case CieInputSpace.UvY:
                CieChromaticity xy = CieColorConverter.Uv1976ToXy(new(v1, v2));
                xyz = CieAnalysisMath.XyYToXyz(xy.X, xy.Y, v3);
                break;
            case CieInputSpace.Lab: xyz = CieAnalysisMath.LabToXyz(new(v1, v2, v3), white); break;
            case CieInputSpace.Luv: xyz = CieAnalysisMath.LuvToXyz(new(v1, v2, v3), white); break;
            case CieInputSpace.SRgb:
                if (v1 < 0 || v1 > 255 || v2 < 0 || v2 > 255 || v3 < 0 || v3 > 255 || v1 % 1 != 0 || v2 % 1 != 0 || v3 % 1 != 0)
                    throw new ArgumentException("sRGB 输入需要 0–255 的整数。");
                CieXyz rgb = CieColorConverter.RgbToXyz((int)v1, (int)v2, (int)v3);
                xyz = new(rgb.X * 100, rgb.Y * 100, rgb.Z * 100);
                source = "sRGB 推算 / D65";
                break;
            default: throw new ArgumentOutOfRangeException(nameof(space));
        }
        var sample = new CieAnalysisSample(Guid.NewGuid(), name.Trim(), group.Trim(), source.Trim(), xyz, basis);
        sample.Validate();
        return sample;
    }

    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 200 || Group == null || Source == null ||
            Group.Length > 200 || Source.Length > 500 || !Enum.IsDefined(Basis))
            throw new ArgumentException("样品名称不能为空（最多 200 字）；分组或来源无效。");
        CieAnalysisMath.ValidateXyz(Xyz);
        if (Basis == CieSampleBasis.ChromaticityOnly && !Xy.IsFinite)
            throw new ArgumentException("仅色坐标样品需要有效的非零 XYZ。");
    }
}

public sealed record CieAnalysisSettings
{
    public double WhiteX { get; init; } = 0.31271;
    public double WhiteY { get; init; } = 0.32902;
    public double AbsoluteWhiteLuminance { get; init; } = 100;
    public double DeltaEThreshold { get; init; } = 2;
    public double JncdStep { get; init; } = 0.004;
    public CieDiagramKind DiagramKind { get; init; } = CieDiagramKind.Cie1931xy;
    public bool ShowCct { get; init; }
    public bool ShowDaylight { get; init; }
    public bool ShowWavelength { get; init; } = true;
    public string[] Gamuts { get; init; } = new[] { "sRGB" };
    public CieChromaticity White => new(WhiteX, WhiteY);
    public CieXyz WhiteFor(CieSampleBasis basis) => CieAnalysisMath.XyYToXyz(WhiteX, WhiteY, basis == CieSampleBasis.Absolute ? AbsoluteWhiteLuminance : 100);

    public void Validate()
    {
        CieXyz white = WhiteFor(CieSampleBasis.Absolute);
        if (white.X <= 0 || white.Y <= 0 || white.Z <= 0 || !white.IsFinite ||
            !double.IsFinite(DeltaEThreshold) || DeltaEThreshold < 0 || !double.IsFinite(JncdStep) || JncdStep <= 0 ||
            !Enum.IsDefined(DiagramKind) || Gamuts == null || Gamuts.Any(n => !CieGamuts.Defaults.Any(g => g.Name == n)))
            throw new ArgumentException("参考白必须有效且 Y > 0；ΔE00 阈值 ≥ 0；JNCD 步长 > 0；图表设置必须有效。");
    }
}

public sealed record CieAnalysisSession
{
    public const int MaximumSamples = 5000;
    public int Version { get; init; } = 1;
    public CieAnalysisSettings Settings { get; init; } = new();
    public List<CieAnalysisSample> Samples { get; init; } = new();
    public Guid? ReferenceId { get; init; }

    public void Validate()
    {
        if (Version != 1) throw new ArgumentException("不支持此会话版本。");
        if (Settings == null || Samples == null || Samples.Count > MaximumSamples || Samples.Any(s => s == null))
            throw new ArgumentException($"会话数据无效；最多支持 {MaximumSamples} 个样品。");
        Settings.Validate();
        foreach (CieAnalysisSample sample in Samples) sample.Validate();
        if (Samples.Select(s => s.Id).Distinct().Count() != Samples.Count || ReferenceId.HasValue && !Samples.Any(s => s.Id == ReferenceId))
            throw new ArgumentException("会话包含重复样品 ID 或失效的参考样品。");
    }
}

public sealed class CieAnalysisRow
{
    public CieAnalysisSample Sample { get; }
    public string Name => Sample.Name;
    public string Group => Sample.Group;
    public string Source => Sample.Source;
    public string BasisText => Sample.Basis switch { CieSampleBasis.Absolute => "绝对 Y", CieSampleBasis.Relative => "相对 Y", _ => "仅色坐标" };
    public bool IsReference { get; }
    public string Role => IsReference ? "参考" : "";
    public CieChromaticity Xy => Sample.Xy;
    public CieChromaticity Uv { get; }
    public CieLab? Lab { get; }
    public CieLuv? Luv { get; }
    public CieCctResult Cct { get; }
    public double? DeltaUv { get; }
    public double? Jncd { get; }
    public double? DeltaE00 { get; }
    public double? DeltaE76 { get; }
    public double? DeltaE94 { get; }
    public double? DeltaLuv { get; }
    public double? Cmc11 { get; }
    public double? Cmc21 { get; }
    public string Result { get; }
    public string XText => Format(Xy.X, "F5");
    public string YText => Format(Xy.Y, "F5");
    public string LuminanceText => Sample.Basis == CieSampleBasis.ChromaticityOnly ? "—" : Format(Sample.Xyz.Y, "F3");
    public string CctText => Cct.IsFinite ? $"{Cct.TemperatureKelvin:F0}" : "—";
    public string DuvText => Cct.IsFinite ? Format(Cct.Duv, "+0.00000;-0.00000;0.00000") : "—";
    public string DeltaEText => Format(DeltaE00, "F4");
    public string DeltaUvText => Format(DeltaUv, "F6");
    public string JncdText => Format(Jncd, "F3");

    public CieAnalysisRow(CieAnalysisSample sample, CieAnalysisSample? reference, CieAnalysisSettings settings)
    {
        Sample = sample;
        IsReference = sample.Id == reference?.Id;
        Uv = CieColorConverter.XyToCie1976uv(Xy);
        Cct = CieAnalysisMath.EstimateCct(Xy);
        if (sample.Basis != CieSampleBasis.ChromaticityOnly)
        {
            Lab = CieAnalysisMath.XyzToLab(sample.Xyz, settings.WhiteFor(sample.Basis));
            Luv = CieAnalysisMath.XyzToLuv(sample.Xyz, settings.WhiteFor(sample.Basis));
        }
        Result = "未设参考";
        if (reference == null) return;
        CieChromaticity refUv = CieColorConverter.XyToCie1976uv(reference.Xy);
        if (Uv.IsFinite && refUv.IsFinite)
        {
            DeltaUv = CieAnalysisMath.Distance(Uv, refUv);
            Jncd = DeltaUv / settings.JncdStep;
        }
        if (Lab == null || reference.Basis == CieSampleBasis.ChromaticityOnly)
        {
            Result = "缺少亮度";
            return;
        }
        if (sample.Basis != reference.Basis)
        {
            Result = "亮度尺度不同";
            return;
        }
        CieXyz white = settings.WhiteFor(sample.Basis);
        CieLab refLab = CieAnalysisMath.XyzToLab(reference.Xyz, white);
        DeltaE00 = CieAnalysisMath.DeltaE2000(refLab, Lab.Value);
        DeltaE76 = CieAnalysisMath.DeltaE76(refLab, Lab.Value);
        DeltaE94 = CieAnalysisMath.DeltaE94(refLab, Lab.Value);
        DeltaLuv = CieAnalysisMath.DeltaLuv(CieAnalysisMath.XyzToLuv(reference.Xyz, white), Luv!.Value);
        Cmc11 = CieAnalysisMath.DeltaECmc(refLab, Lab.Value);
        Cmc21 = CieAnalysisMath.DeltaECmc(refLab, Lab.Value, 2);
        if (!double.IsFinite(DeltaE00.Value) || !double.IsFinite(DeltaE76.Value) || !double.IsFinite(DeltaE94.Value) ||
            !double.IsFinite(DeltaLuv.Value) || !double.IsFinite(Cmc11.Value) || !double.IsFinite(Cmc21.Value))
        {
            DeltaE00 = DeltaE76 = DeltaE94 = DeltaLuv = Cmc11 = Cmc21 = null;
            Result = "数值超出计算范围";
            return;
        }
        Result = IsReference ? "参考" : DeltaE00 <= settings.DeltaEThreshold ? "阈值内" : "超出阈值";
    }

    public static string Format(double? value, string format = "G10") => value.HasValue && double.IsFinite(value.Value)
        ? value.Value.ToString(format, CultureInfo.InvariantCulture) : "—";
}
