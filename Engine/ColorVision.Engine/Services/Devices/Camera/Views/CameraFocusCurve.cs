using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Draw;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Camera.Views;

internal static class CameraFocusCurve
{
    internal static string Title(string? evaluation)
    {
        string title = EngineLocalization.Get("CameraFocusChart_AutoFocus");
        return string.IsNullOrEmpty(evaluation) ? title : $"{title} · {evaluation}";
    }

    internal static string BestReadout(LocalAutoFocusResult result) => result.VidPosition is double vid
        ? string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("CameraFocusChart_BestWithVid"), result.Position, result.Score, vid)
        : string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get("CameraFocusChart_Best"), result.Position, result.Score);

    internal static void ConfigureAxes(Plot chart, bool video)
    {
        string xLabel = EngineLocalization.Get(video ? "CameraFocusChart_Seconds" : "CameraFocusChart_Position");
        string yLabel = EngineLocalization.Get("CameraFocusChart_Score");
        chart.XLabel(xLabel);
        chart.YLabel(yLabel);
        chart.Axes.Bottom.Label.FontName = Fonts.Detect(xLabel);
        chart.Axes.Left.Label.FontName = Fonts.Detect(yLabel);
        chart.Axes.Bottom.Label.FontSize = chart.Axes.Left.Label.FontSize = 12;
    }

    internal static void AddSamples(Plot chart, Coordinates[] samples, bool showMarkers, LocalAutoFocusResult? bestFocus)
    {
        if (samples.Length != 0)
        {
            var curve = chart.Add.Scatter(samples);
            curve.Color = Color.FromHex("#3196CE");
            curve.MarkerSize = showMarkers || samples.Length == 1 ? 4 : 0;
        }
        if (bestFocus != null)
            chart.Add.Marker(bestFocus.Position, bestFocus.Score, MarkerShape.FilledCircle, 9, Colors.Orange);

        chart.Axes.AutoScale();
    }

    internal static void RenderPng(string path, string deviceCode, string evaluation,
        IReadOnlyDictionary<int, double> samples, LocalAutoFocusResult bestFocus)
    {
        using var plot = new Plot();
        SpectrumPlotStyle.ApplyTheme(plot, dark: false);
        string heading = $"{Title(evaluation)} · {deviceCode}\n{BestReadout(bestFocus)}";
        plot.Title(heading, 18);
        plot.Axes.Title.Label.FontName = Fonts.Detect(heading);
        ConfigureAxes(plot, video: false);
        var points = samples.OrderBy(sample => sample.Key).Select(sample => new Coordinates(sample.Key, sample.Value)).ToArray();
        AddSamples(plot, points, showMarkers: true, bestFocus);

        plot.SavePng(path, 1120, 660);
    }
}
