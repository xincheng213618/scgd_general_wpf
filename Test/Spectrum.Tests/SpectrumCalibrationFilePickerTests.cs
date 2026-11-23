using ColorVision.Engine.Services.Devices.Spectrum.Calibration;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;

namespace Spectrum.Tests;

public sealed class SpectrumCalibrationFilePickerTests
{
    [Fact]
    public void MissingCalibrationUsesLastOpenDirectoryEvenWhenItsParentExists()
    {
        var configuredDirectory = Directory.CreateTempSubdirectory();
        var lastOpenDirectory = Directory.CreateTempSubdirectory();
        string currentPath = Path.Combine(configuredDirectory.FullName, "wave.dat");

        try
        {
            Assert.Equal((lastOpenDirectory.FullName, null), PathSelectionHelper.GetFileDialogStart(currentPath, lastOpenDirectory.FullName));
            Assert.Equal((lastOpenDirectory.FullName, null), PathSelectionHelper.GetFileDialogStart(null, lastOpenDirectory.FullName));
            Assert.Equal((configuredDirectory.FullName, null), PathSelectionHelper.GetFileDialogStart(currentPath, Path.Combine(lastOpenDirectory.FullName, "missing")));
            Assert.Equal((configuredDirectory.FullName, null), PathSelectionHelper.GetFileDialogStart(configuredDirectory.FullName, lastOpenDirectory.FullName));

            File.WriteAllText(currentPath, string.Empty);
            Assert.Equal((configuredDirectory.FullName, "wave.dat"), PathSelectionHelper.GetFileDialogStart(currentPath, lastOpenDirectory.FullName));
        }
        finally
        {
            File.Delete(currentPath);
            configuredDirectory.Delete();
            lastOpenDirectory.Delete();
        }
    }

    [Fact]
    public void WorkflowDirectoriesAndLegacySpectrumHistorySurviveConfigReload()
    {
        var directory = Directory.CreateTempSubdirectory();
        string configPath = Path.Combine(directory.FullName, "client.json");

        try
        {
            var config = new ConfigHandler { ConfigFilePath = configPath };
            config.GetRequiredService<SpectrumCalibrationFilePickerConfig>().LastOpenDirectory = directory.FullName;
            config.Save<SpectrumCalibrationFilePickerConfig>();
            var history = config.GetRequiredService<FilePickerHistoryConfig>();
            history.LastOpenDirectories["DisplayAlgorithm"] = Path.GetTempPath();
            history.LastOpenDirectories["ImagingCorrectionReference"] = directory.FullName;
            config.Save<FilePickerHistoryConfig>();

            var reloadedConfig = new ConfigHandler { ConfigFilePath = configPath };
            reloadedConfig.LoadConfigs();
            var preferences = reloadedConfig.GetRequiredService<SpectrumCalibrationFilePickerConfig>();
            Assert.Equal(directory.FullName, preferences.LastOpenDirectory);
            Assert.Equal((directory.FullName, null), PathSelectionHelper.GetFileDialogStart(null, preferences.LastOpenDirectory));
            var reloadedDirectories = reloadedConfig.GetRequiredService<FilePickerHistoryConfig>().LastOpenDirectories;
            Assert.Equal(Path.GetTempPath(), reloadedDirectories["DisplayAlgorithm"]);
            Assert.Equal(directory.FullName, reloadedDirectories["ImagingCorrectionReference"]);
        }
        finally
        {
            File.Delete(configPath);
            directory.Delete();
        }
    }
}
