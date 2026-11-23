using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Results;
using ColorVision.Engine.Templates;
using ColorVision.UI.Views;
using cvColorVision;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class LocalCameraInteractiveCommandTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueuedGetDataUsesTheCameraDatabaseAtIngressAndReportsOnlyCommittedResults(bool rejectImage)
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-camera-get-data-");
        try
        {
            MySqlConnectedEndpoint? active = null;
            MySqlConnectedEndpoint Endpoint(string databaseFile) => new(candidate => ReferenceEquals(active, candidate),
                () => new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={Path.Combine(directory.FullName, databaseFile)};Pooling=False",
                    DbType = DbType.Sqlite,
                    InitKeyType = InitKeyType.Attribute,
                    IsAutoCloseConnection = true,
                }));
            MySqlConnectedEndpoint first = Endpoint("a.db"), second = Endpoint("b.db");
            active = first;
            using var firstDb = first.CreateDbClient();
            using var secondDb = second.CreateDbClient();
            firstDb.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel));
            secondDb.CodeFirst.InitTables(typeof(MeasureBatchModel), typeof(MeasureResultImgModel));
            int otherBatchId = secondDb.Insertable(new MeasureBatchModel { Code = "other-batch", Name = "Other sample" }).ExecuteReturnIdentity();
            int otherImageId = secondDb.Insertable(new MeasureResultImgModel
            {
                BatchId = otherBatchId, FileUrl = "other-photo.tif", DeviceCode = "other-camera",
            }).ExecuteReturnIdentity();
            if (rejectImage)
                firstDb.Ado.ExecuteCommand("CREATE TRIGGER reject_image BEFORE INSERT ON t_scgd_measure_result_img BEGIN SELECT RAISE(ABORT, 'synthetic image failure'); END");

            var config = new ConfigCamera { Code = $"interactive-camera-{Guid.NewGuid():N}" };
            var backend = new CameraBackendState(true);
            using var session = new LocalCameraSession(new LocalCameraSessionTests.FakeNative(), backend, config);
            session.Open("camera", TakeImageMode.Measure_Normal, 16);
            var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            device.Config = config;
            device.SysResourceModel = new SysResourceModel { Code = config.Code };
            device.ResultDatabaseEndpoint = first;
            SetDeviceField(device, "<CameraBackend>k__BackingField", backend);
            SetDeviceField(device, "<LocalCameraSession>k__BackingField", session);
            using var hiddenView = WpfTestHost.Invoke(() => new DockViewRegistration(
                () => throw new InvalidOperationException("The hidden camera view must not be created."), "offline camera", isVisible: false));
            SetDeviceField(device, "_view", hiddenView);

            var completion = new TaskCompletionSource<MsgRecord>(TaskCreationOptions.RunContinuationsAsynchronously);
            var notifications = new ConcurrentQueue<ResultMessage>();
            using var subscription = ResultMessageBus.Default.Subscribe(message =>
            {
                if (message.EventName == "GetData" && message.DeviceCode == config.Code)
                    notifications.Enqueue(message);
            });
            LocalFlowFrame? frame = null;
            WpfTestHost.Invoke(() =>
            {
                MsgRecord record = device.CaptureLocally([1], new CalibrationParam { Id = -1 }, new ParamBase { Id = -1 }, _ =>
                {
                    frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
                    {
                        Width = 1, Height = 1, SourceBpp = 8, Channels = 1, DeviceCode = config.Code,
                        PrimaryBufferKind = LocalFrameBufferKind.CvRaw,
                    }, 1, 0);
                    return new LocalCameraCaptureResult { Frame = frame, TotalTimeMs = 7 };
                });
                record.MsgRecordStateChanged += (_, state) =>
                {
                    if (state is MsgRecordState.Success or MsgRecordState.Fail)
                        completion.TrySetResult(record);
                };

                // RunLocalCommand's dispatcher callback cannot start until this action exits.
                active = second;
                device.ResultDatabaseEndpoint = second;
            });

            MsgRecord result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal("GetData", result.MsgReturn.EventName);
            Assert.False(first.IsCurrent);
            Assert.NotNull(frame);
            Assert.Throws<ObjectDisposedException>(() => frame.Acquire());
            if (rejectImage)
            {
                Assert.Equal(MsgRecordState.Fail, result.MsgRecordState);
                Assert.Equal(-1, result.MsgReturn.Code);
                Assert.Contains("已采集图像仍可预览", result.MsgReturn.Message);
                Assert.Equal(-1, frame.MasterId);
                Assert.Empty(firstDb.Queryable<MeasureBatchModel>().ToList());
                Assert.Empty(firstDb.Queryable<MeasureResultImgModel>().ToList());
                Assert.Empty(notifications);
            }
            else
            {
                Assert.Equal(MsgRecordState.Success, result.MsgRecordState);
                Assert.Equal(0, result.MsgReturn.Code);
                var recordedBatch = firstDb.Queryable<MeasureBatchModel>().Single();
                var recordedImage = firstDb.Queryable<MeasureResultImgModel>().Single();
                var payload = JObject.FromObject((object)result.MsgReturn.Data);
                Assert.Equal((otherBatchId, ArchiveStatus.NotArchived), (recordedBatch.Id, recordedBatch.ArchiveStatus));
                Assert.Equal((otherImageId, recordedBatch.Id, config.Code), (recordedImage.Id, recordedImage.BatchId, recordedImage.DeviceCode));
                Assert.Equal(otherImageId, frame.MasterId);
                Assert.Equal(otherImageId, (int?)payload["MasterId"]);
                var notice = Assert.Single(notifications);
                Assert.Same(first, notice.DatabaseEndpoint);
                Assert.Equal((recordedBatch.Code, otherImageId), (notice.SerialNumber, notice.Data.MasterId));
            }
            Assert.Equal("other-batch", secondDb.Queryable<MeasureBatchModel>().Single().Code);
            Assert.Equal("other-photo.tif", secondDb.Queryable<MeasureResultImgModel>().Single().FileUrl);
        }
        finally
        {
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }

    private static void SetDeviceField(DeviceCamera device, string fieldName, object value) =>
        typeof(DeviceCamera).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(device, value);
}
