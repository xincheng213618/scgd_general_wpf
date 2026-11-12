using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Themes;
using ColorVision.UI;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotLoadedBatchResultsSourceTests
{
    [Fact]
    public void ExistingMainRecordsProjectOnlyCachedMetadataWithoutChangingSelectionOrLoadingImages()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var fixture = new PageFixture(11);
            var image = new ViewResultImage { Id = 7, ResultCode = 23, FilePath = "private-image-path", FileUrl = "private-image-url" };
            typeof(ViewResultImage).GetField("_totalTime", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(image, 3_700_001L);
            var created = new DateTime(2026, 10, 27, 1, 0, 0, DateTimeKind.Unspecified);
            var algorithm = new ViewResultAlg
            {
                Id = 7, ResultCode = null, TotalTime = -17, CreateTime = created, ResultType = (ViewResultAlgType)7319,
                POITemplateName = "Loaded template", FilePath = "private-algorithm-path", ResultDesc = "private-raw-message",
            };
            fixture.Page.ViewResultImages.Add(image);
            fixture.Page.ViewResultAlgs.Add(algorithm);
            var imageList = Assert.IsType<ListView>(fixture.Page.FindName("listView1"));
            var algorithmList = Assert.IsType<ListView>(fixture.Page.FindName("listView2"));
            var selection = (imageList.SelectedItem, algorithmList.SelectedItem);
            var beforeImages = fixture.Page.ViewResultImages.ToArray();
            var beforeAlgorithms = fixture.Page.ViewResultAlgs.ToArray();

            var snapshot = new CopilotLoadedBatchResultsReader().CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var source = Assert.Single(snapshot.Sources);
            var imageRow = Assert.Single(source.Results, row => row.Kind == "image");
            var algorithmRow = Assert.Single(source.Results, row => row.Kind == "algorithm");

            Assert.Equal(11, source.BatchId);
            Assert.NotEqual(imageRow.ResultRef, algorithmRow.ResultRef);
            Assert.Equal(7, imageRow.InternalId);
            Assert.Equal(3_700_001L, imageRow.DurationMs);
            Assert.Equal(23, imageRow.ResultCode);
            Assert.Equal("01:40:001", image.TotalTime);
            Assert.Null(algorithmRow.ResultCode);
            Assert.Equal(-17, algorithmRow.DurationMs);
            Assert.Equal(7319, algorithmRow.TypeCode);
            Assert.Equal(created, algorithmRow.CreatedAt);
            Assert.Equal(DateTimeKind.Unspecified, algorithmRow.CreatedAt!.Value.Kind);
            Assert.DoesNotContain("private-", JsonSerializer.Serialize(snapshot), StringComparison.Ordinal);
            Assert.Equal(selection, (imageList.SelectedItem, algorithmList.SelectedItem));
            Assert.Equal(beforeImages, fixture.Page.ViewResultImages);
            Assert.Equal(beforeAlgorithms, fixture.Page.ViewResultAlgs);
            Assert.Null(algorithm.ViewResults);
            Assert.Matches("^[a-f0-9]{32}$", snapshot.RegistryInstanceId);
            Assert.Matches("^[a-f0-9]{32}$", source.SourceRef);
            Assert.All(source.Results, row => Assert.Matches("^[a-f0-9]{32}$", row.ResultRef));
            Assert.Throws<NotSupportedException>(() => Assert.IsAssignableFrom<IList<CopilotLoadedBatchResultRow>>(source.Results)[0] = source.Results[1]);
        });
    }

    [Fact]
    public void AllRegisteredPagesAreDistinctEvenForTheSameBatchAndTheSameExistingRowObject()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var first = new PageFixture(31);
            using var second = new PageFixture(31);
            var row = new ViewResultImage { Id = 7 };
            first.Page.ViewResultImages.Add(row);
            second.Page.ViewResultImages.Add(row);
            var reader = new CopilotLoadedBatchResultsReader();

            var before = reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
            var repeated = reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.Equal(2, before.Sources.Count);
            Assert.Equal(2, before.Sources.Select(source => source.SourceRef).Distinct().Count());
            Assert.Equal(2, before.Sources.SelectMany(source => source.Results).Select(result => result.ResultRef).Distinct().Count());
            Assert.Equal(before.Sources.Select(source => source.SourceRef), repeated.Sources.Select(source => source.SourceRef));
            Assert.Equal(before.Sources.SelectMany(source => source.Results).Select(result => result.ResultRef),
                repeated.Sources.SelectMany(source => source.Results).Select(result => result.ResultRef));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChangedBatchModelIdentityRetiresTheOldSourceUntilThePageRegistersAgain(bool changeReference)
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var fixture = new PageFixture(41);
            fixture.Page.ViewResultImages.Add(new ViewResultImage { Id = 7 });
            var reader = new CopilotLoadedBatchResultsReader();
            var before = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
            Assert.Equal(before.SourceRef, fixture.ScopeReference!.SourceRef);
            if (changeReference) fixture.Page.MeasureBatchModel = new() { Id = 41 };
            else fixture.Page.MeasureBatchModel.Id = 42;

            Assert.Throws<CopilotLoadedBatchResultsSourceChangedException>(() => reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult());
            Assert.Null(fixture.ScopeReference);
            Assert.Empty(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
            Assert.Single(fixture.Page.ViewResultImages);
            fixture.Page.ViewResultImages.Clear(); // A successful UI reload supplies its own new rows before re-registration.
            fixture.RegisterAgain();
            var after = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);

            Assert.NotEqual(before.SourceRef, after.SourceRef);
            Assert.Equal(after.SourceRef, fixture.ScopeReference!.SourceRef);
            Assert.Equal(fixture.Page.MeasureBatchModel.Id, after.BatchId);
            Assert.Empty(after.Results);
        });
    }

    [Fact]
    public void AutomaticSnapshotOnlyExposesTheCurrentRegisteredPairAndNeverRebindsChangedBatchRows()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            var previousContext = CopilotLiveContextRegistry.Current;
            var page = PageFixture.CreatePage(45);
            try
            {
                var ensure = typeof(MeasureBatchPage).GetMethod("EnsureCopilotContextRegistered", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var capture = typeof(MeasureBatchPage).GetMethod("CaptureCopilotMeasurementResultSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var row = new ViewResultImage { Id = 7 };
                page.ViewResultImages.Add(row);
                ensure.Invoke(page, null);
                var before = Assert.IsType<CopilotMeasurementResultContextSnapshot>(capture.Invoke(page, null));
                Assert.Matches("^[a-f0-9]{32}$", before.LoadedBatchSourceInstanceId);
                Assert.Matches("^[a-f0-9]{32}$", before.LoadedBatchSourceRef);
                Assert.Equal(CopilotMeasurementResultAgentExtension.SourceId, before.SourceId);

                page.MeasureBatchModel.Id = 46;
                var changed = Assert.IsType<CopilotMeasurementResultContextSnapshot>(capture.Invoke(page, null));

                Assert.Empty(changed.LoadedBatchSourceInstanceId);
                Assert.Empty(changed.LoadedBatchSourceRef);
                Assert.Empty(new CopilotLoadedBatchResultsReader().CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
                Assert.Same(row, Assert.Single(page.ViewResultImages));
                page.ViewResultImages.Clear(); // A successful load supplies fresh data before its existing Ensure boundary.
                ensure.Invoke(page, null);
                var registeredAgain = Assert.IsType<CopilotMeasurementResultContextSnapshot>(capture.Invoke(page, null));
                Assert.Equal(before.LoadedBatchSourceInstanceId, registeredAgain.LoadedBatchSourceInstanceId);
                Assert.NotEqual(before.LoadedBatchSourceRef, registeredAgain.LoadedBatchSourceRef);
                Assert.Equal(registeredAgain.LoadedBatchSourceRef,
                    Assert.Single(new CopilotLoadedBatchResultsReader().CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources).SourceRef);
                page.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                var unloaded = Assert.IsType<CopilotMeasurementResultContextSnapshot>(capture.Invoke(page, null));
                Assert.Empty(unloaded.LoadedBatchSourceInstanceId);
                Assert.Empty(unloaded.LoadedBatchSourceRef);
            }
            finally
            {
                page.Dispose();
                if (previousContext != null) CopilotLiveContextRegistry.Publish(previousContext);
                else CopilotLiveContextRegistry.Clear(CopilotMeasurementResultAgentExtension.SourceId);
            }
        });
    }

    [Fact]
    public void QueuedCaptureKeepsTheFrozenRegistryMembershipInsteadOfSwitchingToAnotherPage()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var first = new PageFixture(51);
            using var second = new PageFixture(52);
            using var queued = new ManualResetEventSlim();
            var reader = new CopilotLoadedBatchResultsReader();
            var pending = Task.Run(async () =>
            {
                Task<CopilotLoadedBatchResultsSnapshot> operation;
                try { operation = reader.CaptureAsync(CancellationToken.None); }
                finally { queued.Set(); }
                return await operation;
            });
            queued.Wait();
            first.Unregister();
            PumpUntilCompleted(pending);

            Assert.Throws<CopilotLoadedBatchResultsSourceChangedException>(() => pending.GetAwaiter().GetResult());
            var remaining = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
            Assert.Equal(52, remaining.BatchId);
        });
    }

    [Fact]
    public void DuplicateRowReferencesAreInvalidWhileDistinctObjectsWithTheSameDatabaseIdRemainReadable()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var fixture = new PageFixture(61);
            var row = new ViewResultImage { Id = 7 };
            fixture.Page.ViewResultImages.Add(row);
            fixture.Page.ViewResultImages.Add(row);
            var reader = new CopilotLoadedBatchResultsReader();

            Assert.Throws<CopilotLoadedBatchResultsSourceInvalidException>(() => reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult());
            fixture.Page.ViewResultImages[1] = new ViewResultImage { Id = 7 };
            var source = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);

            Assert.Equal(2, source.Results.Count);
            Assert.Equal(2, source.Results.Select(result => result.ResultRef).Distinct().Count());
        });
    }

    [Fact]
    public void UnregisteringASourceRetiresItsReferencesWhileThePageAndRowsRemainAlive()
    {
        WpfTestHost.Invoke(() =>
        {
            using var resources = new PresentationResources();
            using var fixture = new PageFixture(71);
            var row = new ViewResultImage { Id = 7 };
            fixture.Page.ViewResultImages.Add(row);
            var reader = new CopilotLoadedBatchResultsReader();
            var before = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);

            fixture.Unregister();

            Assert.Empty(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
            Assert.False(fixture.Page.IsLoadedBatchResultSourceDisposed);
            Assert.Same(row, Assert.Single(fixture.Page.ViewResultImages));
            fixture.RegisterAgain();
            var after = Assert.Single(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
            Assert.NotEqual(before.SourceRef, after.SourceRef);
            Assert.NotEqual(Assert.Single(before.Results).ResultRef, Assert.Single(after.Results).ResultRef);
            Assert.Same(row, Assert.Single(fixture.Page.ViewResultImages));
        });
    }

    [Fact]
    public void EmptyRegistryAndPreCancellationDoNotCreateAnyResultSource()
    {
        var reader = new CopilotLoadedBatchResultsReader();
        var empty = reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult();
        Assert.Empty(empty.Sources);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => reader.CaptureAsync(cancellation.Token).GetAwaiter().GetResult());
        Assert.Empty(reader.CaptureAsync(CancellationToken.None).GetAwaiter().GetResult().Sources);
    }

    private static void PumpUntilCompleted(Task task)
    {
        if (task.IsCompleted) return;
        var frame = new DispatcherFrame();
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = task.ContinueWith(_ => { dispatcher.BeginInvoke(new Action(() => frame.Continue = false)); },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        Dispatcher.PushFrame(frame);
    }

    private sealed class PageFixture : IDisposable
    {
        private CopilotLoadedBatchResultsReader.Registration? _registration;
        public MeasureBatchPage Page { get; }
        public CopilotLoadedBatchResultsReader.ScopeReference? ScopeReference => _registration?.ScopeReference;
        public PageFixture(int batchId) { Page = CreatePage(batchId); RegisterAgain(); }
        public void Unregister() { _registration?.Dispose(); _registration = null; }
        public void RegisterAgain() { Unregister(); _registration = CopilotLoadedBatchResultsReader.Register(Page); }
        public static MeasureBatchPage CreatePage(int batchId)
        {
            var page = new MeasureBatchPage(new Frame(), new MeasureBatchModel { Id = batchId, Code = "offline-source-fixture" });
            var databaseLoad = typeof(MeasureBatchPage).GetMethod("Page_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
            page.Loaded -= (RoutedEventHandler)databaseLoad.CreateDelegate(typeof(RoutedEventHandler), page);
            return page;
        }
        public void Dispose() { Unregister(); Page.Dispose(); }
    }

    private sealed class PresentationResources : IDisposable
    {
        private readonly List<ResourceDictionary> _dictionaries = [];
        public PresentationResources()
        {
            foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
            {
                var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                _dictionaries.Add(dictionary);
                Application.Current.Resources.MergedDictionaries.Add(dictionary);
            }
        }
        public void Dispose()
        {
            foreach (var dictionary in _dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
        }
    }
}
