using System;

namespace ColorVision.ImageEditor.Tif;

/// <summary>Recorded at acquisition, independently of subsequent device configuration or rendering.</summary>
public sealed record ImageCameraInformation
{
    public string? DeviceName { get; init; }
    public string? DeviceCode { get; init; }
    public string? CameraId { get; init; }
    public string? CameraCode { get; init; }
    public string? Model { get; init; }
    public string? Mode { get; init; }
    public string? Manufacturer { get; init; }
    public string? SerialNumber { get; init; }
}

public sealed record ImageAcquisitionRecord
{
    public ImageCameraInformation? Camera { get; init; }
    public ImageOpticalCenter? OpticalCenter { get; init; }
    public DateTime? CapturedUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int BitsPerChannel { get; init; }
    public int Channels { get; init; }
    public float Gain { get; init; }
    public float[] Exposure { get; init; } = [];
    public string? ExposureUnit { get; init; }
    public string? ChannelOrder { get; init; }
    public int? NdPort { get; init; }
    public int? AverageCount { get; init; }
}

/// <summary>Principal point in the camera's unflipped image coordinates, in pixels.</summary>
public sealed record ImageOpticalCenter(double X, double Y);

public sealed record ImageCalibrationSource
{
    public int CalibrationType { get; init; }
    public string? Name { get; init; }
    public string? FileName { get; init; }
    public long FileBytes { get; init; }
    public DateTime FileModifiedUtc { get; init; }
    public string? FileSha256 { get; init; }
}

/// <summary>Provenance only. Contains no coefficients and never grants measurement capabilities.</summary>
public sealed record ImageCalibrationSummary
{
    public string? Template { get; init; }
    public DateTime CalibratedUtc { get; init; }
    public int? CalibrationType { get; init; }
    public int? TransformKind { get; init; }
    public string? Backend { get; init; }
    public string? ReferenceCorrection { get; init; }
    public ImageCalibrationSource[] Sources { get; init; } = [];
}

public sealed record ImageRenderedOutput
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int BitsPerPixel { get; init; }
    public string? PixelFormat { get; init; }
}
