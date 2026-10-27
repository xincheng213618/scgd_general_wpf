using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms.Evidence;
using ProjectARVRPro.AlgorithmEvidence;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

[Collection(nameof(FlowExecutionStatusTestGroup))]
public sealed class AlgorithmEvidenceTests
{
    [Fact]
    public void SourcesIncludeUnmarkedFallbackAndNeverUseAnnotatedResult()
    {
        ProjectARVRReuslt record = new() { FileName = "removed.cvraw", SavedSourceImageFileName = "source.tiff", SavedResultImageFileName = "marked.png" };
        Assert.Equal("source.tiff", Assert.Single(ArvrEvidenceImages.GetSources(record, path => path != "removed.cvraw")).FilePath);
        Assert.Empty(ArvrEvidenceImages.GetSources(new() { SavedResultImageFileName = "marked.png" }, _ => true));
        Assert.True(ArvrEvidenceImages.IsW51(new() { Model = "White51_Test_JX2.0" }));
        Assert.True(ArvrEvidenceImages.IsW51(new() { Model = "Test-W51" }));
        Assert.False(ArvrEvidenceImages.IsW51(new() { Model = "White510" }));
        Assert.Empty(ArvrEvidenceImages.Select([new() { Model = "Black" }], true));
        Assert.Single(ArvrEvidenceImages.Select([new() { Model = "Black" }], false));
    }

    [Fact]
    public void ComparisonUsesSameInputAndOneAlgorithmFailureDoesNotStopOthers()
    {
        IntPtr pixels = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(pixels, 123);
            HImage image = new() { rows = 1, cols = 1, channels = 1, depth = 8, stride = 1, pData = pixels, isDispose = true };
            List<string> calls = [];
            IReadOnlyList<ImageEvidenceObservation> results = ImageEvidenceRunner.Run(image,
            [
                new("test.broken", "旧算法", input => { calls.Add("old"); Assert.Equal(pixels, input.pData); throw new InvalidOperationException("failure"); }),
                new("test.working", "新算法", input => { calls.Add("new"); Assert.Equal(123, Marshal.ReadByte(input.pData)); return Success("test.working"); })
            ]);
            Assert.Equal(["old", "new"], calls);
            Assert.Equal(AlgorithmResultStatus.Failed, results[0].Result.Status);
            Assert.Equal(AlgorithmResultStatus.Succeeded, results[1].Result.Status);
            Assert.Equal(123, Marshal.ReadByte(pixels));
            foreach (ImageEvidenceObservation result in results) result.Result.Dispose();
        }
        finally { Marshal.FreeHGlobal(pixels); }
    }

    [Fact]
    public void CancellationDisposesCompletedArtifactsAndDoesNotStartNextAlgorithm()
    {
        using CancellationTokenSource cancellation = new();
        AlgorithmResult first = Success("test.first");
        Assert.Throws<OperationCanceledException>(() => ImageEvidenceRunner.Run(new HImage(),
        [
            new("test.first", "first", _ => { cancellation.Cancel(); return first; }),
            new("test.second", "second", _ => throw new Xunit.Sdk.XunitException("Unexpected second call"))
        ], cancellation.Token));
        Assert.True(first.IsDisposed);
    }

    [Fact]
    public void NextImageSkipsRemovedFilesAndReplacesPreviousObservations()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ColorVision-Evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                string source = Path.Combine(directory, "source.png");
                PngBitmapEncoder encoder = new();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Rgb48, null,
                    new ushort[] { 1000, 2000, 3000, 1000, 2000, 3000, 1000, 2000, 3000, 1000, 2000, 3000 }, 12)));
                using (FileStream stream = File.Create(source)) encoder.Save(stream);
                int calls = 0;
                AlgorithmEvidenceWindow window = new(
                [
                    new() { Id = 1, Model = "W51", FileName = Path.Combine(directory, "missing.png") },
                    new() { Id = 2, Model = "W51", FileName = source },
                    new() { Id = 3, Model = "W51", FileName = Path.Combine(directory, "also-missing.png") },
                    new() { Id = 4, Model = "W51", FileName = source }
                ], algorithms: [new("test.validation", "Test", image =>
                {
                    Assert.Equal(16, image.depth);
                    Assert.Equal(3, image.channels);
                    Assert.Equal(3000, Marshal.ReadInt16(image.pData));
                    Assert.Equal(1000, Marshal.ReadInt16(image.pData, 4));
                    Interlocked.Increment(ref calls);
                    return Success("test.validation");
                })]);
                try
                {
                    Complete(window.ResetAsync());
                    Assert.Equal(1, calls);
                    Assert.Contains("ID：2", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Single(((TabControl)window.FindName("ResultsTabs")).Items);
                    Complete(window.NavigateAsync(1));
                    Assert.Equal(2, calls);
                    Assert.Contains("ID：4", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Single(((TabControl)window.FindName("ResultsTabs")).Items);
                    Assert.Contains("缺图 2 条", ((TextBlock)window.FindName("StatusText")).Text);
                }
                finally { window.Close(); }
            });
        }
        finally
        {
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static AlgorithmResult Success(string id) => new() { AlgorithmId = new AlgorithmId(id), Status = AlgorithmResultStatus.Succeeded };

    private static void Complete(Task task)
    {
        DispatcherFrame frame = new();
        _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
}
