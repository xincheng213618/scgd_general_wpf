using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Draw;
using ScottPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    internal static string SavePng(string? directory, string deviceCode, string evaluation,
        IReadOnlyDictionary<int, double> samples, LocalAutoFocusResult bestFocus, DateTime completedAt)
    {
        if (samples.Count == 0) throw new ArgumentException("An autofocus curve requires at least one sample.", nameof(samples));
        string deviceFolder = deviceCode;
        foreach (char invalid in Path.GetInvalidFileNameChars())
            deviceFolder = deviceFolder.Replace(invalid, '_');
        deviceFolder = deviceFolder.Trim().TrimEnd('.');
        if (deviceFolder.Length == 0) deviceFolder = "Camera";

        string root = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "AutoFocus")
            : directory.Trim();
        string day = completedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string timestamp = completedAt.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
        string folder = Path.GetFullPath(Path.Combine(root, deviceFolder, day));
        Directory.CreateDirectory(folder);

        using var plot = new Plot();
        SpectrumPlotStyle.ApplyTheme(plot, dark: false);
        string heading = $"{Title(evaluation)} · {deviceCode}\n{BestReadout(bestFocus)}";
        plot.Title(heading, 18);
        plot.Axes.Title.Label.FontName = Fonts.Detect(heading);
        ConfigureAxes(plot, video: false);
        var points = samples.OrderBy(sample => sample.Key).Select(sample => new Coordinates(sample.Key, sample.Value)).ToArray();
        AddSamples(plot, points, showMarkers: true, bestFocus);

        string temporary = Path.Combine(folder, $".{Guid.NewGuid():N}.partial");
        try
        {
            plot.SavePng(temporary, 1120, 660);
            string file = Path.Combine(folder, timestamp + ".png");
            for (int suffix = 2; ; suffix++)
            {
                try
                {
                    File.Move(temporary, file);
                    return file;
                }
                catch (IOException) when (File.Exists(file))
                {
                    file = Path.Combine(folder, FormattableString.Invariant($"{timestamp}_{suffix}.png"));
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
