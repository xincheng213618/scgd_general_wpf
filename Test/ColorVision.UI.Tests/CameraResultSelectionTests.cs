using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Views;
using ColorVision.Engine.Services.Results;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.Themes;
using ColorVision.UI;
using MQTTnet;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed partial class CameraResultSelectionTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CaptureFailureHistoryDoesNotHoldMqttOrApplyAfterAnOwnershipChange(bool switchDatabase)
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var readStarted = new TaskCompletionSource<(OrderByType Order, int Count)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var feedbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        MySqlConnectedEndpoint? active = null;
        MySqlConnectedEndpoint first = CreateEndpoint(endpoint => ReferenceEquals(Volatile.Read(ref active), endpoint));
        MySqlConnectedEndpoint second = CreateEndpoint(endpoint => ReferenceEquals(Volatile.Read(ref active), endpoint));
        Volatile.Write(ref active, first);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, readHistory: (endpoint, order, count) =>
        {
            Assert.Same(first, endpoint);
            readStarted.TrySetResult((order, count));
            releaseRead.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            return [new() { Id = 99 }];
        }, endpoint: first));
        using var other = new MQTTServiceBase(control) { SendTopic = "test/other/cmd", SubscribeTopic = "test/other/status" };
        try
        {
            Assert.True(await control.Connect());
            var (camera, unrelated) = WpfTestHost.Invoke(() =>
            {
                fixture.SelectResult(1, fixture.ValidImage);
                fixture.View.ViewResults.Add(new() { Id = 2, FileUrl = fixture.ValidImage });
                MsgRecord record = fixture.Device.DService.PublishAsyncClient(new MsgSend { EventName = "GetData" });
                fixture.Device.DService.TrackPanelFailure(record, serviceOnly: true, async (_, _) =>
                {
                    Func<Task> refresh = fixture.View.PrepareCaptureHistoryRefresh(first, () => true);
                    await refresh();
                    feedbackFinished.TrySetResult();
                });
                return (record, other.PublishAsyncClient(new MsgSend { EventName = "GetData" }));
            });

            Task pump = Task.Run(async () =>
            {
                await client.ReplyAsync(fixture.Device.DService.SubscribeTopic, new MsgReturn { MsgID = camera.MsgID,
                    DeviceCode = fixture.Device.Config.Code, EventName = "GetData", Code = -1 });
                await client.ReplyAsync(other.SubscribeTopic, new MsgReturn { MsgID = unrelated.MsgID, EventName = "GetData", Code = 0 });
            });

            var options = await readStarted.Task.WaitAsync(Deadline);
            await pump.WaitAsync(Deadline);
            Assert.Equal(MsgRecordState.Fail, camera.MsgRecordState);
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal((ViewCamera.Config.OrderByType, ViewCamera.Config.Count), options);
                if (switchDatabase)
                    Volatile.Write(ref active, second);
                else
                    fixture.List.SelectedItem = fixture.View.ViewResults.Single(result => result.Id == 2);
            });
            releaseRead.TrySetResult();
            await feedbackFinished.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(new[] { 1, 2 }, fixture.View.ViewResults.Select(result => result.Id));
                Assert.Equal(switchDatabase ? 1 : 2, Assert.IsType<ViewResultImage>(fixture.List.SelectedItem).Id);
            });
        }
        finally
        {
            releaseRead.TrySetResult();
            await control.DisconnectAsyncClient();
            WpfTestHost.Invoke(fixture.Dispose);
        }
    }

    [Fact]
    public async Task PendingResultLookupDoesNotDelayAnUnrelatedMqttReplyAndKeepsFifo()
    {
        IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rowsAdded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var lookupIds = new ConcurrentQueue<int>();
        List<int> inserted = [];
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, readResult: (_, id) =>
        {
            lookupIds.Enqueue(id);
            if (id == 11)
            {
                firstRead.TrySetResult();
                releaseRead.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            }
            return new MeasureResultImgModel { Id = id, FileUrl = string.Empty };
        }));
        using var other = new MQTTServiceBase(control) { SendTopic = "test/other/cmd", SubscribeTopic = "test/other/status" };
        try
        {
            Assert.True(await control.Connect());
            MsgRecord firstCapture = await SendCameraCaptureAsync(fixture, client);
            MsgRecord secondCapture = await SendCameraCaptureAsync(fixture, client);
            MsgRecord unrelated = WpfTestHost.Invoke(() =>
            {
                fixture.View.ViewResults.CollectionChanged += (_, change) =>
                {
                    if (change.NewItems?[0] is ViewResultImage result) inserted.Add(result.Id);
                    if (inserted.Count == 2) rowsAdded.TrySetResult();
                };
                return other.PublishAsyncClient(new MsgSend { EventName = "GetData" });
            });

            Task pump = Task.Run(async () =>
            {
                await ReplyCameraCaptureAsync(fixture, client, firstCapture, 11);
                await ReplyCameraCaptureAsync(fixture, client, secondCapture, 12);
                await client.ReplyAsync(other.SubscribeTopic, new MsgReturn { MsgID = unrelated.MsgID, EventName = "GetData", Code = 0 });
            });

            await firstRead.Task.WaitAsync(Deadline);
            await pump.WaitAsync(Deadline);
            Assert.Equal(MsgRecordState.Success, unrelated.MsgRecordState);
            Assert.Equal(new[] { 11 }, lookupIds);
            releaseRead.TrySetResult();
            await rowsAdded.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() => Assert.Equal(new[] { 11, 12 }, inserted));
        }
        finally
        {
            releaseRead.TrySetResult();
            await control.DisconnectAsyncClient();
            WpfTestHost.Invoke(fixture.Dispose);
        }
    }

    [Fact]
    public async Task DelayedTaggedResultUsesOnlyItsOwnersDatabaseAndCannotApplyAfterASwitch()
    {
        var firstRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var localPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPresented = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queries = new ConcurrentQueue<(MySqlConnectedEndpoint Endpoint, int MasterId)>();
        MySqlConnectedEndpoint? active = null;
        MySqlConnectedEndpoint first = CreateEndpoint(endpoint => ReferenceEquals(Volatile.Read(ref active), endpoint));
        MySqlConnectedEndpoint second = CreateEndpoint(endpoint => ReferenceEquals(Volatile.Read(ref active), endpoint));
        Volatile.Write(ref active, first);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(readResult: (endpoint, id) =>
        {
            queries.Enqueue((endpoint, id));
            if (ReferenceEquals(endpoint, first))
            {
                firstRead.TrySetResult();
                releaseRead.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            }
            return new MeasureResultImgModel { Id = id, Result = ReferenceEquals(endpoint, first) ? "A" : "B" };
        }, endpoint: first));

        try
        {
            WpfTestHost.Invoke(() => fixture.View.ViewResults.CollectionChanged += (_, change) =>
            {
                if (change.NewItems?[0] is not ViewResultImage row) return;
                if (row.Id == 50) localPresented.TrySetResult();
                if (row.ResultMsg == "B") secondPresented.TrySetResult();
            });

            Publish(first);
            await firstRead.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() => Volatile.Write(ref active, second));
            Publish(first);
            // A local attachment is independent of MySQL and gives a deterministic FIFO drain marker.
            ResultMessageBus.Default.Publish(new(ResultRoutes.Camera, ResultKinds.Image, fixture.Device.Config.Code, "GetData",
                string.Empty, string.Empty, 0, new ResultReference(50, 100), new MeasureResultImgModel { Id = 50 }));
            releaseRead.TrySetResult();
            await localPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() => Assert.Equal(50, Assert.Single(fixture.View.ViewResults).Id));
            Assert.Equal((first, 31), Assert.Single(queries));

            WpfTestHost.Invoke(() => { fixture.Device.ResultDatabaseEndpoint = second; });
            Publish(first);
            Publish(second);
            await secondPresented.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() => Assert.Equal("B", fixture.View.ViewResults.Single(row => row.Id == 31).ResultMsg));
            Assert.Equal(new[] { (first, 31), (second, 31) }, queries);
        }
        finally
        {
            releaseRead.TrySetResult();
            WpfTestHost.Invoke(fixture.Dispose);
        }

        void Publish(MySqlConnectedEndpoint endpoint) => ResultMessageBus.Default.PublishPersisted(ResultRoutes.Camera, ResultKinds.Image,
            fixture.Device.Config.Code, "GetData", string.Empty, string.Empty, 0, 31, 100, databaseEndpoint: endpoint);
    }

    [Fact]
    public async Task TaggedCameraAttachmentUsesNoQueryAndAnOldTagCannotEnterTheNewHistory()
    {
        MySqlConnectedEndpoint? active = null;
        MySqlConnectedEndpoint first = CreateEndpoint(endpoint => ReferenceEquals(active, endpoint));
        MySqlConnectedEndpoint second = CreateEndpoint(endpoint => ReferenceEquals(active, endpoint));
        active = second;
        var added = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        var fixture = WpfTestHost.Invoke(() =>
        {
            var scope = new ResultViewFixture(readResult: (_, _) =>
            {
                Interlocked.Increment(ref lookups);
                return null;
            }, endpoint: second);
            scope.View.ViewResults.CollectionChanged += (_, change) =>
            {
                if (change.NewItems?[0] is ViewResultImage { ResultMsg: "current" }) added.TrySetResult();
            };
            return scope;
        });
        try
        {
            Publish(first, "stale");
            Publish(second, "current");
            await added.Task.WaitAsync(Deadline);
            WpfTestHost.Invoke(() => Assert.Equal("current", Assert.Single(fixture.View.ViewResults).ResultMsg));
            Assert.Equal(0, lookups);
        }
        finally { WpfTestHost.Invoke(fixture.Dispose); }

        void Publish(MySqlConnectedEndpoint endpoint, string result) => ResultMessageBus.Default.Publish(new ResultMessage(
            ResultRoutes.Camera, ResultKinds.Image, fixture.Device.Config.Code, "GetData", string.Empty, string.Empty, 0,
            new ResultReference(41, 100), new MeasureResultImgModel { Id = 41, Result = result }, databaseEndpoint: endpoint));
    }

    [Fact]
    public void CameraTimeoutBaselineDoesNotLookForTheSameIdInAnotherDatabase()
    {
        const string cameraCode = "SYNTHETIC.CAMERA";
        MySqlConnectedEndpoint? active = null;
        int firstRows = 1;
        int firstQueries = 0;
        int secondQueries = 0;
        var first = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(active, endpoint), () =>
        {
            firstQueries++;
            return CreateResultDatabase(cameraCode, firstRows, "A");
        });
        var second = new MySqlConnectedEndpoint(endpoint => ReferenceEquals(active, endpoint), () =>
        {
            secondQueries++;
            return CreateResultDatabase(cameraCode, 2, "B");
        });
        active = first;
        int baseline = MeasureImgResultDao.Instance.GetLatestId(first, cameraCode);
        Assert.Equal(1, baseline);
        firstRows = 2;
        Assert.Equal("A-2", MeasureImgResultDao.Instance.GetLatestAfterId(first, cameraCode, baseline)?.Result);

        active = second;
        Assert.Null(MeasureImgResultDao.Instance.GetLatestAfterId(first, cameraCode, baseline));
        Assert.Equal((2, 0), (firstQueries, secondQueries));
        Assert.Equal("B-2", MeasureImgResultDao.Instance.GetLatestAfterId(second, cameraCode, baseline)?.Result);
    }

    private static MySqlConnectedEndpoint CreateEndpoint(Func<MySqlConnectedEndpoint, bool> isCurrent) =>
        new(isCurrent, () => throw new InvalidOperationException("The result reader must use the offline test double."));

    private static SqlSugarClient CreateResultDatabase(string cameraCode, int rows, string label)
    {
        var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite,
            IsAutoCloseConnection = false, InitKeyType = InitKeyType.Attribute });
        db.CodeFirst.InitTables<MeasureResultImgModel>();
        for (int number = 1; number <= rows; number++)
            db.Insertable(new MeasureResultImgModel { DeviceCode = cameraCode, Result = $"{label}-{number}" }).ExecuteReturnIdentity();
        return db;
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("corrupt")]
    [InlineData("unsupported")]
    [InlineData("no-path")]
    public async Task UnreadableHistoryClearsThePreviousCaptureAndReportsTheSelectedRow(string failure)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using var fixture = new ResultViewFixture();
            string badPath = failure switch
            {
                "missing" => Path.Combine(fixture.DirectoryPath, "missing.png"),
                "corrupt" => fixture.WriteUnsupportedData("corrupt.png"),
                "unsupported" => fixture.WriteUnsupportedData("capture.cv-unsupported-test"),
                _ => string.Empty,
            };
            fixture.SelectResult(1, fixture.ValidImage);
            await fixture.View.ImageView.ImageOpenCompletion;
            ImageSource prior = Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource);

            fixture.SelectResult(2, badPath);
            if (failure is "missing" or "corrupt")
                await Assert.ThrowsAnyAsync<Exception>(() => fixture.View.ImageView.ImageOpenCompletion);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);

            Assert.Null(fixture.View.ImageView.ImageShow.Source);
            Assert.Null(fixture.View.ImageView.ViewBitmapSource);
            Assert.Null(fixture.View.ImageView.EditorContext.IImageOpen);
            Assert.Equal(Visibility.Visible, fixture.Error.Visibility);
            Assert.Contains("ID 2", fixture.Error.Text);

            fixture.SelectResult(3, fixture.ValidImage);
            await fixture.View.ImageView.ImageOpenCompletion;
            Assert.NotSame(prior, Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource));
            Assert.Equal(Visibility.Collapsed, fixture.Error.Visibility);
        });
    }

    [Theory]
    [InlineData("new-row")]
    [InlineData("local-preview")]
    [InlineData("video")]
    public async Task LateHistoryFailureCannotClearNewerPixels(string owner)
    {
        await WpfTestHost.Invoke(async () =>
        {
            using var fixture = new ResultViewFixture();
            var bufferedOpen = new PendingCachedImageOpen();
            fixture.View.ImageView.IEditorToolFactory.IImageOpens[".cv-camera-history-test"] = bufferedOpen;
            string cachedOnly = Path.Combine(fixture.DirectoryPath, "cache.cv-camera-history-test");
            fixture.SelectResult(1, fixture.ValidImage);
            await fixture.View.ImageView.ImageOpenCompletion;

            fixture.SelectResult(2, cachedOnly);
            Task pending = fixture.View.ImageView.ImageOpenCompletion;
            Assert.False(pending.IsCompleted);
            if (owner == "new-row")
            {
                fixture.SelectResult(3, fixture.ValidImage);
                await fixture.View.ImageView.ImageOpenCompletion;
            }
            else
            {
                var preview = new WriteableBitmap(3, 2, 96, 96, PixelFormats.Gray8, null);
                if (owner == "local-preview")
                {
                    fixture.View.ShowLocalResult(null, new LocalCameraPreview
                    {
                        Bitmap = preview, CieData = [], Exposure = [], Metadata = new(), Parameters = new(),
                    }, forceDisplay: true, focusFrame: false);
                }
                else
                {
                    fixture.Device.DisplayConfig.IsLocalVideoOpen = true;
                    fixture.View.ImageView.SetImageSource(preview);
                }
            }

            ImageSource newer = Assert.IsAssignableFrom<BitmapSource>(fixture.View.ImageView.ViewBitmapSource);
            bufferedOpen.Fail();
            await Assert.ThrowsAsync<InvalidDataException>(() => pending);
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            Assert.Same(newer, fixture.View.ImageView.ViewBitmapSource);
            Assert.Equal(Visibility.Collapsed, fixture.Error.Visibility);
        });
    }

    private sealed class PendingCachedImageOpen : IAsyncImageOpen
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public long? GetCachedFileLength(string filePath) => 1;

        public void OpenImage(EditorContext context, string? filePath) => throw new NotSupportedException();

        public Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
            => _completion.Task;

        internal void Fail() => _completion.SetException(new InvalidDataException("Synthetic camera decode failure"));
    }

    private sealed class ResultViewFixture : IDisposable
    {
        private readonly IConfigService _previousConfig = ConfigService.Instance;
        private readonly List<ResourceDictionary> _dictionaries = [];
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CV-camera-result-selection-");

        internal DeviceCamera Device { get; }
        internal ViewCamera View { get; }
        internal ListView List { get; }
        internal TextBlock Error { get; }
        internal string DirectoryPath => _directory.FullName;
        internal string ValidImage { get; }

        internal ResultViewFixture(MQTTControl? mqtt = null, Func<MySqlConnectedEndpoint, int, MeasureResultImgModel?>? readResult = null,
            Func<MySqlConnectedEndpoint, OrderByType, int, List<MeasureResultImgModel>>? readHistory = null,
            MySqlConnectedEndpoint? endpoint = null)
        {
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    _dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }

                ValidImage = Path.Combine(DirectoryPath, "valid.png");
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null, new byte[4], 2)));
                using (FileStream file = File.Create(ValidImage))
                    encoder.Save(file);

                Device = new DeviceCamera(new SysResourceModel { Id = -2, Code = FixtureCameraCode, Value = "{}" });
                Device.ViewRegistration.IsVisible = true;
                Device.ResultDatabaseEndpoint = endpoint ?? (readResult != null || readHistory != null ? CreateEndpoint(_ => true) : null);
                if (mqtt != null)
                {
                    Device.CameraBackend.SetPreference(false);
                    Device.Config.SendTopic = "test/camera/cmd";
                    Device.Config.SubscribeTopic = "test/camera/status";
                    Device.DService.Dispose();
                    Device.DService = new MQTTCamera(Device, mqtt, (_, _) => { });
                }
                View = new ViewCamera(Device, deferInitialization: true, readResult, readHistory);
                View.EnsureInitialized();
                List = (ListView)View.FindName("listView1");
                Error = (TextBlock)View.FindName("HistoryImageError");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal void SelectResult(int id, string path)
        {
            var result = new ViewResultImage { Id = id, FileUrl = path };
            View.ViewResults.Add(result);
            List.SelectedItem = result;
        }

        internal string WriteUnsupportedData(string name)
        {
            string path = Path.Combine(DirectoryPath, name);
            File.WriteAllText(path, "This file has no readable pixels.");
            return path;
        }

        public void Dispose()
        {
            if (Device != null)
                Device.DisplayConfig.IsLocalVideoOpen = false;
            View?.Dispose();
            Device?.Dispose();
            foreach (ResourceDictionary dictionary in _dictionaries)
                Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            TestEnvironment.SetConfig(_previousConfig);
            foreach (FileInfo file in _directory.EnumerateFiles())
                file.Delete();
            _directory.Delete();
        }
    }
}
