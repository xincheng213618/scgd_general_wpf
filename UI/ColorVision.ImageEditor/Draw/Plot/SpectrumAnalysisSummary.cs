using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw;

/// <summary>Neutral presentation contract shared by the main application and spectrum plugin.</summary>
public sealed record SpectrumAnalysisSummary
{
    public double? ColorTemperature { get; init; }
    public double? Duv { get; init; }
    public double? Ra { get; init; }
    public double? Luminance { get; init; }
    public double? LuminousFlux { get; init; }
    public double? PeakWavelength { get; init; }
    public double? Fwhm { get; init; }
    public double? MeanWavelength { get; init; }
    public IReadOnlyList<double?> RenderingIndices { get; init; } = Array.Empty<double?>();
    public string RenderingStatus { get; init; } = "";
    public string TemperatureStatus { get; init; } = "";
}
