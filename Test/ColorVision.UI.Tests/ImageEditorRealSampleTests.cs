using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Layers;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;
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
                        // CVRawOpen reloads the selected baseline before rendering RGB channels.
                        // The ordinary bitmap controller has a separate display-only contract.
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
