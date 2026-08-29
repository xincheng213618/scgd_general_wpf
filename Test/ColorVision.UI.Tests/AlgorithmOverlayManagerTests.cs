using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmOverlayManagerTests
{
    [Fact]
    public void ApplyChangedFailureRollsBackTheStoreMutation()
    {
        AlgorithmOverlayStore store = new();
        store.Changed += (_, _) => throw new InvalidOperationException("subscriber failed");

        Assert.Throws<InvalidOperationException>(() => store.Apply(Overlay("failed-apply", AlgorithmOverlayLifetime.Transient)));

        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public void RemoveChangedFailureRestoresTheRemovedEntry()
    {
        AlgorithmOverlayStore store = new();
        AlgorithmOverlayArtifact artifact = Overlay("failed-remove", AlgorithmOverlayLifetime.Persistent);
        store.Apply(artifact);
        store.Changed += (_, _) => throw new InvalidOperationException("subscriber failed");

        Assert.Throws<InvalidOperationException>(() => store.Remove(artifact.Name));

        Assert.Same(artifact, Assert.Single(store.Snapshot()));
    }

    [Fact]
    public void SourceCommitRemovesTransientVisualAndMetadata()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            long sourceRevision = context.ImageRevision;
            int baselineVisuals = context.ImageShow.Visuals.Count;
            using AlgorithmResult result = Result("transient", AlgorithmOverlayLifetime.Transient);
            IDisposable session = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, result);

            AlgorithmOverlayRegistrationSnapshot registration = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Equal(context.DocumentInstanceId, registration.DocumentInstanceId);
            Assert.Equal(sourceRevision, registration.SourceRevision);
            Assert.True(context.ImageShow.ContainsVisual(registration.Visual));
            Assert.Equal(baselineVisuals + 1, context.ImageShow.Visuals.Count);
            Assert.Single(context.AlgorithmOverlays.Snapshot());

            context.NotifySourcePixelsChanged();

            Assert.Equal(sourceRevision + 1, context.ImageRevision);
            Assert.Equal(baselineVisuals, context.ImageShow.Visuals.Count);
            Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
            session.Dispose();
        });
    }

    [Fact]
    public void PersistentOverlaySurvivesWindowCloseAndCommitThenReplacementClearsIt()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            Guid documentInstanceId = context.DocumentInstanceId;
            long sourceRevision = context.ImageRevision;
            int baselineVisuals = context.ImageShow.Visuals.Count;
            using AlgorithmResult result = Result("persistent", AlgorithmOverlayLifetime.Persistent);
            IDisposable windowSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, result);
            Visual visual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;

            windowSession.Dispose();
            Assert.True(context.ImageShow.ContainsVisual(visual));
            Assert.Single(context.AlgorithmOverlays.Snapshot());

            context.NotifySourcePixelsChanged();
            AlgorithmOverlayRegistrationSnapshot rebased = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Equal(documentInstanceId, rebased.DocumentInstanceId);
            Assert.Equal(sourceRevision + 1, rebased.SourceRevision);
            Assert.Same(visual, rebased.Visual);

            host.View.SetImageSource(Bitmap(9), enableEditorImageServices: false, configureDefaultLayerController: false);
            Assert.Equal(baselineVisuals, context.ImageShow.Visuals.Count);
            Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

    [Fact]
    public void ClearRemovesPersistentVisualAndMetadata()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            using AlgorithmResult result = Result("persistent", AlgorithmOverlayLifetime.Persistent);
            using IDisposable session = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, result);
            Visual visual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;

            host.View.Clear();

            Assert.False(context.ImageShow.ContainsVisual(visual));
            Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

    [Fact]
    public void StaleSameNameSessionCannotRemoveReplacement()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            using AlgorithmResult firstResult = Result("same-name", AlgorithmOverlayLifetime.Transient, 1);
            IDisposable firstSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, firstResult);
            Visual firstVisual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;

            using AlgorithmResult secondResult = Result("same-name", AlgorithmOverlayLifetime.Transient, 2);
            IDisposable secondSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, secondResult);
            Visual secondVisual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;
            Assert.NotSame(firstVisual, secondVisual);
            Assert.False(context.ImageShow.ContainsVisual(firstVisual));
            Assert.True(context.ImageShow.ContainsVisual(secondVisual));

            firstSession.Dispose();
            Assert.True(context.ImageShow.ContainsVisual(secondVisual));
            Assert.Single(context.AlgorithmOverlays.Snapshot());

            secondSession.Dispose();
            Assert.False(context.ImageShow.ContainsVisual(secondVisual));
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

    [Fact]
    public void CompatibilityFacadeClearAlsoRemovesManagedVisual()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            using AlgorithmResult result = Result("compatibility", AlgorithmOverlayLifetime.Persistent);
            using IDisposable session = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, result);
            Visual visual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;

            context.AlgorithmOverlays.ClearPersistent();

            Assert.False(context.ImageShow.ContainsVisual(visual));
            Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

    [Fact]
    public void QueuedFacadeClearCannotDeleteSameNameReplacement()
    {
        using TestImageView host = new();
        AlgorithmOverlayArtifact sharedArtifact = new(
            "queued-same-name",
            AlgorithmOverlayLifetime.Transient,
            [new AlgorithmOverlayItem("point", new AlgorithmOverlayStyle())]);
        IDisposable? firstSession = null;
        IDisposable? replacementSession = null;
        Visual? replacementVisual = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
                using AlgorithmResult first = Result(sharedArtifact, 1);
                firstSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, first);

                // Clear on a worker so the manager's visual cleanup is queued to this dispatcher.
                // Register the replacement before returning control, forcing the old callback to
                // observe a same-name entry with a different artifact identity/token.
                Task.Run(context.AlgorithmOverlays.ClearTransient).GetAwaiter().GetResult();
                using AlgorithmResult replacement = Result(sharedArtifact, 2);
                replacementSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, replacement);
                replacementVisual = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations()).Visual;
            });

            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
                AlgorithmOverlayRegistrationSnapshot registration = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
                Assert.Same(replacementVisual, registration.Visual);
                Assert.True(context.ImageShow.ContainsVisual(registration.Visual));
                Assert.Same(sharedArtifact, Assert.Single(context.AlgorithmOverlays.Snapshot()));
            });
        }
        finally
        {
            WpfTestHost.Invoke(() =>
            {
                firstSession?.Dispose();
                replacementSession?.Dispose();
            });
        }
    }

    [Fact]
    public void DocumentMutationsAlsoClearFacadeOnlyArtifacts()
    {
        using TestImageView host = new();
        WpfTestHost.Invoke(() =>
        {
            ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
            context.AlgorithmOverlays.Apply(Overlay("facade-transient", AlgorithmOverlayLifetime.Transient));
            context.AlgorithmOverlays.Apply(Overlay("facade-persistent", AlgorithmOverlayLifetime.Persistent));

            context.NotifySourcePixelsChanged();
            AlgorithmOverlayArtifact persistent = Assert.Single(context.AlgorithmOverlays.Snapshot());
            Assert.Equal(AlgorithmOverlayLifetime.Persistent, persistent.Lifetime);

            host.View.Clear();
            Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

    [Fact]
    public async Task DelayedOldRevisionCleanupPreservesANewRevisionOverlay()
    {
        using TestImageView host = new();
        using ManualResetEventSlim revisionAdvanced = new();
        using ManualResetEventSlim releaseCleanup = new();
        IDisposable? oldSession = null;
        IDisposable? newSession = null;
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
                using AlgorithmResult oldResult = Result("old-revision", AlgorithmOverlayLifetime.Transient, 1);
                oldSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, oldResult);
                host.View.ImageDocumentRevisionAdvancedHook = (_, _) =>
                {
                    revisionAdvanced.Set();
                    Assert.True(releaseCleanup.Wait(TimeSpan.FromSeconds(5)));
                };
            });

            Task mutation = Task.Run(host.View.NotifySourcePixelsChanged);
            Assert.True(revisionAdvanced.Wait(TimeSpan.FromSeconds(5)));
            Visual newVisual = WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
                using AlgorithmResult newResult = Result("new-revision", AlgorithmOverlayLifetime.Transient, 2);
                newSession = AlgorithmOverlayRenderer.Apply(context, host.View.EditorContext.DrawEditorContext, newResult);
                return context.SnapshotAlgorithmOverlayRegistrations()
                    .Single(registration => registration.Name == "new-revision")
                    .Visual;
            });

            releaseCleanup.Set();
            await mutation.WaitAsync(TimeSpan.FromSeconds(5));

            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = host.View.EditorContext.ProcessingContext;
                AlgorithmOverlayRegistrationSnapshot registration = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
                Assert.Equal("new-revision", registration.Name);
                Assert.Equal(context.ImageRevision, registration.SourceRevision);
                Assert.Same(newVisual, registration.Visual);
                Assert.True(context.ImageShow.ContainsVisual(newVisual));
                Assert.Equal("new-revision", Assert.Single(context.AlgorithmOverlays.Snapshot()).Name);
            });
        }
        finally
        {
            releaseCleanup.Set();
            WpfTestHost.Invoke(() =>
            {
                host.View.ImageDocumentRevisionAdvancedHook = null;
                oldSession?.Dispose();
                newSession?.Dispose();
            });
        }
    }

    private static AlgorithmResult Result(
        string name,
        AlgorithmOverlayLifetime lifetime,
        double x = 1)
        => Result(new AlgorithmOverlayArtifact(
            name,
            lifetime,
            [new AlgorithmOverlayItem("point", new AlgorithmOverlayStyle())]), x);

    private static AlgorithmResult Result(AlgorithmOverlayArtifact overlay, double x)
    {
        const string geometryId = "point";
        return new AlgorithmResult
        {
            InvocationId = Guid.NewGuid(),
            AlgorithmId = StandardAlgorithmIds.RoiStatistics,
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmGeometryArtifact(
                    "geometry",
                    AlgorithmCoordinateSpace.Pixel,
                    [new AlgorithmGeometry(geometryId, AlgorithmGeometryKind.Point, [new AlgorithmPoint(x, 1)])]),
                overlay,
            ],
        };
    }

    private static AlgorithmOverlayArtifact Overlay(string name, AlgorithmOverlayLifetime lifetime)
        => new(name, lifetime, Array.Empty<AlgorithmOverlayItem>());

    private static WriteableBitmap Bitmap(byte value = 1)
    {
        WriteableBitmap bitmap = new(2, 2, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 2, 2), new[] { value, value, value, value }, 2, 0);
        return bitmap;
    }

    private sealed class TestImageView : IDisposable
    {
        public TestImageView()
        {
            View = WpfTestHost.Invoke(() =>
            {
                EnsureImageViewTestResources();
                ImageView view = new();
                view.SetImageSource(Bitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
                return view;
            });
        }

        public ImageView View { get; }

        public void Dispose() => WpfTestHost.Invoke(View.Dispose);
    }

    private static void EnsureImageViewTestResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }
}
