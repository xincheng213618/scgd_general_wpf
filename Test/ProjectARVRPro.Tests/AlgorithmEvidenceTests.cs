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
    public void InitialPatternRecordSelectsItsEvidenceCategoryAndKeepsSourceFiltering()
    {
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            foreach (var (model, category, count) in new[] { ("OpticCenter_Fast_JX2.0", 1, 1), ("Distortion_Test_JX2.0", 2, 2) })
            {
                AlgorithmEvidenceWindow window = new([new() { Id = 12, Model = model }], 12);
                try
                {
                    Assert.Equal(category, ((ComboBox)window.FindName("AlgorithmCategory")).SelectedIndex);
                    Assert.Equal(0, ((ComboBox)window.FindName("SourceFilter")).SelectedIndex);
                    Assert.Equal(count, ((ItemsControl)window.FindName("AlgorithmsList")).Items.Count);
                    Assert.Equal(System.Windows.Visibility.Collapsed, ((StackPanel)window.FindName("ServerAaPanel")).Visibility);
                    if (category == 2)
                    {
                        var parameters = Assert.IsType<DistortionEvidenceParameters>(((ComboBox)window.FindName("ServerDistortionParameters")).SelectedItem);
                        Assert.Contains("非历史参数", parameters.Name);
                    }
                }
                finally { window.Close(); }
            }
        });
        Assert.Single(ArvrEvidenceImages.Select([new() { Model = "White51_Test_JX2.0" }, new() { Model = "OpticCenter_Fast_JX2.0" }], true, EvidenceCategory.RgbCross));
        Assert.Single(ArvrEvidenceImages.Select([new() { Model = "Distortion_Test_JX2.0" }, new() { Model = "White51_Test_JX2.0" }], true, EvidenceCategory.Distortion));
        Assert.False(ArvrEvidenceImages.Matches(new() { Model = "NotDistortion" }, EvidenceCategory.Distortion));
    }

    [Fact]
    public void DefaultWindowIncludesServerAaWithExplicitReferenceParameters()
    {
        ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
        {
            AlgorithmEvidenceWindow window = new([]);
            try
            {
                Assert.Equal(3, ((ItemsControl)window.FindName("AlgorithmsList")).Items.Count);
                AaEvidenceParameters parameters = Assert.IsType<AaEvidenceParameters>(((ComboBox)window.FindName("ServerAaParameters")).SelectedItem);
                Assert.Contains("非历史参数", parameters.Name);
                using var json = System.Text.Json.JsonDocument.Parse(parameters.Json);
                Assert.Equal(System.Text.Json.JsonValueKind.Object, json.RootElement.ValueKind);
            }
            finally { window.Close(); }
        });
    }

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

    [Theory]
    [InlineData("error")]
    [InlineData("adapter-canceled")]
    [InlineData("adapter-task-canceled")]
    public void ComparisonUsesSameInputAndOneAlgorithmFailureDoesNotStopOthers(string failureKind)
    {
        using CancellationTokenSource adapterCancellation = new();
        adapterCancellation.Cancel();
        Exception failure = failureKind switch
        {
            "adapter-canceled" => new OperationCanceledException("adapter canceled", adapterCancellation.Token),
            "adapter-task-canceled" => new TaskCanceledException("adapter task canceled", null, adapterCancellation.Token),
            _ => new InvalidOperationException("failure")
        };
        IntPtr pixels = Marshal.AllocHGlobal(1);
        try
        {
            Marshal.WriteByte(pixels, 123);
            HImage image = new() { rows = 1, cols = 1, channels = 1, depth = 8, stride = 1, pData = pixels, isDispose = true };
            List<string> calls = [];
            IReadOnlyList<ImageEvidenceObservation> results = ImageEvidenceRunner.Run(image,
            [
                new("test.broken", "旧算法", input => { calls.Add("old"); Assert.Equal(pixels, input.pData); throw failure; }),
                new("test.working", "新算法", input => { calls.Add("new"); Assert.Equal(123, Marshal.ReadByte(input.pData)); return Success("test.working"); })
            ]);
            Assert.Equal(["old", "new"], calls);
            Assert.Equal(AlgorithmResultStatus.Failed, results[0].Result.Status);
            Assert.Equal("ExecutionError", Assert.Single(results[0].Result.Failures).Code);
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
    public void CallerCancellationThrownByAlgorithmDisposesEarlierArtifactsAndStopsComparison()
    {
        using CancellationTokenSource cancellation = new();
        AlgorithmResult first = Success("test.first");
        Assert.Throws<OperationCanceledException>(() => ImageEvidenceRunner.Run(new HImage(),
        [
            new("test.first", "first", _ => first),
            new("test.canceling", "canceling", _ =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }),
            new("test.third", "third", _ => throw new Xunit.Sdk.XunitException("Unexpected third call"))
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
                    Assert.Equal(0, calls);
                    Assert.Contains("ID：2", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Empty(((TabControl)window.FindName("ResultsTabs")).Items);
                    Assert.Contains("尚未计算", ((TextBlock)window.FindName("StatusText")).Text);
                    Complete(window.RunCurrentAsync());
                    Assert.Equal(1, calls);
                    Assert.Single(((TabControl)window.FindName("ResultsTabs")).Items);
                    Complete(window.NavigateAsync(1));
                    Assert.Equal(1, calls);
                    Assert.Contains("ID：4", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Empty(((TabControl)window.FindName("ResultsTabs")).Items);
                    Complete(window.RunCurrentAsync());
                    Assert.Equal(2, calls);
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

    // Independently selected files must not inherit a historical record's identity or upload its missing primary path.
    [Fact]
    public void SelectedFilesKeepTheirOwnProvenanceAndCanReturnToHistoricalSources()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ColorVision-Evidence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            ArvrDrawingOverlayCompatibilityTests.RunOnStaThread(() =>
            {
                string first = Path.Combine(directory, "first.png"), second = Path.Combine(directory, "second.png");
                foreach (string path in new[] { first, second })
                {
                    PngBitmapEncoder encoder = new();
                    encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray16, null, new ushort[] { 1000, 2000, 3000, 4000 }, 4)));
                    using FileStream stream = File.Create(path);
                    encoder.Save(stream);
                }
                ProjectARVRReuslt historical = new() { Id = 12, Model = "W51", FileName = Path.Combine(directory, "removed.cvraw"), SavedSourceImageFileName = first };
                AlgorithmEvidenceWindow window = new([historical], algorithms: [new("test.selected", "Test", _ => Success("test.selected"))]);
                try
                {
                    Complete(window.SelectImagesAsync([second, second.ToUpperInvariant(), Path.Combine(directory, "missing.png"), first]));
                    Assert.Equal(2, ((ComboBox)window.FindName("SourceFilter")).SelectedIndex);
                    Assert.Equal(second, window.CurrentSourcePath);
                    Assert.True(((Button)window.FindName("UploadCurrentImageButton")).IsEnabled);
                    Assert.DoesNotContain("ID：12", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Empty(((TabControl)window.FindName("ResultsTabs")).Items);
                    using (var unloadedReport = System.Text.Json.JsonDocument.Parse(window.SerializeReport()))
                        Assert.Empty(unloadedReport.RootElement.GetProperty("Samples").EnumerateArray());
                    Complete(window.RunCurrentAsync());
                    ((ComboBox)window.FindName("AlgorithmCategory")).SelectedIndex = 1;
                    Complete(window.ResetAsync());
                    Assert.Equal(2, ((ComboBox)window.FindName("SourceFilter")).SelectedIndex);
                    Assert.Equal(second, window.CurrentSourcePath);
                    Assert.Empty(((TabControl)window.FindName("ResultsTabs")).Items);
                    Complete(window.NavigateAsync(1));
                    Assert.Equal(first, window.CurrentSourcePath);
                    Assert.Contains("缺图 1 条", ((TextBlock)window.FindName("StatusText")).Text);
                    Assert.Empty(((TabControl)window.FindName("ResultsTabs")).Items);
                    Complete(window.RunCurrentAsync());
                    using (var report = System.Text.Json.JsonDocument.Parse(window.SerializeReport()))
                    {
                        Assert.All(report.RootElement.GetProperty("Samples").EnumerateArray(), sample =>
                        {
                            Assert.Equal("SelectedFile", sample.GetProperty("SourceKind").GetString());
                            Assert.Equal(System.Text.Json.JsonValueKind.Null, sample.GetProperty("RecordId").ValueKind);
                        });
                        Assert.Equal(Path.Combine(directory, "missing.png"), report.RootElement.GetProperty("SkippedFiles")[0].GetProperty("ImagePath").GetString());
                    }
                    ((ComboBox)window.FindName("AlgorithmCategory")).SelectedIndex = 0;
                    ((ComboBox)window.FindName("SourceFilter")).SelectedIndex = 0;
                    Complete(window.ResetAsync());
                    Assert.Equal(first, window.CurrentSourcePath);
                    Assert.Contains("ID：12", ((TextBox)window.FindName("SourceDetails")).Text);
                    Assert.Equal(Path.Combine(directory, "removed.cvraw"), historical.FileName);
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

    private static void Complete(Task task)
    {
        DispatcherFrame frame = new();
        _ = task.ContinueWith(_ => Dispatcher.CurrentDispatcher.BeginInvoke(() => frame.Continue = false),
            CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.FromCurrentSynchronizationContext());
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
}
