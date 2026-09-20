using System.Globalization;
using System.Linq;

namespace ColorVision.ImageEditor.Cie;

/// <summary>Shared display metrics relative to this CIE window's calculation white.</summary>
internal sealed class CiePointReadout
{
    public CieChromaticity Xy { get; }
    public CieChromaticity Uv1960 { get; }
    public CieChromaticity Uv1976 { get; }
    public CieCctResult Cct { get; }
    public CieWavelengthResult? Wavelength { get; }
    public double DistanceToWhite { get; }
    public string WhiteName { get; }
    internal static string GetWhiteName(CieChromaticity white) => CieIlluminants.Defaults
        .FirstOrDefault(p => CieAnalysisMath.Distance(p.Chromaticity, white) < 1e-9)?.Name ?? "自定义白点";
    public string CctText => Cct.IsFinite
        ? string.Create(CultureInfo.InvariantCulture, $"CCT≈{Cct.TemperatureKelvin:F0} K  Duv={Cct.Duv:+0.00000;-0.00000;0.00000}")
        : "CCT / Duv: 不适用";
    public string WavelengthText => Wavelength.HasValue
        ? $"{(Wavelength.Value.IsComplementary ? "补波长 λc" : "主波长 λd")}: {CieAnalysisRow.Format(Wavelength.Value.Wavelength, "F1")} nm"
        : "主 / 补波长: —";
    public string PurityText => Wavelength.HasValue
        ? $"{(Wavelength.Value.IsComplementary ? "紫线方向比例" : "激发纯度")}: {CieAnalysisRow.Format(Wavelength.Value.Purity * 100, "F2")}%"
        : "激发纯度: —";
    public string CursorText => string.Create(CultureInfo.InvariantCulture,
        $"x={Xy.X:F5}  y={Xy.Y:F5}    u={Uv1960.X:F5}  v={Uv1960.Y:F5}    u′={Uv1976.X:F5}  v′={Uv1976.Y:F5}\n{CctText}   {WavelengthText}   {PurityText} ({WhiteName})");

    public CiePointReadout(CieChromaticity xy, CieChromaticity white)
    {
        Xy = xy;
        WhiteName = GetWhiteName(white);
        Uv1960 = CieColorConverter.XyToCie1960uv(xy);
        Uv1976 = CieColorConverter.XyToCie1976uv(xy);
        Cct = CieAnalysisMath.EstimateCct(xy);
        Wavelength = CieGamutGeometry.Wavelength(xy, white, CieSpectrumLocus.Points);
        DistanceToWhite = CieAnalysisMath.Distance(Uv1976, CieColorConverter.XyToCie1976uv(white));
    }
}
