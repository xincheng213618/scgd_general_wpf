using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Tif;
using ColorVision.Algorithms;
using System.ComponentModel;
using System.IO;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

/// <summary>Protects the format-neutral sample binding and content invalidation boundary.</summary>
public sealed class EmbeddedCalibratedImageTests
{
    [Theory]
    [InlineData(8, 1, "png")]
    [InlineData(16, 1, "png")]
    [InlineData(8, 3, "png")]
    [InlineData(16, 3, "png")]
    [InlineData(8, 1, "tif")]
    [InlineData(16, 1, "tif")]
    [InlineData(8, 3, "tif")]
    [InlineData(16, 3, "tif")]
    public async Task FileRoundTripKeepsDisplaySamplesAndOriginalMeasurementProfiles(int bits, int channels, string extension)
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        string path = Path.Combine(Path.GetTempPath(), $"cv-embedded-profile-{Guid.NewGuid():N}.{extension}");
        try
        {
            BitmapSource pixels = CreatePixels(bits, channels);
            if (bits == 16)
            {
                ushort[] samples = channels == 1 ? [100, 400, 700, 1000]
                    : [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200];
                pixels = BitmapSource.Create(2, 2, 96, 96, pixels.Format, null, samples, 2 * channels * sizeof(ushort));
                pixels.Freeze();
            }
            var snapshot = new ColorCalibrationSnapshot
            {
                Width = 2, Height = 2, RawBpp = bits, Channels = channels, TransformKind = channels == 1 ? 2 : 0,
                Coefficients = [2, 0, 0, 0, 3, 0, 0, 0, 4], Exposure = [1, 1, 1]
            };
            string expectedHash = ColorVisionPixelBinding.ComputeSha256(pixels);
            CalibratedImageExport.Save(pixels, Parameters(pixels, snapshot, channels), path);
            await WpfTestHost.Invoke(() => { view.OpenImage(path); return view.ImageOpenCompletion; });
            var sources = WpfTestHost.Invoke(() => Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!));
            using IImageProfileMeasurementSource profile = await Task.Run(() => sources.Open(default));
            double Sample(int pixel, int channel) => bits == 16 ? 100 + pixel * 300 + channel * 100 : (pixel + 1) * 10 + channel * 2;
            Assert.Equal(channels == 1 ? new[] { "Gray", "CIE Y" } : new[] { "B", "G", "R", "CIE X", "CIE Y", "CIE Z", "CIE x", "CIE y" }, profile.ChannelNames);
            for (int pixel = 0; pixel < 4; pixel++)
            {
                int x = pixel % 2, y = pixel / 2;
                for (int c = 0; c < channels; c++)
                    Assert.Equal(Sample(pixel, channels == 1 ? 0 : 2 - c), profile.Read(x, y, c));
                var poi = WpfTestHost.Invoke(() => Assert.IsAssignableFrom<CalibratedImageCapability>(view.ImageContentCapability)
                    .CalculatePoi([new(x, y, 1, 1, PoiMeasurementShape.Point)], true).Results[0]);
                Assert.Equal(Sample(pixel, channels == 1 ? 0 : 1) * (channels == 1 ? 2 : 3), poi.Y);
                Assert.Equal(poi.Y, profile.Read(x, y, channels == 1 ? 1 : 4));
                if (channels == 3)
                {
                    Assert.Equal(Sample(pixel, 0) * 2, poi.X);
                    Assert.Equal(Sample(pixel, 2) * 4, poi.Z);
                    Assert.Equal(poi.X, profile.Read(x, y, 3));
                    Assert.Equal(poi.Z, profile.Read(x, y, 5));
                }
            }
            await WpfTestHost.Invoke(async () =>
            {
                var layers = ((IReadOnlyList<ImageLayerDescriptor>)view.ComboBoxLayers.ItemsSource).ToArray();
                await SelectFileLayer(view, layers.Single(l => l.Id == "cie-y"));
                // The profile must read original XYZ while the displayed Y proxy is normalized.
                Assert.Same(sources, Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!));
                var profileParameters = new ImageProfileParameters { SampleSpacingPixels = 0.5 };
                var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, profileParameters, new PolylineAlgorithmRoi([new(0, 0), new(1, 0)]));
                using var result = new ImageProfileAlgorithmProvider().ExecuteMeasurement(invocation, profileParameters, profile,
                    new ImageSelectionScope(Guid.NewGuid(), 1, profile.Width, profile.Height, 96, 96), default);
                Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
                var row = result.GetArtifact<AlgorithmTableArtifact>()!.Rows[1];
                double expectedY = (Sample(0, channels == 1 ? 0 : 1) + Sample(1, channels == 1 ? 0 : 1)) / 2 * (channels == 1 ? 2 : 3);
                Assert.Equal(expectedY, row["CIE Y"].GetDouble());
                if (channels == 3)
                {
                    double x = row["CIE X"].GetDouble(), y = row["CIE Y"].GetDouble(), z = row["CIE Z"].GetDouble();
                    Assert.Equal(x / (x + y + z), row["CIE x"].GetDouble());
                    Assert.Equal(y / (x + y + z), row["CIE y"].GetDouble());
                }
                await SelectFileLayer(view, layers.Single(l => l.Id == "composite"));
                Assert.Equal(expectedHash, ColorVisionPixelBinding.ComputeSha256((BitmapSource)view.Presentation.DisplaySource!));
                if (channels == 3)
                    foreach (string name in new[] { "red", "green", "blue" })
                    {
                        await SelectFileLayer(view, layers.Single(l => l.Id == "cie-y"));
                        await SelectFileLayer(view, layers.Single(l => l.Id == name));
                        BitmapSource display = Assert.IsAssignableFrom<BitmapSource>(view.Presentation.DisplaySource);
                        int stride = 2 * (bits / 8);
                        byte[] samples = new byte[stride * 2];
                        display.CopyPixels(samples, stride, 0);
                        int channel = name == "red" ? 0 : name == "green" ? 1 : 2;
                        for (int pixel = 0; pixel < 4; pixel++)
                            Assert.Equal(Sample(pixel, channel), bits == 8 ? samples[pixel] : BitConverter.ToUInt16(samples, pixel * 2));
                        Assert.Equal(expectedHash, ColorVisionPixelBinding.ComputeSha256((BitmapSource)view.ViewBitmapSource));
                        view.ComboBoxLayers.SelectedItem = layers.Single(l => l.Id == "composite");
                        Assert.Equal(expectedHash, ColorVisionPixelBinding.ComputeSha256((BitmapSource)view.Presentation.DisplaySource!));
                    }
                Assert.Same(sources, Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!));
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); File.Delete(path); }
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("switch")]
    [InlineData("clear")]
    [InlineData("close")]
    public async Task EmbeddedProfilesRetireWithTheirContentIncludingCachedSamples(string mutation)
    {
        ImageView view = await OpenMonochromeFixture();
        try
        {
            ImageProfileSourceOption option = WpfTestHost.Invoke(() => Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!));
            using IImageProfileMeasurementSource profile = option.Open(default);
            Assert.Equal(20, profile.Read(0, 0, 1)); // Populate the per-pixel XYZ cache.
            using CancellationTokenSource cancellation = new();
            using (IImageProfileMeasurementSource canceled = option.Open(cancellation.Token))
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => canceled.Read(0, 0, 1));
            }
            using (IImageProfileMeasurementSource disposed = option.Open(default))
            {
                disposed.Dispose();
                Assert.Throws<ObjectDisposedException>(() => disposed.Read(0, 0, 1));
            }
            Assert.Equal(20, profile.Read(0, 0, 1)); // Disposing a reader must not dispose its owner.
            await WpfTestHost.Invoke(async () =>
            {
                switch (mutation)
                {
                    case "edit": view.CommitSourcePixels(new WriteableBitmap((BitmapSource)view.ViewBitmapSource)); break;
                    case "clear": view.Clear(); break;
                    case "close": view.Dispose(); break;
                    case "switch":
                        BitmapSource pixels = CreatePixels(8, 1);
                        var snapshot = new ColorCalibrationSnapshot
                        {
                            Width = 2, Height = 2, RawBpp = 8, Channels = 1, TransformKind = 2,
                            Coefficients = [3, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1]
                        };
                        await Publish(view, new(pixels, Parameters(pixels, snapshot, 1), "replacement.png"));
                        using (var replacement = Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!).Open(default))
                            Assert.Equal(30, replacement.Read(0, 0, 1));
                        break;
                }
                if (mutation != "switch") Assert.Null(view.EditorContext.ProcessingContext.ProfileMeasurementSources);
            });
            Assert.Throws<ObjectDisposedException>(() => profile.Read(0, 0, 0));
            Assert.Throws<ObjectDisposedException>(() => profile.Read(0, 0, 1));
            Assert.Throws<ObjectDisposedException>(() => option.Open(default));
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Theory]
    [InlineData(8, 1)]
    [InlineData(16, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 3)]
    public async Task LosslessSamplesShareRawMeasurementAndEditingRevokesCapability(int bits, int channels)
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            BitmapSource pixels = CreatePixels(bits, channels);
            ColorCalibrationSnapshot snapshot = new()
            {
                Width = 2, Height = 2, RawBpp = bits, Channels = channels,
                TransformKind = channels == 1 ? 2 : 0, Coefficients = [2, 0, 0, 0, 3, 0, 0, 0, 4],
                Exposure = [10, 20, 30], InterleavedBgr = bits == 8
            };
            ColorVisionTiffParameters parameters = Parameters(pixels, snapshot, channels);
            DecodedImageContent content = new(pixels, parameters, "fixture.png");
            await WpfTestHost.Invoke(() => Publish(view, content));
            WpfTestHost.Invoke(() =>
            {
                CalibratedImageCapability capability = Assert.IsAssignableFrom<CalibratedImageCapability>(view.ImageContentCapability);
                var result = capability.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point), new(0, 0, 2, 2, PoiMeasurementShape.Rect)], true);
                Assert.Equal(channels, result.Channels);
                Assert.Equal(channels == 1 ? 20 : 36, result.Results[0].Y);
                if (channels == 3) { Assert.Equal(20, result.Results[0].X); Assert.Equal(56, result.Results[0].Z); }
                Assert.Equal(channels == 1 ? 50 : 81, result.Results[1].Y);
                Assert.True(view.Config.GetProperties<bool>("HasCieMeasurements"));
                view.SetDisplayLayerSource(new WriteableBitmap(pixels));
                Assert.Same(capability, view.ImageContentCapability);
                Assert.Equal(result.Results[0], capability.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point)], true).Results[0]);
                view.SetImageSource(new WriteableBitmap(pixels), false, true);
                Assert.Null(view.ImageContentCapability);
                Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.NotNull(view.Config.GetProperties<ColorCalibrationSnapshot>(RawCalibrationInformationProvider.MetadataKey));
                Assert.Throws<InvalidOperationException>(() => capability.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point)], true));
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Theory]
    [InlineData("mismatched-pixels")]
    [InlineData("mismatched-depth")]
    [InlineData("unknown-schema")]
    [InlineData("display")]
    [InlineData("record-only")]
    public async Task UnboundParametersRemainReadableWithoutMeasurementTools(string reason)
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            BitmapSource pixels = CreatePixels(8, 3);
            ColorCalibrationSnapshot snapshot = new()
            {
                Width = 2, Height = 2, RawBpp = 8, Channels = 3, TransformKind = 0,
                Coefficients = [1, 0, 0, 0, 1, 0, 0, 0, 1], Exposure = [1, 1, 1], CanReplay = reason != "record-only"
            };
            ColorVisionTiffParameters parameters = Parameters(pixels, snapshot, 3);
            if (reason == "mismatched-pixels") parameters.PixelSha256 = new string('0', 64);
            if (reason == "mismatched-depth") parameters.Bpp = 16;
            if (reason == "unknown-schema") parameters.Schema = "ColorVision.CVImage/999";
            if (reason == "display") parameters.PixelRole = "display";
            await WpfTestHost.Invoke(() => Publish(view, new(pixels, parameters, "fixture.tif")));
            WpfTestHost.Invoke(() =>
            {
                Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.Empty(view.ImageContentCapability!.GetEditorTools());
                Assert.NotNull(view.Config.GetProperties<ColorCalibrationSnapshot>(RawCalibrationInformationProvider.MetadataKey));
                Assert.NotNull(view.ViewBitmapSource);
                Assert.Null(view.EditorContext.ProcessingContext.ProfileMeasurementSources);
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task FloatYReopensActualNegativeSamplesWithoutApplyingRecordedMatrixAgain()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            BitmapSource pixels = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray32Float, null, new float[] { -2, 0, 4, 6 }, 8);
            pixels.Freeze();
            ColorVisionTiffParameters parameters = new()
            {
                Rows = 2, Cols = 2, Bpp = 32, PixelChannels = 1, PixelRole = "xyz-channel", MeasurementChannel = "Y",
                PixelLayout = pixels.Format.ToString(), PixelSha256 = ColorVisionPixelBinding.ComputeSha256(pixels)
            };
            await WpfTestHost.Invoke(() => Publish(view, new(pixels, parameters, "y.tif")));
            WpfTestHost.Invoke(() =>
            {
                CalibratedImageCapability capability = Assert.IsAssignableFrom<CalibratedImageCapability>(view.ImageContentCapability);
                Assert.Equal(-2, capability.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point)], true).Results[0].Y);
                Assert.Equal(2, capability.CalculatePoi([new(0, 0, 2, 2, PoiMeasurementShape.Rect)], true).Results[0].Y);
                var option = Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                using IImageProfileMeasurementSource profile = option.Open(default);
                Assert.Equal(new[] { "CIE Y" }, profile.ChannelNames);
                Assert.Equal(-2, profile.Read(0, 0, 0));
                Assert.Equal(6, profile.Read(1, 1, 0));
                view.Clear();
                Assert.Null(view.ImageContentCapability);
                Assert.Throws<ObjectDisposedException>(() => profile.Read(0, 0, 0));
                Assert.Throws<ObjectDisposedException>(() => option.Open(default));
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PixelEditReenteredFromLayerNotificationRevokesMeasurement(bool commitPixels)
    {
        ImageView view = await OpenMonochromeFixture();
        try
        {
            WpfTestHost.Invoke(() =>
            {
                var capability = Assert.IsAssignableFrom<CalibratedImageCapability>(view.ImageContentCapability);
                bool edited = false;
                view.EditorContext.ProcessingContext.ProfileMeasurementSources = [];
                view.ImageSourceLoaded += (_, _) =>
                {
                    if (edited) return;
                    edited = true;
                    var replacement = new WriteableBitmap(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray16, null,
                        new ushort[] { 99, 99, 99, 99 }, 4));
                    if (commitPixels) view.CommitSourcePixels(replacement);
                    else view.SetImageSource(replacement);
                };
                view.SetDisplayLayerSource(new WriteableBitmap((BitmapSource)view.ViewBitmapSource));
                Assert.True(edited);
                Assert.Null(view.ImageContentCapability);
                Assert.Null(view.ImageContent);
                Assert.False(view.Config.GetProperties<bool>("HasCieMeasurements"));
                Assert.Null(view.EditorContext.ProcessingContext.ProfileMeasurementSources);
                Assert.NotNull(view.Config.GetProperties<ColorCalibrationSnapshot>(RawCalibrationInformationProvider.MetadataKey));
                Assert.Throws<InvalidOperationException>(() => capability.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point)], true));
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task OldRetirementCannotEraseNewCapabilityOrAdvanceItsRevision()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        BlockingCapability old = new(block: true), replacement = new(block: false);
        Task? retirement = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray16, null));
                view.AttachImageContentCapability(old);
            });
            retirement = Task.Run(view.NotifySourcePixelsChanged);
            await old.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await WpfTestHost.Invoke(() => view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task);
            WpfTestHost.Invoke(() => Assert.Null(view.ComboBoxLayers.ItemsSource));
            long revision = WpfTestHost.Invoke(() =>
            {
                view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray16, null));
                view.AttachImageContentCapability(replacement);
                return view.ImageRevision;
            });
            old.Continue.TrySetResult();
            await retirement.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(replacement, view.ImageContentCapability);
                Assert.Equal(revision, view.ImageRevision);
                Assert.NotNull(view.ComboBoxLayers.ItemsSource);
                Assert.Equal(1, old.DisposeCount);
                Assert.Equal(0, replacement.DisposeCount);
            });
        }
        finally
        {
            old.Continue.TrySetResult();
            if (retirement != null) await retirement.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(view.Dispose);
        }
        Assert.Equal(1, replacement.DisposeCount);
    }

    [Fact]
    public async Task DetachedCleanupCannotRetireReactivatedCvRawOpener()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        try
        {
            var state = WpfTestHost.Invoke(() =>
            {
                view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray16, null));
                var opener = new CVRawOpen(view.EditorContext);
                opener.AttachLiveCvcie(view, 2, 2, 32, 1, new byte[16], [1]);
                Action release = opener.DetachForRetirement(view.ContentVersion);
                opener.AttachLiveCvcie(view, 2, 2, 32, 1,
                    new float[] { 7, 8, 9, 10 }.SelectMany(BitConverter.GetBytes).ToArray(), [1]);
                IEditorToggleTool probe = opener.GetEditorTools().OfType<IEditorToggleTool>().First();
                probe.IsChecked = true;
                return (Opener: opener, Release: release, Probe: probe);
            });
            await Task.Run(state.Release);
            // Drain the queued UI retirement; no timing or scheduler-count assumption is involved.
            await WpfTestHost.Invoke(() => view.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background).Task);
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(state.Opener, view.ImageContentCapability);
                Assert.True(view.Config.GetProperties<bool>("IsCVCIE"));
                Assert.True(state.Probe.IsChecked);
                Assert.Equal(7, state.Opener.CalculatePoi([new(0, 0, 1, 1, PoiMeasurementShape.Point)], true).Results[0].Y);
            });
        }
        finally { WpfTestHost.Invoke(view.Dispose); }
    }

    [Fact]
    public async Task OpenerBufferReuseWaitsForDetachedMeasurementReaders()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource proceed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task? reader = null;
        Task? retirement = null;
        try
        {
            var state = WpfTestHost.Invoke(() =>
            {
                view.SetImageSource(new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray16, null));
                using ColorVision.FileIO.CVCIEFile raw = new()
                {
                    Cols = 2, Rows = 2, Channels = 1, Bpp = 16, Data = new byte[8]
                };
                ColorCalibrationSnapshot snapshot = new()
                {
                    Width = 2, Height = 2, RawBpp = 16, Channels = 1, TransformKind = 2,
                    Coefficients = [2, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1]
                };
                var buffer = new PoiMeasurementBuffer(raw, snapshot);
                var opener = new CVRawOpen(view.EditorContext);
                opener.InitializeEmbedded(buffer, snapshot, "fixture.cvraw", null);
                view.AttachImageContentCapability(opener);
                var option = Assert.Single(view.EditorContext.ProcessingContext.ProfileMeasurementSources!);
                var profile = option.Open(default);
                Assert.Equal(0, profile.Read(0, 0, 1));
                return (Buffer: buffer, Opener: opener, Option: option, Profile: profile);
            });
            reader = Task.Run(() => state.Buffer.RawSource!.BorrowRaw(file =>
            {
                entered.TrySetResult();
                proceed.Task.GetAwaiter().GetResult();
                return file.Data.Length;
            }));
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Action release = WpfTestHost.Invoke(() => state.Opener.DetachForRetirement(view.ContentVersion));
            // Detachment revokes even cached profile reads before slow reader cleanup can start.
            await Task.Run(() =>
            {
                using (state.Profile)
                {
                    Assert.Throws<ObjectDisposedException>(() => state.Profile.Read(0, 0, 1));
                    Assert.Throws<ObjectDisposedException>(() => state.Option.Open(default));
                }
            }).WaitAsync(TimeSpan.FromSeconds(10));
            retirement = Task.Run(release);
            Task reuse = WpfTestHost.Invoke(() => state.Opener.ReleaseContentAsync(reuseBuffers: true));
            Assert.False(reuse.IsCompleted);
            proceed.TrySetResult();
            await reader.WaitAsync(TimeSpan.FromSeconds(10));
            await retirement.WaitAsync(TimeSpan.FromSeconds(10));
            await reuse.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Throws<ObjectDisposedException>(() => state.Buffer.RawSource!.BorrowRaw(file => file.Data.Length));
        }
        finally
        {
            proceed.TrySetResult();
            if (reader != null) await reader.WaitAsync(TimeSpan.FromSeconds(10));
            if (retirement != null) await retirement.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    [Theory]
    [InlineData(ImageViewSourceFormat.Png)]
    [InlineData(ImageViewSourceFormat.Tiff)]
    public async Task SharedArvrSnapshotExportPreservesSamplesWithoutReplayMetadataAfterViewCloses(ImageViewSourceFormat format)
    {
        ImageView view = await OpenMonochromeFixture();
        string directory = Path.Combine(Path.GetTempPath(), $"cv-image-snapshot-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, format == ImageViewSourceFormat.Png ? "source.png" : "source.tif");
        string rendered = Path.Combine(directory, "rendered.png");
        try
        {
            var snapshot = WpfTestHost.Invoke(() =>
            {
                var captured = view.CaptureSnapshotForBackgroundSave(false);
                view.Dispose();
                return Assert.IsType<ImageViewSnapshot>(captured);
            });
            await ImageView.SaveSnapshotExportsAsync(snapshot, new ImageViewSnapshotExportOptions
            {
                SourceFileName = source, SourceOptions = new() { Format = format },
                RenderedFileName = rendered, RenderedOptions = new() { Format = ImageViewSnapshotFormat.Png }
            });
            WpfTestHost.Invoke(() =>
            {
                foreach (string path in new[] { source, rendered })
                {
                    using var stream = File.OpenRead(path);
                    BitmapFrame frame = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                    if (path != source)
                    {
                        Assert.True(ColorVisionTiffParameters.TryRead(frame.Metadata as BitmapMetadata, out var record));
                        Assert.Equal("display", record!.PixelRole);
                        Assert.Null(record.Calibration);
                        Assert.Null(record.PixelSha256);
                        Assert.NotNull(record.CalibrationSummary);
                        continue;
                    }
                    Assert.False(ColorVisionTiffParameters.TryRead(frame.Metadata as BitmapMetadata, out _));
                    Assert.Equal(PixelFormats.Gray16, frame.Format);
                    ushort[] samples = new ushort[4];
                    frame.CopyPixels(samples, 4, 0);
                    Assert.Equal(new ushort[] { 10, 20, 30, 40 }, samples);
                }
            });
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
            foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static async Task SelectFileLayer(ImageView view, ImageLayerDescriptor layer)
    {
        TaskCompletionSource loaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<ImageViewImageSourceLoadedEventArgs> sourceLoaded = (_, _) => loaded.TrySetResult();
        var descriptor = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(DrawCanvas));
        var canvas = view.EditorContext.ProcessingContext.ImageShow;
        EventHandler displayLoaded = (_, _) =>
        {
            if (view.Presentation.DisplaySource is BitmapSource source && (source.Format == PixelFormats.Gray8 || source.Format == PixelFormats.Gray16))
                loaded.TrySetResult();
        };
        if (layer.Kind == ImageLayerKind.Channel) descriptor.AddValueChanged(canvas, displayLoaded);
        else view.ImageSourceLoaded += sourceLoaded;
        try
        {
            view.ComboBoxLayers.SelectedItem = layer;
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally
        {
            if (layer.Kind == ImageLayerKind.Channel) descriptor.RemoveValueChanged(canvas, displayLoaded);
            else view.ImageSourceLoaded -= sourceLoaded;
        }
    }

    private static async Task<ImageView> OpenMonochromeFixture()
    {
        ImageView view = WpfTestHost.Invoke(CreateView);
        BitmapSource pixels = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray16, null, new ushort[] { 10, 20, 30, 40 }, 4);
        pixels.Freeze();
        ColorCalibrationSnapshot snapshot = new()
        {
            Width = 2, Height = 2, RawBpp = 16, Channels = 1, TransformKind = 2,
            Coefficients = [2, 0, 0, 0, 0, 0, 0, 0, 0], Exposure = [1, 1, 1]
        };
        await WpfTestHost.Invoke(() => Publish(view, new(pixels, Parameters(pixels, snapshot, 1), "fixture.tif")));
        return view;
    }

    private sealed class BlockingCapability(bool block) : IImageContentCapability
    {
        private int _disposeCount;
        public int DisposeCount => Volatile.Read(ref _disposeCount);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Activate() { }
        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
            if (!block) return;
            Entered.TrySetResult();
            Continue.Task.GetAwaiter().GetResult();
        }
        public IEnumerable<IEditorTool> GetEditorTools() => [];
        public void OnEditorToolsActivated(EditorContext context) { }
        public void OnEditorToolsDeactivated(EditorContext context) { }
    }

    private static Task Publish(ImageView view, DecodedImageContent content)
    {
        view.Config.SetImageMetadata(ImageViewPropertyKeys.FilePath, content.FilePath, nameof(EmbeddedCalibratedImageTests), "fixture identity");
        return view.PublishDecodedImageContentAsync(content, content.Pixels, CancellationToken.None);
    }

    private static ColorVisionTiffParameters Parameters(BitmapSource pixels, ColorCalibrationSnapshot snapshot, int channels) => new()
    {
        Rows = pixels.PixelHeight, Cols = pixels.PixelWidth, Bpp = snapshot.RawBpp, PixelChannels = channels,
        PixelLayout = pixels.Format.ToString(), PixelSha256 = ColorVisionPixelBinding.ComputeSha256(pixels),
        PixelRole = "calibration-input", Calibration = JToken.FromObject(snapshot)
    };

    private static BitmapSource CreatePixels(int bits, int channels)
    {
        PixelFormat format = channels == 1 ? bits == 8 ? PixelFormats.Gray8 : PixelFormats.Gray16 : bits == 8 ? PixelFormats.Bgr24 : PixelFormats.Rgb48;
        int stride = 2 * channels * (bits / 8);
        byte[] data = new byte[stride * 2];
        for (int pixel = 0; pixel < 4; pixel++)
            for (int c = 0; c < channels; c++)
            {
                int logicalChannel = channels == 3 && bits == 8 ? 2 - c : c;
                data[(pixel * channels + c) * (bits / 8)] = (byte)((pixel + 1) * 10 + logicalChannel * 2);
            }
        BitmapSource pixels = BitmapSource.Create(2, 2, 96, 96, format, null, data, stride);
        pixels.Freeze();
        return pixels;
    }

    private static ImageView CreateView()
    {
        Application app = Application.Current;
        app.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        app.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        app.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        app.Resources["ToolBarImage"] = new Style(typeof(Image));
        app.Resources["BaseStyle"] = new Style(typeof(Control));
        app.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        app.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
        ImageView view = new();
        view.IEditorToolFactory.IEditorTools.Clear();
        view.RegisterImageContentProvider(new CalibratedImageContentProvider());
        return view;
    }
}

