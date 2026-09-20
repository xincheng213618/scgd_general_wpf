using System.Diagnostics;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using System.Windows;

namespace HeightMapValidation.Legacy;

public partial class Window3D
{
    public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Dictionary<string, double> Timings { get; } = new();
    public int VertexCount => currentMesh?.Positions.Count ?? 0;
    public int TriangleCount => (currentMesh?.TriangleIndices.Count ?? 0) / 3;
    public int SampleWidth => newWidth;
    public int SampleHeight => newHeight;
    public static double LastBuildArraysMilliseconds { get; set; }
    public static double LastBuildCollectionsMilliseconds { get; set; }
    public List<double> MeasureHitTests()
    {
        var samples = new List<double>();
        for (int i = 0; i < 50; i++)
        {
            var point = new Point(viewport!.ActualWidth * (.3 + (i % 5) * .1), viewport.ActualHeight * (.3 + (i / 5 % 5) * .1));
            long started = Stopwatch.GetTimestamp();
            VisualTreeHelper.HitTest(viewport, point);
            samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
        return samples;
    }
    public void Animate(double seconds)
    {
        currentRotation.Quaternion = new Quaternion(new Vector3D(0, 0, 1), 15 * Math.Sin(seconds * 0.75))
            * new Quaternion(new Vector3D(1, 0, 0), 8 * Math.Sin(seconds * 0.53));
    }
    public void ResetValidationView() => ResetViewButton_Click(this, new());
}
