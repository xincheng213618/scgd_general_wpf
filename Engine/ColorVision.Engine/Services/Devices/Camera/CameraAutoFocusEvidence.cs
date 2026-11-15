using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Views;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Services.Devices.Camera;

/// <summary>Image captured at the selected focus position and curve from the same local autofocus command.</summary>
public sealed class CameraAutoFocusEvidence
{
    private readonly LocalAutoFocusResult result;
    private readonly IReadOnlyDictionary<int, double> samples;
    private readonly string deviceCode;
    private readonly string evaluation;

    internal CameraAutoFocusEvidence(BitmapSource frame, LocalAutoFocusResult result, IReadOnlyDictionary<int, double> samples,
        string deviceCode, string evaluation)
    {
        Frame = frame;
        this.result = result;
        this.samples = samples;
        this.deviceCode = deviceCode;
        this.evaluation = evaluation;
    }

    [JsonIgnore]
    public BitmapSource Frame { get; }
    public int Position => result.Position;
    public double? VidPosition => result.VidPosition;

    public void SaveCurvePng(string path, double vid) =>
        CameraFocusCurve.RenderPng(path, deviceCode, evaluation, samples, result with { VidPosition = vid });
}
