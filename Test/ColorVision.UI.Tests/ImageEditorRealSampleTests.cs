using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.UI;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class ImageEditorRealSampleTests(ITestOutputHelper output)
{
    internal const string SamplesEnvironmentVariable = "COLORVISION_IMAGE_EDITOR_SAMPLE_FILES";
    internal const string OutputEnvironmentVariable = "COLORVISION_IMAGE_EDITOR_SAMPLE_OUTPUT";

    [ImageEditorSamplesFact]
    [Trait("Category", "LocalImageSamples")]
    public async Task CvFilesLoadSelectLayersLeaseAndExportWithoutChangingTheSamples()
    {
        string[] samples = Environment.GetEnvironmentVariable(SamplesEnvironmentVariable)!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        Assert.NotEmpty(samples);
        string outputDirectory = Path.Combine(
            Environment.GetEnvironmentVariable(OutputEnvironmentVariable) ?? Path.GetTempPath(),
            $"image-editor-samples-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        output.WriteLine($"Sample export evidence: {outputDirectory}");
        List<object> reports = [];

        using SampleView fixture = new();
        for (int index = 0; index < samples.Length; index++)
        {
            string sample = samples[index];
            Assert.True(File.Exists(sample), $"Sample does not exist: {sample}");
            Assert.Contains(Path.GetExtension(sample).ToLowerInvariant(), new[] { ".cvraw", ".cvcie" });
            string fileHash = HashFile(sample);
            int headerOffset = CVFileUtil.ReadCIEFileHeader(sample, out CVCIEFile header);
            using (header)
            {
                Assert.True(headerOffset > 0, $"Invalid sample header: {sample}");
                await fixture.OpenAsync(sample);
                Capture initial = WpfTestHost.Invoke(() => fixture.CaptureCurrent());
                Capture? selected = null;
                try
                {
                    Assert.Equal(initial.Pixels.Sha256, HashLease(initial.Lease, initial.Pixels.Stride));
                    ImageLayerDescriptor? layer = WpfTestHost.Invoke(() => fixture.GetAnotherLayer());
                    if (layer != null)
                    {
                        await fixture.SelectLayerAsync(layer);
                        selected = WpfTestHost.Invoke(() => fixture.CaptureCurrent());
                        Assert.Equal(layer.Id, selected.LayerId);
                        // RGB channel selection is display-only; derived CIE layers replace the
                        // document source because they represent a different underlying plane.
                        if (layer.SourceChannelIndex.HasValue)
                            Assert.Equal(initial.Revision, selected.Revision);
                        else
                            Assert.True(selected.Revision > initial.Revision);
                        Assert.Equal(initial.Pixels.Sha256, HashLease(initial.Lease, initial.Pixels.Stride));
                        Assert.Equal(selected.Pixels.Sha256, HashLease(selected.Lease, selected.Pixels.Stride));
                        if (layer.Id is "cie-x" or "cie-y" or "cie-z")
                        {
                            int channelIndex = header.Channels == 1 ? 0 : layer.Id switch { "cie-x" => 0, "cie-y" => 1, _ => 2 };
                            Assert.True(CVFileUtil.ReadCIEFileChannel(sample, channelIndex, out CVCIEFile plane));
                            using (plane)
                            {
                                plane.Channels = 1;
                                plane.FileExtType = CVType.Raw;
                                PixelState directlyReadPlane = WpfTestHost.Invoke(() => DescribePixels(
                                    plane.Bpp is 32 or 64 ? MediaHelper.RenderFloatChannel(plane) : plane.ToWriteableBitmap(showErrors: false)!));
                                Assert.Equal(directlyReadPlane, selected.Pixels);
                            }
                        }
                        if (layer.SourceChannelIndex is int channel)
                        {
                            WpfTestHost.Invoke(() =>
                            {
                                Assert.Equal(channel, fixture.View.GetSelectedLayerSourceChannelIndex());
                                BitmapSource display = Assert.IsAssignableFrom<BitmapSource>(fixture.View.Presentation.DisplaySource);
                                Assert.NotSame(fixture.View.Document.Source, display);
                                Assert.Equal(initial.Pixels.Width, display.PixelWidth);
                                Assert.Equal(initial.Pixels.Height, display.PixelHeight);
                            });
                            Assert.Equal(initial.Pixels.Sha256, selected.Pixels.Sha256);
                        }
                    }

                    string initialExport = Path.Combine(outputDirectory, $"sample-{index + 1:D2}-initial.tiff");
                    await ExportAndCompareAsync(initial, initialExport);
                    string? selectedExport = null;
                    if (selected != null)
                    {
                        selectedExport = Path.Combine(outputDirectory, $"sample-{index + 1:D2}-selected.tiff");
                        await ExportAndCompareAsync(selected, selectedExport);
                    }

                    Assert.Equal(fileHash, HashFile(sample));
                    reports.Add(new
                    {
                        Sample = Path.GetFileName(sample), FileSha256 = fileHash,
                        Header = new { header.Cols, header.Rows, header.Channels, header.Bpp },
                        Initial = new { initial.LayerId, initial.Revision, initial.Pixels, Export = initialExport },
                        Selected = selected == null ? null : new { selected.LayerId, selected.Revision, selected.Pixels, Export = selectedExport },
                    });
                    output.WriteLine($"{Path.GetFileName(sample)}: {initial.Pixels.Width}x{initial.Pixels.Height} {initial.Pixels.Format}; {initial.LayerId} -> {selected?.LayerId ?? "single layer"}; source TIFF pixels and sample SHA256 verified.");
                }
                finally
                {
                    initial.Dispose();
                    selected?.Dispose();
                }
            }
        }

        File.WriteAllText(Path.Combine(outputDirectory, "sample-results.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
    }

    [ImageEditorSamplesFact]
    [Trait("Category", "LocalImageSamples")]
    public async Task CvRawChannelSwitchesKeepTheLoadedSourceAndReportTimings()
    {
        string[] samples = Environment.GetEnvironmentVariable(SamplesEnvironmentVariable)!
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Path.GetFullPath)
            .Where(path => string.Equals(Path.GetExtension(path), ".cvraw", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.NotEmpty(samples);

        foreach (string sample in samples)
        {
            Assert.True(File.Exists(sample), $"Sample does not exist: {sample}");
            using SampleView fixture = new();
            ColorVision.ImageEditor.EditorTools.PseudoColor.PseudoColorDefaultConfig.Current.IsAutoSetRangeByDefault = true;
            double processBaselineMiB = CollectPrivateMemoryMiB();
            Stopwatch stopwatch = Stopwatch.StartNew();
            await fixture.OpenAsync(sample);
            stopwatch.Stop();
            double openMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
            double openedPrivateMemoryMiB = CollectPrivateMemoryMiB();
            Assert.False(WpfTestHost.Invoke(() => HasNativeFrameCache(fixture.View)));

            (ImageSource Source, long Revision, ImageLayerDescriptor[] Layers) baseline = WpfTestHost.Invoke(() =>
                (fixture.View.Document.Source!, fixture.View.Document.Revision,
                    fixture.View.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>().ToArray()));
            ImageLayerDescriptor[] rgbLayers = baseline.Layers.Where(layer => layer.SourceChannelIndex.HasValue).ToArray();
            Assert.Equal(new[] { "red", "green", "blue" }, rgbLayers.Select(layer => layer.Id).ToArray());
            ImageLayerDescriptor composite = Assert.Single(baseline.Layers, layer => layer.Id == "composite");
            PixelFormat expectedChannelFormat = WpfTestHost.Invoke(() =>
            {
                BitmapSource sourceBitmap = Assert.IsAssignableFrom<BitmapSource>(baseline.Source);
                return sourceBitmap.Format.BitsPerPixel / 3 == 16 ? PixelFormats.Gray16 : PixelFormats.Gray8;
            });

            int reloadCount = 0;
            EventHandler<ImageViewImageSourceLoadedEventArgs> loaded = (_, _) => Interlocked.Increment(ref reloadCount);
            WpfTestHost.Invoke(() => fixture.View.ImageSourceLoaded += loaded);
            List<object> channelTimings = [];
            try
            {
                foreach (ImageLayerDescriptor layer in rgbLayers)
                {
                    stopwatch.Restart();
                    await fixture.SelectLayerAsync(layer);
                    stopwatch.Stop();
                    double elapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                    WpfTestHost.Invoke(() =>
                    {
                        Assert.Equal(baseline.Revision, fixture.View.Document.Revision);
                        Assert.Same(baseline.Source, fixture.View.Document.Source);
                        Assert.Equal(layer.Id, fixture.View.SelectedLayer?.Id);
                        Assert.NotSame(baseline.Source, fixture.View.Presentation.DisplaySource);
                        BitmapSource channel = Assert.IsAssignableFrom<BitmapSource>(fixture.View.Presentation.DisplaySource);
                        Assert.Equal(expectedChannelFormat, channel.Format);
                    });
                    channelTimings.Add(new { layer.Id, Milliseconds = Math.Round(elapsedMilliseconds, 3) });
                }
                Assert.True(WpfTestHost.Invoke(() => HasNativeFrameCache(fixture.View)));
                double switchedPrivateMemoryMiB = CollectPrivateMemoryMiB();

                stopwatch.Restart();
                await fixture.View.Dispatcher.InvokeAsync(() => fixture.View.ComboBoxLayers.SelectedItem = composite);
                stopwatch.Stop();
                WpfTestHost.Invoke(() =>
                {
                    Assert.Equal(baseline.Revision, fixture.View.Document.Revision);
                    Assert.Same(baseline.Source, fixture.View.Document.Source);
                    Assert.Same(baseline.Source, fixture.View.Presentation.DisplaySource);
                    Assert.Null(fixture.View.FunctionImage);
                });

                Assert.Equal(0, Volatile.Read(ref reloadCount));
                long sourceRevision = baseline.Revision;
                double compositeMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                await fixture.View.Dispatcher.InvokeAsync(fixture.View.Clear);
                WpfTestHost.Invoke(() =>
                {
                    Assert.Null(fixture.View.Document.Source);
                    Assert.Null(fixture.View.Presentation.DisplaySource);
                    Assert.Null(fixture.View.FunctionImage);
                    Assert.False(HasNativeFrameCache(fixture.View));
                });
                baseline = default;
                rgbLayers = null!;
                composite = null!;
                double clearedPrivateMemoryMiB = CollectPrivateMemoryMiB();
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    Sample = sample,
                    FileMiB = Math.Round(new FileInfo(sample).Length / 1024d / 1024d, 3),
                    OpenMilliseconds = Math.Round(openMilliseconds, 3),
                    Channels = channelTimings,
                    CompositeMilliseconds = Math.Round(compositeMilliseconds, 3),
                    SourceRevision = sourceRevision,
                    ReloadCount = reloadCount,
                    PrivateMemoryMiB = new
                    {
                        Baseline = processBaselineMiB,
                        Opened = openedPrivateMemoryMiB,
                        OpenIncrement = Math.Round(openedPrivateMemoryMiB - processBaselineMiB, 3),
                        Switched = switchedPrivateMemoryMiB,
                        SwitchIncrement = Math.Round(switchedPrivateMemoryMiB - openedPrivateMemoryMiB, 3),
                        Cleared = clearedPrivateMemoryMiB,
                        RetainedAfterClear = Math.Round(clearedPrivateMemoryMiB - processBaselineMiB, 3),
                    },
                }));
            }
            finally
            {
                WpfTestHost.Invoke(() => fixture.View.ImageSourceLoaded -= loaded);
            }
        }
    }

    private static bool HasNativeFrameCache(ImageView view)
    {
        FieldInfo field = view.Document.GetType().GetField("_cachedSource", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return field.GetValue(view.Document) != null;
    }

    private static double CollectPrivateMemoryMiB()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        return Math.Round(process.PrivateMemorySize64 / 1024d / 1024d, 3);
    }

    private static async Task ExportAndCompareAsync(Capture capture, string outputPath)
    {
        await ImageView.SaveSnapshotExportsAsync(capture.Snapshot, new ImageViewSnapshotExportOptions
        {
            SourceFileName = outputPath,
            SourceOptions = new ImageViewSourceSaveOptions { Format = ImageViewSourceFormat.Tiff },
        });
        using FileStream stream = File.OpenRead(outputPath);
        BitmapSource decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        Assert.Equal(capture.Pixels, DescribePixels(decoded));
    }

    private static PixelState DescribePixels(BitmapSource source)
    {
        int stride = checked((source.PixelWidth * source.Format.BitsPerPixel + 7) / 8);
        byte[] pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);
        return new(source.PixelWidth, source.PixelHeight, source.Format.ToString(), stride, Convert.ToHexString(SHA256.HashData(pixels)));
    }

    private static string HashLease(ImageFrameLease lease, int packedStride)
    {
        HImage image = lease.Image;
        Assert.True(image.stride >= packedStride);
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] row = new byte[packedStride];
        for (int y = 0; y < image.rows; y++)
        {
            Marshal.Copy(IntPtr.Add(image.pData, checked(y * image.stride)), row, 0, row.Length);
            hash.AppendData(row);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record PixelState(int Width, int Height, string Format, int Stride, string Sha256);

    private sealed record Capture(ImageViewSnapshot Snapshot, ImageFrameLease Lease, PixelState Pixels, long Revision, string? LayerId) : IDisposable
    {
        public void Dispose()
        {
            Snapshot.Dispose();
            Lease.Dispose();
        }
    }

    private sealed class SampleView : IDisposable
    {
        private readonly IConfigService? _previousConfigService = ConfigService.Instance;
        internal ImageView View { get; }

        internal SampleView()
        {
            ConfigService.SetInstance(new ConfigHandler());
            try
            {
                View = WpfTestHost.Invoke(() =>
                {
                    EnsureResources();
                    CvcieDisplayConfig.Current.EnableTrueColor = false;
                    ImageView view = new();
                    view.IEditorToolFactory.IEditorTools.Clear();
                    // Register the real opener explicitly so this probe does not depend on assembly scan order.
                    CVRawOpen opener = new(view.EditorContext);
                    view.IEditorToolFactory.IImageOpens[".cvraw"] = opener;
                    view.IEditorToolFactory.IImageOpens[".cvcie"] = opener;
                    return view;
                });
            }
            catch
            {
                ConfigService.SetInstance(_previousConfigService!);
                throw;
            }
        }

        internal Task OpenAsync(string path) => WaitForImageAsync(() => View.OpenImage(path), expectChannel: false);
        internal Task SelectLayerAsync(ImageLayerDescriptor layer)
            => WaitForImageAsync(() => View.ComboBoxLayers.SelectedItem = layer, layer.Kind == ImageLayerKind.Channel);

        internal ImageLayerDescriptor? GetAnotherLayer()
        {
            ImageLayerDescriptor[] layers = View.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>().ToArray();
            Assert.NotEmpty(layers);
            return layers.FirstOrDefault(layer => layer.Id == "cie-x" && layer.Id != View.SelectedLayer?.Id)
                ?? layers.FirstOrDefault(layer => layer.SourceChannelIndex.HasValue && layer.Id != View.SelectedLayer?.Id)
                ?? layers.FirstOrDefault(layer => layer.Id != View.SelectedLayer?.Id);
        }

        internal Capture CaptureCurrent()
        {
            Assert.IsType<CVRawOpen>(View.EditorContext.IImageOpen);
            BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(View.Document.Source);
            Assert.Equal(source.PixelWidth, View.Config.GetProperties<int>(ImageViewPropertyKeys.ImageWidth));
            Assert.Equal(source.PixelHeight, View.Config.GetProperties<int>(ImageViewPropertyKeys.ImageHeight));
            ImageFrameLease lease = Assert.IsType<ImageFrameLease>(View.AcquireImageFrame());
            try
            {
                ImageViewSnapshot snapshot = Assert.IsType<ImageViewSnapshot>(View.CaptureSnapshotForBackgroundSave(includeOverlays: false));
                return new(snapshot, lease, DescribePixels(source), View.Document.Revision, View.SelectedLayer?.Id);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        private async Task WaitForImageAsync(Action action, bool expectChannel)
        {
            TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            bool loaded = false;
            ImageSource? previousDisplay = null;
            List<string> events = [];
            void CheckCompletion()
            {
                bool completed = expectChannel
                    ? !ReferenceEquals(previousDisplay, View.Presentation.DisplaySource)
                        && View.FunctionImage is BitmapSource
                        && ReferenceEquals(View.FunctionImage, View.Presentation.DisplaySource)
                    : loaded;
                if (completed) completion.TrySetResult();
            }
            string DescribeState() => $"layer={View.SelectedLayer?.Id}, revision={View.Document.Revision}, display={View.Presentation.DisplaySource?.GetType().Name ?? "null"}, function={View.FunctionImage?.GetType().Name ?? "null"}";
            void OnLoaded(object? sender, ImageViewImageSourceLoadedEventArgs args)
            {
                loaded = true;
                events.Add($"loaded: {DescribeState()}");
                CheckCompletion();
            }
            void OnDisplayChanged(object? sender, EventArgs args)
            {
                events.Add($"display: {DescribeState()}");
                CheckCompletion();
            }
            DependencyPropertyDescriptor sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(DrawCanvas));
            WpfTestHost.Invoke(() =>
            {
                previousDisplay = View.Presentation.DisplaySource;
                View.ImageSourceLoaded += OnLoaded;
                sourceProperty.AddValueChanged(View.ImageShow, OnDisplayChanged);
            });
            try
            {
                await View.Dispatcher.InvokeAsync(action).Task.WaitAsync(TimeSpan.FromSeconds(60));
                await completion.Task.WaitAsync(TimeSpan.FromSeconds(60));
            }
            catch (TimeoutException ex)
            {
                string details = WpfTestHost.Invoke(() => $"{DescribeState()}; events=[{string.Join("; ", events)}]");
                throw new TimeoutException($"Image sample selection did not complete: {details}", ex);
            }
            finally
            {
                WpfTestHost.Invoke(() =>
                {
                    View.ImageSourceLoaded -= OnLoaded;
                    sourceProperty.RemoveValueChanged(View.ImageShow, OnDisplayChanged);
                });
            }
        }

        public void Dispose()
        {
            try { WpfTestHost.Invoke(View.Dispose); }
            finally { ConfigService.SetInstance(_previousConfigService!); }
        }

        private static void EnsureResources()
        {
            Application application = Application.Current!;
            application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
            application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            application.Resources["ToolBarImage"] = new Style(typeof(Image));
            application.Resources["BaseStyle"] = new Style(typeof(Control));
            application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        }
    }
}

public sealed class ImageEditorSamplesFactAttribute : FactAttribute
{
    public ImageEditorSamplesFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ImageEditorRealSampleTests.SamplesEnvironmentVariable)))
            Skip = $"Set {ImageEditorRealSampleTests.SamplesEnvironmentVariable} to explicitly selected local CVRAW/CVCIE samples.";
    }
}
