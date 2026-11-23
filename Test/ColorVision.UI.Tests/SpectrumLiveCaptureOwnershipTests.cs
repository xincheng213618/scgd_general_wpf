using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Correction;
using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using MQTTnet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SqlSugar;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class SpectrumLiveCaptureOwnershipTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(10);
    private const int CorrectionPointCount = 4001;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedSpectrumResultUsesTheMatchedGetDataReplyAndOriginalRow(bool luminousFluxMode)
    {
        var displayed = new TaskCompletionSource<ViewResultSpectrum>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var scope = new CaptureScope(luminousFluxMode: luminousFluxMode, readMeasurements: true,
            present: result => displayed.TrySetResult(result));
        await scope.ConnectAsync();

        var capture = scope.StartOwnedResult();
        string command = await scope.ExpectCommandAsync(capture);
        await scope.ReplyAsync(command, code: 102).WaitAsync(Deadline);
        await scope.ReplyAsync(command, deviceCode: "other-spectrum").WaitAsync(Deadline);
        await scope.ReplyAsync("unmatched-measurement").WaitAsync(Deadline);
        Assert.False(capture.IsCompleted);

        await scope.ReplyAsync(command).WaitAsync(Deadline);
        var (succeeded, result) = await capture.WaitAsync(Deadline);
        Assert.True(succeeded);
        var row = Assert.IsType<SpectumResultEntity>(result);
        Assert.Equal(scope.ResultId, row.Id);
        Assert.Equal(scope.Device.Code, row.DeviceCode);
        Assert.Equal(73f, row.fPh);
        Assert.Equal(luminousFluxMode, row.DataType);
        Assert.Equal(luminousFluxMode ? 146f : (float?)null, row.LuminousFlux);
        var view = await displayed.Task.WaitAsync(Deadline);
        Assert.Equal(row.Id, view.Id);
        Assert.Equal(row.fPh, view.fPh);
        Assert.Equal(luminousFluxMode ? 2 : 1, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
        Assert.False(scope.HasExtraCommand);
    }

    [Fact]
    public async Task OwnedSpectrumResultIgnoresAnOldReplyWithTheSameIdAfterDatabaseSwitch()
    {
        await using var scope = new CaptureScope(readMeasurements: true);
        await scope.ConnectAsync();

        var original = scope.StartOwnedResult();
        string originalCommand = await scope.ExpectCommandAsync(original);
        scope.Reconnect();
        await scope.ReplyAsync(originalCommand).WaitAsync(Deadline);

        var (originalSucceeded, originalResult) = await original.WaitAsync(Deadline);
        Assert.False(originalSucceeded);
        Assert.Null(originalResult);
        Assert.Equal(0, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);

        var current = scope.StartOwnedResult();
        string currentCommand = await scope.ExpectCommandAsync(current);
        await scope.ReplyAsync(originalCommand).WaitAsync(Deadline);
        Assert.False(current.IsCompleted);
        await scope.ReplyAsync(currentCommand).WaitAsync(Deadline);

        var (currentSucceeded, currentResult) = await current.WaitAsync(Deadline);
        Assert.True(currentSucceeded);
        var row = Assert.IsType<SpectumResultEntity>(currentResult);
        Assert.Equal(scope.ResultId, row.Id);
        Assert.Equal(279f, row.fPh);
        Assert.Equal(0, scope.OriginalOpens);
        Assert.Equal(1, scope.ReplacementOpens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedSpectrumResultRejectsRetirementDuringTheOriginalRead(bool replaceOwner)
    {
        await using var scope = new CaptureScope(readMeasurements: true);
        await scope.ConnectAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.OnOpenOriginal = () =>
        {
            started.TrySetResult();
            release.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
        };

        var capture = scope.StartOwnedResult();
        string command = await scope.ExpectCommandAsync(capture);
        Task reply = Task.Run(() => scope.ReplyAsync(command));
        try
        {
            await started.Task.WaitAsync(Deadline);
            if (replaceOwner) scope.TransferTransportToReplacementOwner();
            else scope.Reconnect();
        }
        finally { release.TrySetResult(); }

        await reply.WaitAsync(Deadline);
        var (succeeded, result) = await capture.WaitAsync(Deadline);
        Assert.False(succeeded);
        Assert.Null(result);
        Assert.Equal(1, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedSpectrumResultPreservesASuccessfulAcknowledgementWithoutAStoredMeasurement(bool includesMissingId)
    {
        await using var scope = new CaptureScope(readMeasurements: true);
        await scope.ConnectAsync();

        var capture = scope.StartOwnedResult();
        await scope.ReplyAsync(await scope.ExpectCommandAsync(capture), includesResult: includesMissingId,
            resultId: int.MaxValue).WaitAsync(Deadline);

        var (succeeded, result) = await capture.WaitAsync(Deadline);
        Assert.True(succeeded);
        Assert.Null(result);
        Assert.Equal(includesMissingId ? 1 : 0, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
    }

    [Fact]
    public async Task OwnedSpectrumResultRejectsARowExplicitlyOwnedByAnotherDevice()
    {
        await using var scope = new CaptureScope(readMeasurements: true);
        scope.SetOriginalResultDeviceCode("other-spectrum");
        await scope.ConnectAsync();

        var capture = scope.StartOwnedResult();
        await scope.ReplyAsync(await scope.ExpectCommandAsync(capture)).WaitAsync(Deadline);

        var (succeeded, result) = await capture.WaitAsync(Deadline);
        Assert.False(succeeded);
        Assert.Null(result);
        Assert.Equal(1, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
    }

    [Fact]
    public async Task IdleCapturePublishesOneRequestUsesTheDeviceDatabaseAndReleasesTheGate()
    {
        await using var scope = new CaptureScope();
        await scope.ConnectAsync();

        for (int expectedOpenCount = 1; expectedOpenCount <= 2; expectedOpenCount++)
        {
            Task<SpectrumColorMeasurement> capture = scope.Start();
            await scope.ReplyAsync(await scope.ExpectCommandAsync(capture)).WaitAsync(Deadline);
            var measurement = await capture.WaitAsync(Deadline);
            Assert.Equal(scope.ResultId, measurement.ResultId);
            Assert.Equal(scope.Device.Code, measurement.DeviceCode);
            Assert.Equal(73d, measurement.Y);
            Assert.Equal(new[] { new SpectrumValuePoint(500, 1.5), new SpectrumValuePoint(501, 2.5) }, measurement.Spectrum);
            Assert.Equal(expectedOpenCount, scope.OriginalOpens);
            Assert.Equal(0, scope.ReplacementOpens);
            Assert.False(scope.HasExtraCommand);
        }
    }

    [Fact]
    public async Task ReplyCannotReadTheReplacementDatabaseAfterTheOriginalIsRetired()
    {
        await using var scope = new CaptureScope();
        await scope.ConnectAsync();

        Task<SpectrumColorMeasurement> capture = scope.Start();
        string command = await scope.ExpectCommandAsync(capture);
        scope.Reconnect();
        await scope.ReplyAsync(command).WaitAsync(Deadline);
        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.WaitAsync(Deadline));
        Assert.Equal(0, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);

        Task<SpectrumColorMeasurement> repeat = scope.Start();
        await scope.ReplyAsync(await scope.ExpectCommandAsync(repeat)).WaitAsync(Deadline);
        Assert.Equal(279d, (await repeat.WaitAsync(Deadline)).Y);
        Assert.Equal(0, scope.OriginalOpens);
        Assert.Equal(1, scope.ReplacementOpens);
    }

    [Fact]
    public async Task ResultFromLookupInProgressIsRejectedWhenTheTransportChangesOwner()
    {
        await using var scope = new CaptureScope();
        await scope.ConnectAsync();
        var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        scope.OnOpenOriginal = () =>
        {
            opened.TrySetResult();
            release.Task.WaitAsync(Deadline).GetAwaiter().GetResult();
        };

        Task<SpectrumColorMeasurement> capture = scope.Start();
        string command = await scope.ExpectCommandAsync(capture);
        Task reply = Task.Run(() => scope.ReplyAsync(command));
        try
        {
            await opened.Task.WaitAsync(Deadline);
            scope.TransferTransportToReplacementOwner();
        }
        finally { release.TrySetResult(); }

        await reply.WaitAsync(Deadline);
        await Assert.ThrowsAsync<InvalidOperationException>(() => capture.WaitAsync(Deadline));
        Assert.Equal(1, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
    }

    [Fact]
    public async Task CancelledWaitKeepsTheMeasurementGateUntilItsCommandSettles()
    {
        await using var scope = new CaptureScope();
        await scope.ConnectAsync();
        using var cancelled = new CancellationTokenSource();

        Task<SpectrumColorMeasurement> capture = scope.Start(cancelled.Token);
        string oldCommand = await scope.ExpectCommandAsync(capture);
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture.WaitAsync(Deadline));
        Task<SpectrumColorMeasurement> overlapping = scope.Start();
        await Assert.ThrowsAsync<InvalidOperationException>(() => overlapping.WaitAsync(Deadline));
        Assert.False(scope.HasExtraCommand);
        await scope.ReplyAsync(oldCommand).WaitAsync(Deadline);
        Assert.Equal(0, scope.OriginalOpens);

        Task<SpectrumColorMeasurement> repeat = scope.Start();
        await scope.ReplyAsync(await scope.ExpectCommandAsync(repeat)).WaitAsync(Deadline);
        Assert.Equal(73d, (await repeat.WaitAsync(Deadline)).Y);
        Assert.Equal(1, scope.OriginalOpens);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemoteMagnitudeCaptureRequiresTheSameCalibrationFileAtReply(bool replaceMagnitudeFile)
    {
        await using var scope = new CaptureScope(fullCorrectionSpectrum: true);
        await scope.ConnectAsync();
        string magnitudePath = scope.Device.Config.MaguideFile;
        string originalHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(magnitudePath)));
        Assert.Null(DeviceSpectrum.ValidateCorrectionMagnitudeFile(magnitudePath));

        Task<SpectrumMeasurementSnapshot> capture = scope.StartMagnitudeCorrection();
        string command = await scope.ExpectCommandAsync(capture);
        if (replaceMagnitudeFile)
        {
            CaptureScope.WriteMagnitude(magnitudePath, CorrectionPointCount, coefficient: 2);
            Assert.Null(DeviceSpectrum.ValidateCorrectionMagnitudeFile(magnitudePath));
            Assert.NotEqual(originalHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(magnitudePath))));
        }
        await scope.ReplyAsync(command).WaitAsync(Deadline);

        if (replaceMagnitudeFile)
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => capture.WaitAsync(Deadline));
            Assert.Contains("标定组或标定文件", error.Message);
        }
        else
        {
            SpectrumMeasurementSnapshot snapshot = await capture.WaitAsync(Deadline);
            Assert.Equal(scope.ResultId, snapshot.ResultId);
            Assert.Equal(scope.Device.Code, snapshot.DeviceCode);
            Assert.Equal((380d, 780d, 0.1d), (snapshot.StartWavelength, snapshot.EndWavelength, snapshot.Interval));
            Assert.Equal(CorrectionPointCount, snapshot.RelativeSpectrum.Count);
            Assert.Equal((2d, 73d), (snapshot.AbsoluteScale, snapshot.PhotometricValue));
            Assert.Equal(magnitudePath, snapshot.MagnitudeFilePath);
            Assert.Equal(originalHash, snapshot.MagnitudeFileSha256);
        }
        Assert.Equal(1, scope.OriginalOpens);
        Assert.Equal(0, scope.ReplacementOpens);
        Assert.False(scope.HasExtraCommand);
    }

    private sealed class CaptureScope : IAsyncDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("cv-spectrum-live-capture-");
        private readonly SpectrumNdSelectionTests.CalibrationMqttClient client;
        private readonly MQTTControl mqtt;
        private readonly MQTTSpectrum service;
        private readonly DeviceSpectrum replacementOwner;
        private readonly string measurementEvent;
        private readonly MySqlConnectedEndpoint original;
        private readonly MySqlConnectedEndpoint replacement;
        private MySqlConnectedEndpoint active;
        private int originalOpens;
        private int replacementOpens;

        internal DeviceSpectrum Device { get; }
        internal int ResultId { get; }
        internal int OriginalOpens => Volatile.Read(ref originalOpens);
        internal int ReplacementOpens => Volatile.Read(ref replacementOpens);
        internal Action? OnOpenOriginal { get; set; }
        internal bool HasExtraCommand => client.Commands.Reader.TryPeek(out _);

        internal CaptureScope(bool fullCorrectionSpectrum = false, bool luminousFluxMode = false,
            bool readMeasurements = false, Action<ViewResultSpectrum>? present = null)
        {
            string code = $"SPECTRUM.LIVE.{Guid.NewGuid():N}";
            measurementEvent = luminousFluxMode ? "EQE.GetData" : "GetData";
            ResultId = Seed("original.db", code, 73, fullCorrectionSpectrum, luminousFluxMode);
            Assert.Equal(ResultId, Seed("replacement.db", code, 279, fullCorrectionSpectrum, luminousFluxMode));
            original = new(candidate => ReferenceEquals(candidate, Volatile.Read(ref active)), () =>
            {
                Interlocked.Increment(ref originalOpens);
                OnOpenOriginal?.Invoke();
                return Open("original.db");
            });
            replacement = new(candidate => ReferenceEquals(candidate, Volatile.Read(ref active)), () =>
            {
                Interlocked.Increment(ref replacementOpens);
                return Open("replacement.db");
            });
            active = original;

            IMqttClient transport = DispatchProxy.Create<IMqttClient, SpectrumNdSelectionTests.CalibrationMqttClient>();
            client = (SpectrumNdSelectionTests.CalibrationMqttClient)(object)transport;
            mqtt = new MQTTControl(transport);
            (Device, service, replacementOwner) = WpfTestHost.Invoke(() =>
            {
                var config = new ConfigSpectrum
                {
                    Code = code, SN = code, SendTopic = "test/live-spectrum/cmd", SubscribeTopic = "test/live-spectrum/status",
                };
                WriteCalibration(config, fullCorrectionSpectrum);
                var owner = CreateDevice(config, original);
                var spectrumTransport = new MQTTSpectrum(owner, mqtt,
                    (endpoint, id) => readMeasurements ? ReadMeasurement(endpoint, id) : null,
                    () => new Version(), _ => present);
                owner.DService = spectrumTransport;
                spectrumTransport.DeviceStatus = DeviceStatusType.Opened;
                if (luminousFluxMode)
                    owner.DisplayConfig.IsLuminousFluxMode = true;
                var other = CreateDevice(config, original);
                other.DService = spectrumTransport;
                return (owner, spectrumTransport, other);
            });
        }

        internal async Task ConnectAsync() => Assert.True(await mqtt.Connect().WaitAsync(Deadline));

        internal Task<SpectrumColorMeasurement> Start(CancellationToken cancellationToken = default) =>
            WpfTestHost.Invoke(() => Device.CaptureColorMeasurementAsync(cancellationToken));

        internal Task<(bool Succeeded, SpectumResultEntity? Result)> StartOwnedResult() =>
            WpfTestHost.Invoke(() => Device.DService.GetDataResultAsync());

        internal Task<SpectrumMeasurementSnapshot> StartMagnitudeCorrection() => WpfTestHost.Invoke(() =>
        {
            var capture = typeof(DeviceSpectrum).GetMethod("CaptureCorrectionMeasurementAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<CancellationToken, Task<SpectrumMeasurementSnapshot>>>(Device);
            return Device.CreateSpectrumCorrectionHost(capture).CaptureAsync(CancellationToken.None);
        });

        internal async Task<string> ExpectCommandAsync(Task capture)
        {
            Task<string> received = client.Commands.Reader.ReadAsync().AsTask();
            if (ReferenceEquals(await Task.WhenAny(received, capture).WaitAsync(Deadline), capture))
            {
                await capture;
                throw new InvalidOperationException("The capture finished before publishing its MQTT request.");
            }
            JObject payload = JObject.Parse(await received);
            Assert.Equal(measurementEvent, payload.Value<string>("EventName"));
            Assert.Equal(Device.Code, payload.Value<string>("DeviceCode"));
            return Assert.IsType<string>(payload.Value<string>("MsgID"));
        }

        internal Task ReplyAsync(string id, bool includesResult = true, int code = 0, string? deviceCode = null, int? resultId = null) => client.ReplyAsync(service.SubscribeTopic, new MsgReturn
        {
            MsgID = id, DeviceCode = deviceCode ?? Device.Code, EventName = measurementEvent, Code = code,
            Data = includesResult ? new JObject { ["MasterId"] = resultId ?? ResultId } : new JObject(),
        });

        internal void SetOriginalResultDeviceCode(string code)
        {
            using var db = Open("original.db");
            Assert.Equal(1, db.Updateable<SpectumResultEntity>().SetColumns(row => row.DeviceCode == code)
                .Where(row => row.Id == ResultId).ExecuteCommand());
        }

        internal void Reconnect() => WpfTestHost.Invoke(() =>
        {
            Volatile.Write(ref active, replacement);
            Device.ResultDatabaseEndpoint = replacement;
        });

        // This is a plain runtime owner reference. Set it while the original endpoint is blocked, even
        // if its database setup is on the dispatcher, and release the pending lookup immediately after.
        internal void TransferTransportToReplacementOwner() => service.Device = replacementOwner;

        private static DeviceSpectrum CreateDevice(ConfigSpectrum config, MySqlConnectedEndpoint endpoint)
        {
            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            device.SysResourceModel = new SysResourceModel { Id = 123, Code = config.Code };
            device.Config = config;
            device.ResultDatabaseEndpoint = endpoint;
            typeof(DeviceSpectrum).GetField("<SpectrumBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(device, new SpectrumBackendState(false));
            typeof(DeviceSpectrum).GetField("correctionMeasurementGate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(device, new SemaphoreSlim(1, 1));
            return device;
        }

        private int Seed(string path, string deviceCode, float luminance, bool fullCorrectionSpectrum, bool luminousFluxMode)
        {
            using var db = Open(path);
            db.CodeFirst.InitTables<SpectumResultEntity>();
            if (luminousFluxMode)
                db.CodeFirst.InitTables<SMUResultModel>();
            return db.Insertable(new SpectumResultEntity
            {
                DeviceCode = deviceCode, fPh = luminance, fx = .3f, fy = .4f,
                DataType = luminousFluxMode, LuminousFlux = luminousFluxMode ? luminance * 2 : null,
                fSpect1 = fullCorrectionSpectrum ? 380 : 500, fSpect2 = fullCorrectionSpectrum ? 780 : 501,
                fInterval = fullCorrectionSpectrum ? .1f : 1f, fPlambda = 2,
                fPL = fullCorrectionSpectrum ? JsonConvert.SerializeObject(Enumerable.Repeat(1.5, CorrectionPointCount)) : "[1.5,2.5]",
            }).ExecuteReturnIdentity();
        }

        private SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            DbType = DbType.Sqlite, ConnectionString = $"Data Source={Path.Combine(directory.FullName, name)};Pooling=False",
            IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
        });

        private static SpectumResultEntity? ReadMeasurement(MySqlConnectedEndpoint endpoint, int id)
        {
            using var db = endpoint.CreateDbClient();
            return MQTTSpectrum.ReadServiceResult(db, id);
        }

        private void WriteCalibration(ConfigSpectrum config, bool fullCorrectionSpectrum)
        {
            config.WavelengthFile = Path.Combine(directory.FullName, "wavelength.dat");
            config.MaguideFile = Path.Combine(directory.FullName, "magnitude.dat");
            int count = fullCorrectionSpectrum ? CorrectionPointCount : 1;
            using (var wavelength = new BinaryWriter(File.Create(config.WavelengthFile)))
            {
                wavelength.Write((ulong)(sizeof(ulong) + count * sizeof(double)));
                for (int index = 0; index < count; index++) wavelength.Write(Wavelength(count, index));
            }
            WriteMagnitude(config.MaguideFile, count, coefficient: 1);
        }

        internal static void WriteMagnitude(string path, int count, double coefficient)
        {
            using var magnitude = new BinaryWriter(File.Create(path));
            magnitude.Write((ulong)(24 + count * 2 * sizeof(double)));
            magnitude.Write(10f);
            magnitude.Write(1);
            magnitude.Write((ulong)count);
            for (int index = 0; index < count; index++) magnitude.Write(Wavelength(count, index));
            for (int index = 0; index < count; index++) magnitude.Write(coefficient);
        }

        private static double Wavelength(int count, int index) => count == 1 ? 500d : 380d + index * .1d;

        public async ValueTask DisposeAsync()
        {
            service.Dispose();
            await mqtt.DisconnectAsyncClient().WaitAsync(Deadline);
            DisplayConfigManager.Instance.Configs.TryRemove(Device.Code, out _);
            foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
            directory.Delete();
        }
    }
}
