using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace YoloObjectDetection;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string ModelPath { get; set; } = "yolov8n.onnx";
    public string ClassesPath { get; set; } = "classes.txt";
    public float ConfidenceThreshold { get; set; } = 0.4f;
    public float NmsThreshold { get; set; } = 0.5f;
    public int CameraIndex { get; set; }
    public int CameraWidth { get; set; } = 640;
    public int CameraHeight { get; set; } = 480;
    public int CameraFps { get; set; } = 30;
    public RoiSettings DetectionRoi { get; set; } = new();
    public List<InspectionRegionSettings> InspectionRegions { get; set; } = [];

    public static AppSettings Load(string path)
    {
        if (!File.Exists(path))
            return new AppSettings();

        return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
    }

    public static string ResolvePath(string baseDirectory, string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
            return configuredPath;

        return Path.GetFullPath(Path.Combine(baseDirectory, configuredPath));
    }
}

public sealed class RoiSettings
{
    public bool Enabled { get; set; }
    public string Name { get; set; } = "Main ROI";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

public sealed class InspectionRegionSettings
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "Region";
    public string RequiredClass { get; set; } = string.Empty;
    public int MinCount { get; set; } = 1;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}