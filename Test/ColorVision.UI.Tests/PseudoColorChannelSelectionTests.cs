using ColorVision.Algorithms;
using ColorVision.Common.Utilities;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Layers;
using ColorVision.ImageEditor.Presentation.PseudoColor;
using System.Collections;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

public sealed class PseudoColorChannelSelectionTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Watchdog = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("red")]
    [InlineData("composite")]
    public async Task TurningPseudoColorOffRestoresTheSelectedLayer(string selectedLayer)
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync(selectedLayer);
            WpfTestHost.Invoke(() => fixture.AssertSelectedDisplay(selectedLayer));
            await fixture.EnablePseudoColorAsync();
            var enabled = WpfTestHost.Invoke(fixture.Snapshot);
            List<Task<DisplaySnapshot>> offTurnCheckpoints = [];

            WpfTestHost.Invoke(() =>
            {
                fixture.SourceChanged = () =>
                {
                    if (!ReferenceEquals(fixture.View.ImageShow.Source, fixture.Source)) return;
                    // Keep Cancel's synchronous notification; observe the display after its UI turn without pumping it.
                    offTurnCheckpoints.Add(fixture.View.Dispatcher.InvokeAsync(fixture.Snapshot, DispatcherPriority.Send).Task);
                };
                fixture.State.IsEnabled = false;
            });
            await fixture.WaitForOwnWorkAsync();
            Task<DisplaySnapshot>[] queuedCheckpoints = WpfTestHost.Invoke(() =>
            {
                fixture.SourceChanged = null;
                return offTurnCheckpoints.ToArray();
            });
            DisplaySnapshot[] checkpoints = await Task.WhenAll(queuedCheckpoints).WaitAsync(Watchdog);
            WpfTestHost.Invoke(() =>
            {
                output.WriteLine(JsonSerializer.Serialize(new { selectedLayer, enabled, checkpoints, disabled = fixture.Snapshot() }));
                fixture.AssertDocumentUnchanged();
                Assert.False(fixture.State.IsEnabled);
                Assert.False(fixture.Context.HasActiveAlgorithmPreview);
                fixture.AssertSelectedDisplay(selectedLayer);
                if (selectedLayer == "red")
                {
                    Assert.NotEmpty(checkpoints);
                    Assert.All(checkpoints, checkpoint =>
                    {
                        Assert.Equal(PixelFormats.Gray8.ToString(), checkpoint.Format);
                        Assert.Equal(new[] { 31, 61, 91, 121 }, checkpoint.Pixels);
                    });
                }
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task ChangingTheSelectedChannelWhileEnabledRerendersPseudoColorAndKeepsItOnDisable()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            await fixture.EnablePseudoColorAsync();
            int previousRuns = fixture.Provider.Runs.Length;

            await fixture.SelectLayerAsync("green");
            var changedWhileEnabled = WpfTestHost.Invoke(fixture.Snapshot);
            ProviderRun[] runsAfterChange = fixture.Provider.Runs;
            WpfTestHost.Invoke(() => fixture.State.IsEnabled = false);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                output.WriteLine(JsonSerializer.Serialize(new { previousRuns, changedWhileEnabled, runsAfterChange, disabled = fixture.Snapshot() }));
                fixture.AssertDocumentUnchanged();
                // Observe quiescent state first: an absent rerender is a behavior failure, not a timeout waiting for it.
                Assert.True(runsAfterChange.Length > previousRuns);
                Assert.Equal(1, runsAfterChange[^1].Channel);
                Assert.Equal(PixelFormats.Bgr24.ToString(), changedWhileEnabled.Format);
                Assert.True(changedWhileEnabled.HasActivePreview);
                fixture.AssertSelectedDisplay("green");
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Theory]
    [InlineData("algorithm")]
    [InlineData("display")]
    public async Task TurningPseudoColorOffPreservesASynchronousReplacementDuringCancel(string takeover)
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        ImageAlgorithmPreviewSession? replacement = null;
        AlgorithmInvocationClaim? replacementClaim = null;
        WriteableBitmap? replacementDisplay = null;
        int callbacks = 0;
        Exception? callbackFailure = null;
        bool replacementWasPublished = false;
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            await fixture.EnablePseudoColorAsync();
            WpfTestHost.Invoke(() => fixture.SourceChanged = () =>
            {
                if (!ReferenceEquals(fixture.View.ImageShow.Source, fixture.Source) || callbacks != 0) return;
                callbacks++;
                try
                {
                    if (takeover == "algorithm")
                    {
                        replacement = ImageAlgorithmPreviewSession.StartForCatalogPreview(fixture.Context);
                        replacementClaim = fixture.CurrentClaim();
                    }
                    else
                    {
                        replacementDisplay = new WriteableBitmap(2, 2, 96, 96, PixelFormats.Gray8, null);
                        replacementDisplay.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 201, 202, 203, 204 }, 2, 0);
                        replacementDisplay.Freeze();
                        fixture.Context.Presentation.Publish(replacementDisplay, replacementDisplay);
                        replacementWasPublished = ReferenceEquals(replacementDisplay, fixture.View.ImageShow.Source);
                    }
                }
                catch (Exception ex)
                {
                    callbackFailure = ex;
                    throw;
                }
            });

            WpfTestHost.Invoke(() => fixture.State.IsEnabled = false);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                output.WriteLine(JsonSerializer.Serialize(new { takeover, callbacks, replacementClaim, replacementWasPublished, callbackFailure = callbackFailure?.ToString(), final = fixture.Snapshot() }));
                Assert.Null(callbackFailure);
                Assert.Equal(1, callbacks);
                fixture.AssertDocumentUnchanged();
                Assert.Equal("red", fixture.View.SelectedLayer!.Id);
                if (replacement != null)
                {
                    Assert.True(replacement.OwnsHostPreview);
                    Assert.Equal(replacementClaim, fixture.CurrentClaim());
                    Assert.Same(fixture.Source, fixture.View.ImageShow.Source);
                }
                else
                {
                    Assert.Same(replacementDisplay, fixture.View.ImageShow.Source);
                    Assert.Same(replacementDisplay, fixture.Context.FunctionImage);
                }
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => { fixture.SourceChanged = null; replacement?.Dispose(); });
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Rgb48RedUsesCanonicalBgrChannelForStaticPseudoColorAndSourceChannelForFrames()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture(rgb48: true));
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            await fixture.EnablePseudoColorAsync();
            WpfTestHost.Invoke(() =>
            {
                Assert.True(fixture.Context.DisplayEffects.TryCapturePseudoColorRequest(out var frame));
                ProviderRun run = fixture.Provider.Runs[^1];
                output.WriteLine(JsonSerializer.Serialize(new { sourceIndex = fixture.View.GetSelectedLayerSourceChannelIndex(), frame, run, final = fixture.Snapshot() }));
                fixture.AssertDocumentUnchanged();
                Assert.Equal(0, fixture.View.GetSelectedLayerSourceChannelIndex());
                Assert.Equal(0, frame.Channel);
                Assert.Equal(AlgorithmImageFormat.Bgr48, run.InputFormat);
                Assert.Equal(2, run.Channel);
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task ChangingDisabledPseudoColorSettingsDoesNotReplaceTheSelectedChannel()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            BitmapSource selected = WpfTestHost.Invoke(() =>
            {
                Assert.False(fixture.State.IsEnabled);
                fixture.AssertSelectedDisplay("red");
                return Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageShow.Source);
            });
            WpfTestHost.Invoke(() =>
            {
                fixture.State.SliderValueStart = 5;
                fixture.State.SliderValueEnd = 200;
                fixture.State.ColormapTypes = ColorVision.Core.ColormapTypes.COLORMAP_TURBO;
            });
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                output.WriteLine(JsonSerializer.Serialize(new { sameDisplay = ReferenceEquals(selected, fixture.View.ImageShow.Source), final = fixture.Snapshot() }));
                fixture.AssertDocumentUnchanged();
                Assert.False(fixture.State.IsEnabled);
                Assert.Empty(fixture.Provider.Runs);
                fixture.AssertSelectedDisplay("red");
                Assert.Same(selected, fixture.View.ImageShow.Source);
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task ReenablingDuringOffRestorationRendersTheNewRequestAfterCancellationCompletes()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        int callbacks = 0;
        long callbackBeforeEnableGeneration = -1, callbackAfterEnableGeneration = -1;
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            await fixture.EnablePseudoColorAsync();
            ProviderRun[] previousRuns = fixture.Provider.Runs;
            long generationBeforeOff = WpfTestHost.Invoke(() => fixture.Context.AlgorithmPreviewGeneration);
            WpfTestHost.Invoke(() => fixture.SourceChanged = () =>
            {
                if (!ReferenceEquals(fixture.View.ImageShow.Source, fixture.Source) || callbacks != 0) return;
                callbacks++;
                callbackBeforeEnableGeneration = fixture.Context.AlgorithmPreviewGeneration;
                fixture.State.IsEnabled = true;
                callbackAfterEnableGeneration = fixture.Context.AlgorithmPreviewGeneration;
            });

            WpfTestHost.Invoke(() => fixture.State.IsEnabled = false);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                DisplaySnapshot final = fixture.Snapshot();
                output.WriteLine(JsonSerializer.Serialize(new
                {
                    callbacks, previousRuns, generationBeforeOff, callbackBeforeEnableGeneration, callbackAfterEnableGeneration,
                    finalGeneration = fixture.Context.AlgorithmPreviewGeneration, enabled = fixture.State.IsEnabled, final
                }));
                fixture.AssertDocumentUnchanged();
                Assert.Equal(1, callbacks);
                Assert.True(fixture.State.IsEnabled);
                Assert.True(final.Runs.Length > previousRuns.Length);
                Assert.NotEqual(previousRuns[^1].InvocationId, final.Runs[^1].InvocationId);
                Assert.Equal(2, final.Runs[^1].Channel);
                Assert.True(final.HasActivePreview);
                Assert.Equal(PixelFormats.Bgr24.ToString(), final.Format);
                Assert.Equal(ControlledPseudoProvider.OutputPixels.Select(value => (int)value), final.Pixels);
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChangingChannelPreservesManualRangeOrRecalculatesAutomaticRange(bool automaticRange)
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture());
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            await fixture.EnablePseudoColorAsync();
            WpfTestHost.Invoke(() =>
            {
                fixture.State.IsAutoSetRange = automaticRange;
                if (!automaticRange)
                {
                    fixture.State.SliderValueStart = 20;
                    fixture.State.SliderValueEnd = 180;
                }
            });
            await fixture.WaitForOwnWorkAsync();
            RangeSnapshot red = WpfTestHost.Invoke(fixture.CaptureRange);
            output.WriteLine(JsonSerializer.Serialize(new { phase = "red-configured", automaticRange, red }));
            ProviderRun previous = red.Display.Runs[^1];
            Assert.Equal(automaticRange ? 31d : 20d, red.ValueStart);
            Assert.Equal(automaticRange ? 121d : 180d, red.ValueEnd);
            Assert.Equal((uint)red.ValueStart, previous.Minimum);
            Assert.Equal((uint)red.ValueEnd, previous.Maximum);
            Assert.Equal(automaticRange, previous.AutoRange);
            Assert.False(previous.UseNominalRange);
            Assert.Equal(2, previous.Channel);
            Assert.True(red.Display.HasActivePreview);
            if (automaticRange)
            {
                // These are literal extrema of the fixture's actual Red bytes, not a helper-derived oracle.
                Assert.Equal(31u, red.DataMinimum);
                Assert.Equal(121u, red.DataMaximum);
                Assert.Equal(31d, red.SliderMinimum);
                Assert.Equal(121d, red.SliderMaximum);
                Assert.Equal(31u, previous.DataMinimum);
                Assert.Equal(121u, previous.DataMaximum);
            }

            await fixture.SelectLayerAsync("green");
            WpfTestHost.Invoke(() =>
            {
                RangeSnapshot green = fixture.CaptureRange();
                ProviderRun current = green.Display.Runs[^1];
                output.WriteLine(JsonSerializer.Serialize(new { phase = "green-selected", automaticRange, red, green }));
                fixture.AssertDocumentUnchanged();
                Assert.True(fixture.State.IsEnabled);
                Assert.Equal("green", green.Display.SelectedLayer);
                Assert.Equal(automaticRange, green.AutomaticRange);
                Assert.NotEqual(previous.InvocationId, current.InvocationId);
                Assert.Equal(1, current.Channel);
                Assert.True(green.Display.Publications > red.Display.Publications);
                Assert.True(green.Display.HasActivePreview);
                Assert.Equal(automaticRange ? 21d : 20d, green.ValueStart);
                Assert.Equal(automaticRange ? 111d : 180d, green.ValueEnd);
                Assert.Equal((uint)green.ValueStart, current.Minimum);
                Assert.Equal((uint)green.ValueEnd, current.Maximum);
                Assert.Equal(automaticRange, current.AutoRange);
                Assert.False(current.UseNominalRange);
                if (automaticRange)
                {
                    Assert.Equal(21u, green.DataMinimum);
                    Assert.Equal(111u, green.DataMaximum);
                    Assert.Equal(21d, green.SliderMinimum);
                    Assert.Equal(111d, green.SliderMaximum);
                    Assert.Equal(21u, current.DataMinimum);
                    Assert.Equal(111u, current.DataMaximum);
                }
            });
        }
        finally { await fixture.DisposeAsync(); }
    }

    [Fact]
    public async Task ContinuousRawFramesKeepTheSelectedRedProjection()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture(defaultSourceEntry: true));
        Guid sourceId = Guid.NewGuid();
        TaskCompletionSource<WriteableBitmap> presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int presentations = 0;
        List<string> publishedFormats = [];
        void OnPresented(Guid id, WriteableBitmap source)
        {
            if (id != sourceId) return;
            presentations++;
            presented.TrySetResult(source);
        }
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            long revision = WpfTestHost.Invoke(() =>
            {
                Assert.True(fixture.View.EnableEditorImageServices);
                Assert.Equal(Visibility.Visible, fixture.View.ComboBoxLayers.Visibility);
                Assert.Equal(4, fixture.View.ComboBoxLayers.Items.Count);
                Assert.False(fixture.State.IsEnabled);
                fixture.AssertSelectedDisplay("red");
                fixture.Context.StreamPresentation.FramePresented += OnPresented;
                fixture.SourceChanged = () => publishedFormats.Add(Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageShow.Source).Format.ToString());
                return fixture.Context.ImageRevision;
            });
            Guid document = WpfTestHost.Invoke(() => fixture.Context.DocumentInstanceId);
            byte[] framePixels = [255, 20, 0, 0, 60, 255, 255, 100, 0, 0, 140, 255];
            WriteableBitmap producer = WpfTestHost.Invoke(() =>
            {
                WriteableBitmap frame = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
                frame.WritePixels(new Int32Rect(0, 0, 2, 2), framePixels, 6, 0);
                fixture.Context.StreamPresentation.Submit(frame, sourceId);
                return frame;
            });
            WriteableBitmap accepted = await presented.Task.WaitAsync(Watchdog);
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(fixture.Context.ViewBitmapSource);
                byte[] actualSource = new byte[12];
                source.CopyPixels(actualSource, 6, 0);
                DisplaySnapshot display = fixture.Snapshot();
                output.WriteLine(JsonSerializer.Serialize(new { phase = "next-raw-frame", presentations,
                    document, actualDocument = fixture.Context.DocumentInstanceId, revision, actualRevision = fixture.Context.ImageRevision,
                    sourceFormat = source.Format.ToString(), actualSource, source.IsFrozen,
                    producerIsFrozen = producer.IsFrozen, sourceIsAccepted = ReferenceEquals(source, accepted),
                    functionIsDisplay = ReferenceEquals(fixture.Context.FunctionImage, fixture.View.ImageShow.Source), publishedFormats, display }));
                Assert.Equal(1, presentations);
                Assert.Equal(document, fixture.Context.DocumentInstanceId);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.Equal(PixelFormats.Bgr24, source.Format);
                Assert.Equal(framePixels, actualSource);
                Assert.True(source.IsFrozen);
                Assert.False(producer.IsFrozen);
                Assert.NotSame(producer, source);
                Assert.Same(accepted, source);
                Assert.False(fixture.State.IsEnabled);
                Assert.True(fixture.Context.StreamPresentation.IsActive);
                Assert.Empty(fixture.Provider.Runs);
                Assert.Equal("red", display.SelectedLayer);
                Assert.Equal("red", display.ComboLayer);
                Assert.Equal(PixelFormats.Gray8.ToString(), display.Format);
                Assert.Equal(new[] { 0, 255, 0, 255 }, display.Pixels);
                Assert.NotEmpty(publishedFormats);
                Assert.All(publishedFormats, format => Assert.Equal(PixelFormats.Gray8.ToString(), format));
                Assert.Same(fixture.View.ImageShow.Source, fixture.Context.FunctionImage);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.FramePresented -= OnPresented);
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ContinuousNativePseudoColorOffRestoresTheLatestSelectedRedProjection()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture(defaultSourceEntry: true));
        Guid sourceId = Guid.NewGuid();
        TaskCompletionSource<WriteableBitmap> presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int presentations = 0;
        void OnPresented(Guid id, WriteableBitmap source)
        {
            if (id != sourceId) return;
            presentations++;
            presented.TrySetResult(source);
        }
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            byte[] framePixels = [255, 20, 0, 0, 60, 255, 255, 100, 0, 0, 140, 255];
            long revision = WpfTestHost.Invoke(() =>
            {
                fixture.AssertSelectedDisplay("red");
                fixture.Context.StreamPresentation.FramePresented += OnPresented;
                long beforeSubmit = fixture.Context.ImageRevision;
                WriteableBitmap frame = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
                frame.WritePixels(new Int32Rect(0, 0, 2, 2), framePixels, 6, 0);
                fixture.Context.StreamPresentation.Submit(frame, sourceId);
                return beforeSubmit;
            });
            Guid document = WpfTestHost.Invoke(() => fixture.Context.DocumentInstanceId);
            await presented.Task.WaitAsync(Watchdog);
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            WpfTestHost.Invoke(() =>
            {
                presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.State.ColormapTypes = ColorVision.Core.ColormapTypes.COLORMAP_HOT;
                fixture.State.SliderValueStart = 0;
                fixture.State.SliderValueEnd = 255;
                fixture.State.IsEnabled = true;
            });
            await presented.Task.WaitAsync(Watchdog);
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                DisplaySnapshot enabled = fixture.Snapshot();
                output.WriteLine(JsonSerializer.Serialize(new { phase = "native-stream-pseudo-on", presentations, enabled }));
                WriteNativeOwnerIdentity();
                Assert.True(fixture.State.IsEnabled);
                Assert.Equal("red", enabled.SelectedLayer);
                Assert.Equal(PixelFormats.Bgr24.ToString(), enabled.Format);
                // HOT endpoints are black and white; Red is literally 0,255,0,255 in the submitted BGR frame.
                Assert.Equal(new[] { 0, 0, 0, 255, 255, 255, 0, 0, 0, 255, 255, 255 }, enabled.Pixels);
                Assert.Empty(fixture.Provider.Runs);
                presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
                fixture.State.IsEnabled = false;
            });
            WriteableBitmap accepted = await presented.Task.WaitAsync(Watchdog);
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(fixture.Context.ViewBitmapSource);
                byte[] actualSource = new byte[12];
                source.CopyPixels(actualSource, 6, 0);
                DisplaySnapshot disabled = fixture.Snapshot();
                output.WriteLine(JsonSerializer.Serialize(new { phase = "native-stream-pseudo-off", presentations,
                    revision, actualRevision = fixture.Context.ImageRevision, actualSource, source.IsFrozen, disabled }));
                Assert.Equal(3, presentations);
                Assert.Equal(document, fixture.Context.DocumentInstanceId);
                Assert.Equal(revision + 3, fixture.Context.ImageRevision);
                Assert.Same(accepted, source);
                Assert.Equal(PixelFormats.Bgr24, source.Format);
                Assert.Equal(framePixels, actualSource);
                Assert.True(source.IsFrozen);
                Assert.False(fixture.State.IsEnabled);
                Assert.Equal("red", disabled.SelectedLayer);
                Assert.Equal("red", disabled.ComboLayer);
                Assert.Equal(PixelFormats.Gray8.ToString(), disabled.Format);
                Assert.Equal(new[] { 0, 255, 0, 255 }, disabled.Pixels);
                Assert.Same(fixture.View.ImageShow.Source, fixture.Context.FunctionImage);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.FramePresented -= OnPresented);
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangingTheSelectedChannelBeforeQueuedRawFrameRunsKeepsTheLatestFrame()
    {
        WriteOwnerIdentity();
        Fixture fixture = WpfTestHost.Invoke(() => new Fixture(defaultSourceEntry: true));
        Guid sourceId = Guid.NewGuid();
        TaskCompletionSource<WriteableBitmap> presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int presentations = 0;
        void OnPresented(Guid id, WriteableBitmap source)
        {
            if (id != sourceId) return;
            presentations++;
            presented.TrySetResult(source);
        }
        try
        {
            await fixture.WaitForOwnWorkAsync();
            await fixture.SelectLayerAsync("red");
            WpfTestHost.Invoke(() =>
            {
                fixture.Context.StreamPresentation.FramePresented += OnPresented;
                WriteableBitmap first = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
                first.WritePixels(new Int32Rect(0, 0, 2, 2), new byte[] { 255, 20, 0, 0, 60, 255, 255, 100, 0, 0, 140, 255 }, 6, 0);
                fixture.Context.StreamPresentation.Submit(first, sourceId);
            });
            await presented.Task.WaitAsync(Watchdog);
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            await fixture.WaitForOwnWorkAsync();
            long revision = WpfTestHost.Invoke(() => fixture.Context.ImageRevision);
            Guid document = WpfTestHost.Invoke(() => fixture.Context.DocumentInstanceId);
            byte[] latestPixels = [3, 33, 203, 4, 44, 204, 5, 55, 205, 6, 66, 206];
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal("red", fixture.View.SelectedLayer!.Id);
                Assert.False(fixture.State.IsEnabled);
                Assert.Equal(new[] { 0, 255, 0, 255 }, fixture.Snapshot().Pixels);
                presented = new(TaskCreationOptions.RunContinuationsAsynchronously);
                WriteableBitmap latest = new(2, 2, 96, 96, PixelFormats.Bgr24, null);
                latest.WritePixels(new Int32Rect(0, 0, 2, 2), latestPixels, 6, 0);
                // One dispatcher turn prevents the stream worker from taking Red's pending frame before Green replaces it.
                fixture.Context.StreamPresentation.Submit(latest, sourceId);
                fixture.View.ComboBoxLayers.SelectedItem = fixture.View.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>().Single(layer => layer.Id == "green");
            });
            await WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.Completion).WaitAsync(Watchdog);
            await fixture.WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                BitmapSource source = Assert.IsAssignableFrom<BitmapSource>(fixture.Context.ViewBitmapSource);
                byte[] actualSource = new byte[12];
                source.CopyPixels(actualSource, 6, 0);
                DisplaySnapshot display = fixture.Snapshot();
                output.WriteLine(JsonSerializer.Serialize(new { phase = "queued-raw-frame-channel-change", presentations,
                    revision, actualRevision = fixture.Context.ImageRevision, sourceFormat = source.Format.ToString(), actualSource, display }));
                Assert.True(presented.Task.IsCompletedSuccessfully);
                Assert.Equal(2, presentations);
                Assert.Equal(document, fixture.Context.DocumentInstanceId);
                Assert.Equal(revision + 1, fixture.Context.ImageRevision);
                Assert.Equal(PixelFormats.Bgr24, source.Format);
                Assert.Equal(latestPixels, actualSource);
                Assert.True(source.IsFrozen);
                Assert.False(fixture.State.IsEnabled);
                Assert.Equal("green", display.SelectedLayer);
                Assert.Equal("green", display.ComboLayer);
                Assert.Equal(PixelFormats.Gray8.ToString(), display.Format);
                Assert.Equal(new[] { 33, 44, 55, 66 }, display.Pixels);
                Assert.Same(fixture.View.ImageShow.Source, fixture.Context.FunctionImage);
                Assert.Empty(fixture.Provider.Runs);
            });
        }
        finally
        {
            WpfTestHost.Invoke(() => fixture.Context.StreamPresentation.FramePresented -= OnPresented);
            await fixture.DisposeAsync();
        }
    }

    private void WriteOwnerIdentity()
    {
        string path = typeof(ImageView).Assembly.Location;
        output.WriteLine($"ImageEditor: {path}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))}");
    }

    private void WriteNativeOwnerIdentity()
    {
        string core = typeof(ColorVision.Core.OpenCVMediaHelper).Assembly.Location;
        output.WriteLine($"Core: {core}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(core)))}");
        using System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
        foreach (System.Diagnostics.ProcessModule module in process.Modules)
        {
            if (!module.ModuleName.Equals("opencv_helper.dll", StringComparison.OrdinalIgnoreCase)
                && !module.ModuleName.Equals("OpenCvSharpExtern.dll", StringComparison.OrdinalIgnoreCase)) continue;
            output.WriteLine($"Native module: {module.FileName}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(module.FileName)))}");
        }
    }

    private sealed record DisplaySnapshot(string? SelectedLayer, string? ComboLayer, string Format, int[] Pixels,
        long Revision, bool SourceIsOriginal, bool HasActivePreview, int Publications, ProviderRun[] Runs);

    private sealed record RangeSnapshot(bool AutomaticRange, uint DataMinimum, uint DataMaximum,
        double SliderMinimum, double SliderMaximum, double ValueStart, double ValueEnd, DisplaySnapshot Display);

    private sealed class Fixture
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly IDictionary Executors = (IDictionary)typeof(TaskConflator).GetField("_executors", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        private readonly string _renderKey;
        private readonly object _channels;
        private readonly object _channelSync;
        private readonly DependencyPropertyDescriptor _sourceProperty;
        private readonly byte[] _sourcePixels;
        private readonly long _revision;
        private readonly Guid _document;
        private int _publications;

        public Fixture(bool rgb48 = false, bool defaultSourceEntry = false)
        {
            ResourceDictionary resources = Application.Current.Resources;
            resources["TextBox.Small"] = new Style(typeof(TextBox));
            resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            resources["ToolBarImage"] = new Style(typeof(Image));
            resources["BaseStyle"] = new Style(typeof(Control));
            resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            Provider = new ControlledPseudoProvider();
            PseudoColorParameters defaults = new();
            AlgorithmCatalog catalog = new();
            catalog.Register(new AlgorithmDescriptor(StandardAlgorithmIds.PseudoColor, new AlgorithmVersion(1, 0, 0),
                "Controlled pseudo color", "test", "Real display and layer ownership", typeof(PseudoColorParameters),
                new AlgorithmParameterSchema(defaults.SchemaVersion, Array.Empty<AlgorithmParameterField>(), AlgorithmJson.ToElement(defaults)),
                new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48 },
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
                OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Bgr24 }));
            View = defaultSourceEntry ? new ImageView() : new ImageView(new AlgorithmRuntime(catalog, [Provider], new ImmediateScheduler()));
            Context = View.EditorContext.ProcessingContext;
            Source = new WriteableBitmap(2, 2, 96, 96, rgb48 ? PixelFormats.Rgb48 : PixelFormats.Bgr24, null);
            _sourcePixels = rgb48 ? Rgb48Pixels() : [11, 21, 31, 41, 51, 61, 71, 81, 91, 101, 111, 121];
            Source.WritePixels(new Int32Rect(0, 0, 2, 2), _sourcePixels, rgb48 ? 12 : 6, 0);
            // Immutable frames keep nested Source publication clear of mutable Freezable inheritance bookkeeping.
            Source.Freeze();
            if (defaultSourceEntry) Context.SetImageSource(Source);
            else View.SetImageSource(Source, enableEditorImageServices: false, configureDefaultLayerController: true);
            _revision = Context.ImageRevision;
            _document = Context.DocumentInstanceId;
            object controller = typeof(ColorVision.ImageEditor.Presentation.ImageDisplayEffects).GetField("_pseudoColorController", Instance)!.GetValue(Context.DisplayEffects)!;
            _renderKey = (string)controller.GetType().GetField("_renderTaskKey", Instance)!.GetValue(controller)!;
            _channels = typeof(ImageView).GetField("_channels", Instance)!.GetValue(View)!;
            _channelSync = _channels.GetType().GetField("_selectionSync", Instance)!.GetValue(_channels)!;
            _sourceProperty = DependencyPropertyDescriptor.FromProperty(Image.SourceProperty, typeof(DrawCanvas))!;
            _sourceProperty.AddValueChanged(View.ImageShow, OnSourceChanged);
            View.AlgorithmPreviewPublicationHook = _ => _publications++;
            State.IsAutoSetRange = false;
        }

        public ImageView View { get; }
        public ImageProcessingContext Context { get; }
        public WriteableBitmap Source { get; }
        public ControlledPseudoProvider Provider { get; }
        public PseudoColorState State => Context.DisplayEffects.PseudoColor;
        public Action? SourceChanged { get; set; }

        private void OnSourceChanged(object? sender, EventArgs e) => SourceChanged?.Invoke();

        public async Task SelectLayerAsync(string id)
        {
            WpfTestHost.Invoke(() => View.ComboBoxLayers.SelectedItem = View.ComboBoxLayers.Items.Cast<ImageLayerDescriptor>().Single(layer => layer.Id == id));
            await WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() => Assert.Equal(id, View.SelectedLayer!.Id));
        }

        public async Task EnablePseudoColorAsync()
        {
            WpfTestHost.Invoke(() => State.IsEnabled = true);
            await Provider.Started.Task.WaitAsync(Watchdog);
            Provider.Release.TrySetResult(true);
            await Provider.ResultDisposed.Task.WaitAsync(Watchdog);
            await WaitForOwnWorkAsync();
            WpfTestHost.Invoke(() =>
            {
                Assert.True(State.IsEnabled);
                Assert.True(_publications > 0);
                Assert.True(Context.HasActiveAlgorithmPreview);
                Assert.Equal(PixelFormats.Bgr24, Assert.IsAssignableFrom<BitmapSource>(View.ImageShow.Source).Format);
                Assert.NotSame(Source, View.ImageShow.Source);
                Assert.Equal(ControlledPseudoProvider.OutputPixels, ReadPixels(Assert.IsAssignableFrom<BitmapSource>(View.ImageShow.Source)));
            });
        }

        public Task WaitForOwnWorkAsync() => Task.Run(() =>
        {
            // Read only this repository's own executor/selection state under its locks.
            // The watchdog bounds a stuck test; no fixed delay or latency acceptance is used.
            Assert.True(SpinWait.SpinUntil(() => RenderIdle() && ChannelIdle(), Watchdog), "Watchdog: own display work did not become idle.");
        });

        private bool RenderIdle()
        {
            object? executor = Executors[_renderKey];
            if (executor == null) return true;
            object sync = executor.GetType().GetField("_lock", Instance)!.GetValue(executor)!;
            lock (sync) return !(bool)executor.GetType().GetField("_isRunning", Instance)!.GetValue(executor)!;
        }

        private bool ChannelIdle()
        {
            lock (_channelSync) return _channels.GetType().GetField("_selection", Instance)!.GetValue(_channels) == null;
        }

        public AlgorithmInvocationClaim? CurrentClaim()
            => Context.AlgorithmRuntime.InvocationCoordinator.TryGetCurrent(new AlgorithmInvocationScope(Context.DocumentInstanceId, Context.ImageRevision), out AlgorithmInvocationClaim claim) ? claim : null;

        public DisplaySnapshot Snapshot()
        {
            BitmapSource display = Assert.IsAssignableFrom<BitmapSource>(View.ImageShow.Source);
            return new(View.SelectedLayer?.Id, (View.ComboBoxLayers.SelectedItem as ImageLayerDescriptor)?.Id,
                display.Format.ToString(), ReadPixels(display).Select(value => (int)value).ToArray(), Context.ImageRevision,
                ReferenceEquals(Source, Context.ViewBitmapSource), Context.HasActiveAlgorithmPreview, _publications, Provider.Runs);
        }

        public RangeSnapshot CaptureRange()
            => new(State.IsAutoSetRange, State.DataMin, State.DataMax, State.SliderMinimum, State.SliderMaximum,
                State.SliderValueStart, State.SliderValueEnd, Snapshot());

        public void AssertDocumentUnchanged()
        {
            Assert.Same(Source, Context.ViewBitmapSource);
            Assert.Equal(_revision, Context.ImageRevision);
            Assert.Equal(_document, Context.DocumentInstanceId);
            Assert.Equal(_sourcePixels, ReadPixels(Source));
        }

        public void AssertSelectedDisplay(string id)
        {
            Assert.Equal(id, View.SelectedLayer!.Id);
            Assert.Equal(id, Assert.IsType<ImageLayerDescriptor>(View.ComboBoxLayers.SelectedItem).Id);
            if (id == "composite") Assert.Same(Source, View.ImageShow.Source);
            else
            {
                BitmapSource display = Assert.IsAssignableFrom<BitmapSource>(View.ImageShow.Source);
                Assert.Equal(PixelFormats.Gray8, display.Format);
                Assert.Equal(id == "red" ? new byte[] { 31, 61, 91, 121 } : new byte[] { 21, 51, 81, 111 }, ReadPixels(display));
            }
        }

        public async Task DisposeAsync()
        {
            Provider.Release.TrySetResult(true);
            WpfTestHost.Invoke(() =>
            {
                SourceChanged = null;
                View.AlgorithmPreviewPublicationHook = null;
                _sourceProperty.RemoveValueChanged(View.ImageShow, OnSourceChanged);
                View.Dispose();
            });
            await WaitForOwnWorkAsync();
        }

        private static byte[] ReadPixels(BitmapSource bitmap)
        {
            int stride = (bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);
            return pixels;
        }

        private static byte[] Rgb48Pixels()
        {
            ushort[] values = [1000, 2000, 3000, 1100, 2100, 3100, 1200, 2200, 3200, 1300, 2300, 3300];
            byte[] bytes = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                bytes[i * 2] = (byte)values[i];
                bytes[i * 2 + 1] = (byte)(values[i] >> 8);
            }
            return bytes;
        }
    }

    private sealed class ImmediateScheduler : IAlgorithmExecutionScheduler
    {
        public ValueTask<AlgorithmResult> ScheduleAsync(AlgorithmProviderMetadata metadata,
            Func<CancellationToken, ValueTask<AlgorithmResult>> operation, CancellationToken cancellationToken)
            => operation(cancellationToken);
    }

    private sealed record ProviderRun(Guid InvocationId, int Channel, AlgorithmImageFormat InputFormat,
        uint Minimum, uint Maximum, bool AutoRange, bool UseNominalRange, uint DataMinimum, uint DataMaximum);

    private sealed class ControlledPseudoProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public static readonly byte[] OutputPixels = [150, 70, 220, 151, 71, 221, 152, 72, 222, 153, 73, 223];
        private readonly List<ProviderRun> _runs = [];
        public ProviderRun[] Runs { get { lock (_runs) return _runs.ToArray(); } }
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ResultDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public AlgorithmProviderMetadata Metadata { get; } = new("test.pseudo-color-channel", "Controlled pseudo color",
            AlgorithmProviderKind.Cpu, AlgorithmExecutionPlane.Local, 1,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48 });

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            reason = descriptor.Id == StandardAlgorithmIds.PseudoColor ? null : "wrong algorithm";
            return reason == null;
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
            => CanExecuteDescriptor(descriptor, out reason);

        public async ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            PseudoColorParameters parameters = Assert.IsType<PseudoColorParameters>(context.Parameters);
            lock (_runs) _runs.Add(new(context.Invocation.InvocationId, parameters.Channel, context.Inputs[0].Image.Format,
                parameters.Minimum, parameters.Maximum, parameters.AutoRange, parameters.UseNominalRange, parameters.DataMinimum, parameters.DataMaximum));
            Started.TrySetResult(true);
            await Release.Task.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("image", "primary", new AlgorithmImageBuffer(2, 2, 6, AlgorithmImageFormat.Bgr24, OutputPixels.ToArray())), new DisposalSignal(ResultDisposed)],
            };
        }
    }

    private sealed record DisposalSignal(TaskCompletionSource<bool> Signal) : AlgorithmArtifact("test-result-disposal"), IDisposable
    {
        public void Dispose() => Signal.TrySetResult(true);
    }
}
