using ColorVision.Engine.Services.Devices.Camera.Job;
using ColorVision.Engine.Services.Devices.Spectrum.Job;
using ColorVision.Scheduler;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public class SchedulerTaskSerializationTests
{
    [Fact]
    public void LegacyGoldenFile_DeserializesAndRoundTripsPolymorphicConfiguration()
    {
        string fixturePath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "Scheduler",
            "scheduler_tasks.legacy.json");

        var tasks = SchedulerTaskSerializer.LoadFromFile(fixturePath);

        SchedulerInfo task = Assert.Single(tasks);
        Assert.Equal("LegacyCameraCapture", task.JobName);
        Assert.Equal("legacy-camera", task.GroupName);
        Assert.Equal(JobExecutionMode.Cron, task.Mode);
        Assert.Equal(typeof(CameraCaptureJob), task.JobType);
        Assert.Equal(0, task.ScheduleDefinitionVersion);

        var config = Assert.IsType<CameraCaptureJobConfig>(task.Config);
        Assert.Equal("Camera-A", config.DeviceCameraName);
        Assert.Null(config.DeviceCameraCode);

        string roundTripJson = SchedulerTaskSerializer.Serialize(tasks);
        Assert.Contains("\"$type\"", roundTripJson, StringComparison.Ordinal);
        Assert.Contains("ColorVision.Scheduler.SchedulerInfo", roundTripJson, StringComparison.Ordinal);
        Assert.Contains("CameraCaptureJobConfig", roundTripJson, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CameraCaptureJobConfig.DeviceCameraCode), roundTripJson, StringComparison.Ordinal);

        SchedulerInfo roundTrippedTask = Assert.Single(SchedulerTaskSerializer.Deserialize(roundTripJson));
        Assert.Equal(task.JobName, roundTrippedTask.JobName);
        var roundTrippedConfig = Assert.IsType<CameraCaptureJobConfig>(roundTrippedTask.Config);
        Assert.Equal("Camera-A", roundTrippedConfig.DeviceCameraName);
        Assert.Null(roundTrippedConfig.DeviceCameraCode);
    }

    [Theory]
    [InlineData("camera-code", "spectrum-code")]
    [InlineData("", "")]
    public void ExplicitDeviceCodes_RoundTripAlongsideLegacyNames(string cameraCode, string spectrumCode)
    {
        string json = SchedulerTaskSerializer.Serialize(
        [
            new() { JobType = typeof(CameraCaptureJob), Config = new CameraCaptureJobConfig
                { DeviceCameraCode = cameraCode, DeviceCameraName = "Old camera name" } },
            new() { JobType = typeof(SpectrumGetDataJob), Config = new SpectrumGetDataJobConfig
                { DeviceSpectrumCode = spectrumCode, DeviceSpectrumName = "Old spectrometer name" } },
        ]);

        Assert.Collection(SchedulerTaskSerializer.Deserialize(json), camera =>
        {
            Assert.Equal(typeof(CameraCaptureJob), camera.JobType);
            var config = Assert.IsType<CameraCaptureJobConfig>(camera.Config);
            Assert.Equal(cameraCode, config.DeviceCameraCode);
            Assert.Equal("Old camera name", config.DeviceCameraName);
        }, spectrum =>
        {
            Assert.Equal(typeof(SpectrumGetDataJob), spectrum.JobType);
            var config = Assert.IsType<SpectrumGetDataJobConfig>(spectrum.Config);
            Assert.Equal(spectrumCode, config.DeviceSpectrumCode);
            Assert.Equal("Old spectrometer name", config.DeviceSpectrumName);
        });
    }

    [Fact]
    public void LegacySpectrumName_RoundTripsWithoutAnExplicitCode()
    {
        var document = JObject.Parse(SchedulerTaskSerializer.Serialize(
        [
            new() { JobType = typeof(SpectrumGetDataJob), Config = new SpectrumGetDataJobConfig { DeviceSpectrumName = "old-spectrum" } },
        ]));
        var legacyConfig = Assert.IsType<JObject>(document["$values"]![0]!["Config"]);
        legacyConfig.Remove(nameof(SpectrumGetDataJobConfig.DeviceSpectrumCode));

        var tasks = SchedulerTaskSerializer.Deserialize(document.ToString());
        string roundTripJson = SchedulerTaskSerializer.Serialize(tasks);
        Assert.DoesNotContain(nameof(SpectrumGetDataJobConfig.DeviceSpectrumCode), roundTripJson, StringComparison.Ordinal);
        var roundTripped = Assert.Single(SchedulerTaskSerializer.Deserialize(roundTripJson));
        Assert.Equal(typeof(SpectrumGetDataJob), roundTripped.JobType);
        var config = Assert.IsType<SpectrumGetDataJobConfig>(roundTripped.Config);
        Assert.Equal("old-spectrum", config.DeviceSpectrumName);
        Assert.Null(config.DeviceSpectrumCode);
    }

    [Fact]
    public void NewTask_UsesCurrentScheduleDefinitionVersion()
    {
        var task = new SchedulerInfo();

        Assert.Equal(SchedulerInfo.CurrentScheduleDefinitionVersion, task.ScheduleDefinitionVersion);
    }

    [Fact]
    public void SaveToFile_PreservesTheBackupWhenThePrimaryIsMissingOrCorrupt()
    {
        DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-SchedulerSaveGuard-");
        string path = Path.Combine(directory.FullName, "tasks.json");
        string backupPath = path + ".bak";
        try
        {
            SchedulerTaskSerializer.SaveToFile(backupPath, [new() { JobName = "recoverable" }]);
            byte[] backup = File.ReadAllBytes(backupPath);

            Assert.Throws<InvalidDataException>(() => SchedulerTaskSerializer.SaveToFile(path, []));
            Assert.False(File.Exists(path));
            Assert.Equal(backup, File.ReadAllBytes(backupPath));

            const string corrupt = "{\"$values\":[";
            File.WriteAllText(path, corrupt);
            Assert.Throws<InvalidDataException>(() => SchedulerTaskSerializer.SaveToFile(path, []));
            Assert.Equal(corrupt, File.ReadAllText(path));
            Assert.Equal(backup, File.ReadAllBytes(backupPath));
        }
        finally
        {
            File.Delete(path);
            File.Delete(backupPath);
            directory.Delete();
        }
    }

    [Fact]
    public void SaveToFile_ReplacesDefinitionAtomicallyAndKeepsPreviousBackup()
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            nameof(SchedulerTaskSerializationTests),
            Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(temporaryDirectory, "scheduler_tasks.json");

        try
        {
            var first = new System.Collections.ObjectModel.ObservableCollection<SchedulerInfo>
            {
                new() { JobName = "before", GroupName = "compatibility" },
            };
            SchedulerTaskSerializer.SaveToFile(filePath, first);

            var second = new System.Collections.ObjectModel.ObservableCollection<SchedulerInfo>
            {
                new() { JobName = "after", GroupName = "compatibility" },
            };
            SchedulerTaskSerializer.SaveToFile(filePath, second);

            SchedulerInfo savedTask = Assert.Single(SchedulerTaskSerializer.LoadFromFile(filePath));
            Assert.Equal("after", savedTask.JobName);
            Assert.Equal(SchedulerInfo.CurrentScheduleDefinitionVersion, savedTask.ScheduleDefinitionVersion);
            Assert.Equal("before", Assert.Single(SchedulerTaskSerializer.LoadFromFile(filePath + ".bak")).JobName);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}
