using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Spectrum.Calibration;

public sealed class SpectrumCalibrationFilePickerConfig : IConfig
{
    public string LastOpenDirectory { get; set; } = string.Empty;
}

internal static class SpectrumCalibrationFilePicker
{
    internal static string? Browse(Window? owner, string? currentPath, bool? opensLocally)
    {
        var previousDirectory = ConfigService.Instance?.GetRequiredService<SpectrumCalibrationFilePickerConfig>().LastOpenDirectory;
        var dialog = new OpenFileDialog { Filter = Properties.Resources.SpectrumCalibrationGroups_DatFileFilter, RestoreDirectory = true };
        return OpenFilePicker.ShowDialog(dialog, owner, ResolvePath(currentPath, opensLocally), "SpectrumCalibration", previousDirectory);
    }

    private static string? ResolvePath(string? path, bool? opensLocally)
    {
        if (!opensLocally.HasValue || string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            return opensLocally.Value
                ? LocalSpectrumSession.ResolvePath(path)
                : DeviceSpectrum.ResolveCalibrationFilePath(path, ServiceConfig.Instance.CVMainService_x64);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }
}

public sealed class SpectrumCalibrationFilePropertiesEditor : TextSelectFilePropertiesEditor
{
    protected override string? BrowseFile(Window? owner, string? currentPath, string historyKey)
    {
        Window? ancestor = owner;
        while (ancestor != null && ancestor is not EditSpectrum)
            ancestor = ancestor.Owner;

        return SpectrumCalibrationFilePicker.Browse(owner, currentPath, (ancestor as EditSpectrum)?.Draft.DisplayConfig.UseLocalSpectrum);
    }
}
