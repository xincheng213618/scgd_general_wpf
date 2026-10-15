using ColorVision.Core;
using System.Windows.Media.Imaging;

namespace CameraTest.Application;

/// <summary>Read-only presentation of one recorded edge. Keeps only the cropped pixels, not a live camera frame.</summary>
public sealed class MeasurementDetailSnapshot
{
    public FrameAnalysis Measurement { get; }
    public BmwTargetAnalysis Target { get; }
    public BmwEdgeAnalysis Edge { get; }
    public IReadOnlyList<MetricRow> Rows { get; }
    public BitmapSource Preview { get; }
    public bool IsSearchPreview { get; }
    public double Frequency { get; }

    public MeasurementDetailSnapshot(TestFrame frame, FrameAnalysis measurement, string targetId, string edgeId, double frequency)
    {
        if (frame.Id != measurement.FrameId) throw new ArgumentException("图像与测量结果不是同一帧，请重新分析。");
        if (!double.IsFinite(frequency) || frequency < 0 || frequency > .5) throw new ArgumentOutOfRangeException(nameof(frequency));
        Measurement = measurement;
        Target = measurement.Targets.Single(t => t.Id == targetId);
        Edge = Target.Edges.Single(e => e.Id.ToString() == edgeId);
        Rows = measurement.Rows().Where(r => r.Target == targetId && r.Edge == edgeId).ToArray();
        Frequency = frequency;
        var bounds = new RoiRect(0, 0, frame.Data.Width, frame.Data.Height);
        IsSearchPreview = !BmwSfrRoiSettings.IsInside(Edge.Roi, bounds);
        var roi = IsSearchPreview ? Target.SearchRoi : Edge.Roi;
        if (!BmwSfrRoiSettings.IsInside(roi, bounds)) throw new ArgumentException("预览区域超出原图。");
        int bytesPerPixel = frame.Data.Channels * frame.Data.BitDepth / 8;
        int stride = checked(roi.Width * bytesPerPixel);
        var pixels = new byte[checked(stride * roi.Height)];
        for (int y = 0; y < roi.Height; y++)
            Buffer.BlockCopy(frame.Data.Pixels, (roi.Y + y) * frame.Data.Stride + roi.X * bytesPerPixel, pixels, y * stride, stride);
        Preview = new TestFrame(new(pixels, roi.Width, roi.Height, frame.Data.BitDepth, frame.Data.Channels, stride, frame.Data.CapturedAt), frame.Source).CreateBitmap();
    }
}
