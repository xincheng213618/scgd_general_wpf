using System;
using System.Collections.Generic;

namespace YoloObjectDetection;

public sealed class FrameDetectionResult
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Source { get; set; } = string.Empty;
    public int FrameWidth { get; set; }
    public int FrameHeight { get; set; }
    public float ConfidenceThreshold { get; set; }
    public float NmsThreshold { get; set; }
    public double InferenceMs { get; set; }
    public RectExport? DetectionRoi { get; set; }
    public string InspectionStatus { get; set; } = "NotConfigured";
    public List<InspectionRegionResult> InspectionRegions { get; set; } = [];
    public List<DetectionExport> Detections { get; set; } = [];
}

public sealed class DetectionExport
{
    public int ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public float Confidence { get; set; }
    public RectExport BoundingBox { get; set; } = new();
}

public sealed class InspectionRegionResult
{
    public string Name { get; set; } = string.Empty;
    public string RequiredClass { get; set; } = string.Empty;
    public int MinCount { get; set; }
    public int FoundCount { get; set; }
    public bool Passed { get; set; }
    public RectExport Region { get; set; } = new();
}

public sealed class RectExport
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}