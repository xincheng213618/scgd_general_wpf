using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using MQTTnet;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed partial class CameraResultSelectionTests
{
    [Theory]
    [InlineData("current")]
    [InlineData("switch-before-reply")]
    [InlineData("switch-during-read")]
    [InlineData("manual-background")]
    [InlineData("newer-request")]
    [InlineData("closed")]
    public async Task PoiBackgroundCaptureUsesOnlyItsCurrentRequestAndDatabase(string change)
    {
        using var databases = new CameraResultDatabases();
        var transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
        var client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
        var control = new MQTTControl(transport);
        var fixture = WpfTestHost.Invoke(() => new ResultViewFixture(control, readResult: (_, _) => null, endpoint: databases.First));
        var readStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opens = new ConcurrentQueue<string>();
        var config = WpfTestHost.Invoke(() => new PoiConfig());
        var image = fixture.View.ImageView;
        MsgRecord? sent = null;
        var capture = new PoiBackgroundCapture(() => fixture.Device, (camera, _) =>
            sent = camera.DService.GetData([1], new CalibrationParam { Id = -1 }, new ParamBase { Id = -1 }));
        try
        {
            string initialPath = CopyImage("initial.png");
            string replacementPath = CopyImage("replacement.png");
            string manualPath = CopyImage("manual.png");
            await OpenBackgroundAsync(initialPath);
            databases.SetFileUrl(databases.First, fixture.ValidImage);
            databases.SetFileUrl(databases.Second, replacementPath);
            bool pauseRead = change is "switch-during-read" or "manual-background";
            databases.BeforeOpen = name =>
            {
                opens.Enqueue(name);
                readStarted.TrySetResult();
                if (pauseRead) releaseRead.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
            };

            Assert.True(await control.Connect().WaitAsync(Deadline));
            (MsgRecord firstRecord, Task firstCapture) = await StartCaptureAsync();
            if (change == "switch-before-reply")
                WpfTestHost.Invoke(() => databases.SwitchToSecond(fixture.Device));
            if (change == "closed")
                WpfTestHost.Invoke(capture.Cancel);
            (MsgRecord Record, Task Completion)? newer = change == "newer-request" ? await StartCaptureAsync() : null;

            await ReplyCameraCaptureAsync(fixture, client, firstRecord, databases.MasterId);
            Assert.Equal(MsgRecordState.Success, await firstRecord.WorkerCompletion!.WaitAsync(Deadline));
            if (pauseRead)
            {
                await readStarted.Task.WaitAsync(Deadline);
                if (change == "switch-during-read")
                    WpfTestHost.Invoke(() => databases.SwitchToSecond(fixture.Device));
                else
                    await OpenBackgroundAsync(manualPath);
                releaseRead.TrySetResult();
            }
            await firstCapture.WaitAsync(Deadline);

            if (newer is { } latest)
            {
                Assert.Empty(opens);
                WpfTestHost.Invoke(() => Assert.Equal(initialPath, config.BackgroundFilePath));
                await ReplyCameraCaptureAsync(fixture, client, latest.Record, databases.MasterId);
                await latest.Completion.WaitAsync(Deadline);
            }

            string expectedPath = change switch
            {
                "current" or "newer-request" => fixture.ValidImage,
                "manual-background" => manualPath,
                _ => initialPath,
            };
            if (change is "switch-before-reply" or "closed") Assert.Empty(opens);
            else Assert.Equal("A", Assert.Single(opens));

            await WpfTestHost.Invoke(() => image.ImageOpenCompletion).WaitAsync(Deadline);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(expectedPath, config.BackgroundFilePath);
                Assert.Equal(expectedPath, image.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath));
                Assert.Equal(2, Assert.IsAssignableFrom<BitmapSource>(image.ViewBitmapSource).PixelWidth);
            });

            string CopyImage(string name)
            {
                string path = Path.Combine(fixture.DirectoryPath, name);
                File.Copy(fixture.ValidImage, path);
                return path;
            }

            async Task OpenBackgroundAsync(string path)
            {
                await WpfTestHost.Invoke(() =>
                {
                    Assert.True(image.TryOpenImage(path));
                    config.BackgroundFilePath = path;
                    return image.ImageOpenCompletion;
                }).WaitAsync(Deadline);
            }

            async Task<(MsgRecord, Task)> StartCaptureAsync()
            {
                Task completion = WpfTestHost.Invoke(() => capture.CaptureAsync(image, () => config));
                MsgRecord record = Assert.IsType<MsgRecord>(sent);
                string wire = await client.Commands.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
                var request = JObject.Parse(wire);
                Assert.Equal(record.MsgID, request.Value<string>("MsgID"));
                Assert.Equal("GetData", request.Value<string>("EventName"));
                return (record, completion);
            }
        }
        finally
        {
            releaseRead.TrySetResult();
            WpfTestHost.Invoke(capture.Cancel);
            try { await control.DisconnectAsyncClient().WaitAsync(Deadline); }
            finally { WpfTestHost.Invoke(fixture.Dispose); }
        }
    }
}
