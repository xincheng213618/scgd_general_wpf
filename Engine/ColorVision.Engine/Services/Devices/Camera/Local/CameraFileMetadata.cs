using ColorVision.FileIO;
using ColorVision.ImageEditor.Tif;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

/// <summary>Optional acquisition/provenance records. They never authorize color replay.</summary>
internal static class CameraFileMetadata
{
    internal const string AcquisitionKind = "colorvision.acquisition.camera";
    internal const string ProvenanceKind = "colorvision.calibration.provenance";
    private static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(CameraFileMetadata));

    internal static void Save<T>(string path, string kind, T record)
        => CVFileMetadata.SetProperty(path, kind, 1, Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(record)));

    internal static (ImageAcquisitionRecord? Acquisition, ImageCalibrationSummary? Provenance) Read(string path)
    {
        try
        {
            var properties = CVFileMetadata.Read(path);
            T? ReadRecord<T>(string kind) where T : class
            {
                if (!properties.TryGetValue(kind, out var property) || property.Version != 1) return null;
                try { return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(property.Value)); }
                catch (JsonException error) { Log.Warn($"Invalid optional image record: {kind}", error); return null; }
            }
            return (ReadRecord<ImageAcquisitionRecord>(AcquisitionKind), ReadRecord<ImageCalibrationSummary>(ProvenanceKind));
        }
        catch (Exception error) when (error is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            Log.Warn("Optional image acquisition records are unavailable.", error);
            return default;
        }
    }

    internal static void ApplyStored(ColorVisionTiffParameters parameters, string path)
    {
        (parameters.Acquisition, parameters.CalibrationSummary) = Read(path);
        if (parameters.Acquisition?.NdPort is int ndPort) parameters.NdPort = ndPort;
    }

    internal static ColorVisionTiffParameters CreateParameters(LocalFrameMetadata metadata,
        ColorCalibrationSnapshot? calibration, ImageCalibrationSummary? provenance)
        => new()
        {
            SourceType = "CVRAW", ExportedChannel = "Src", Rows = metadata.Height, Cols = metadata.Width,
            Bpp = metadata.SourceBpp, SourceChannels = metadata.Channels, Gain = metadata.Gain,
            Exposure = (float[])metadata.Exposure.Clone(), NdPort = metadata.Acquisition?.NdPort ?? 0,
            Acquisition = metadata.Acquisition, CalibrationSummary = calibration?.ToSummary() ?? provenance,
            Calibration = calibration == null ? null : JToken.FromObject(calibration), PixelRole = "record-only"
        };
}
