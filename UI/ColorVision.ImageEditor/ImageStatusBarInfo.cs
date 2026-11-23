using ColorVision.ImageEditor.Settings;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;

namespace ColorVision.ImageEditor;

internal sealed record ImageStatusBarField(string Label, string Value);

internal sealed record ImageStatusBarInfo(
    string Summary,
    string DisplayName,
    string? FilePath,
    IReadOnlyList<ImageStatusBarField> Fields)
{
    internal static ImageStatusBarInfo? Create(ImageViewConfig config)
    {
        int width = config.GetProperties<int>(ImageViewPropertyKeys.Cols);
        int height = config.GetProperties<int>(ImageViewPropertyKeys.Rows);
        if (width <= 0 || height <= 0)
        {
            width = config.GetProperties<int>(ImageViewPropertyKeys.ImageWidth);
            height = config.GetProperties<int>(ImageViewPropertyKeys.ImageHeight);
        }
        if (width <= 0 || height <= 0) return null;

        int channels = config.GetProperties<int>(ImageViewPropertyKeys.Channel);
        int depth = config.GetProperties<int>(ImageViewPropertyKeys.Depth);
        string? filePath = string.IsNullOrWhiteSpace(config.FilePath) ? null : config.FilePath;
        string? fileName = filePath == null ? null : Path.GetFileName(filePath);
        string? extension = Path.GetExtension(filePath)?.ToUpperInvariant();
        string dimensions = $"{width} × {height} px";

        var chunks = new List<string> { dimensions };
        if (channels > 0) chunks.Add($"{SettingsText.Channels} {channels}");
        if (depth > 0) chunks.Add($"{depth} bit");
        if (!string.IsNullOrEmpty(extension)) chunks.Add(extension);
        string? fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrEmpty(fileNameWithoutExtension)) chunks.Add(fileNameWithoutExtension);

        var fields = new List<ImageStatusBarField> { new(SettingsText.Dimensions, dimensions) };
        if (channels > 0) fields.Add(new(SettingsText.Channels, channels.ToString()));
        if (depth > 0) fields.Add(new(SettingsText.Depth, $"{depth} bit"));
        if (config.Properties.TryGetValue(ImageViewPropertyKeys.PixelFormat, out object? format) && format is PixelFormat pixelFormat)
            fields.Add(new(SettingsText.Format, pixelFormat.ToString()));
        double dpiX = config.GetProperties<double>(ImageViewPropertyKeys.DpiX);
        double dpiY = config.GetProperties<double>(ImageViewPropertyKeys.DpiY);
        if (dpiX > 0 && dpiY > 0) fields.Add(new("DPI", $"{dpiX:0.##} × {dpiY:0.##}"));

        if (filePath != null)
        {
            if (!string.IsNullOrEmpty(extension)) fields.Add(new(DisplayText.Get("文件类型"), extension));
            if (config.Properties.TryGetValue(ImageViewPropertyKeys.FileSize, out object? size) && size is long bytes && bytes >= 0)
                fields.Add(new(SettingsText.FileSize, $"{bytes:N0} B"));
            AddMetadata(DisplayText.Get("文件创建时间"), ImageViewPropertyKeys.FileCreationTime);
            AddMetadata(DisplayText.Get("文件修改时间"), ImageViewPropertyKeys.FileModifiedTime);
            fields.Add(new(SettingsText.FilePath, filePath));
        }

        AddMetadata(DisplayText.Get("拍摄时间"), ImageViewPropertyKeys.DateTaken);
        AddMetadata(DisplayText.Get("相机型号"), ImageViewPropertyKeys.CameraModel, "ColorVision.Camera.Model");
        AddMetadata(DisplayText.Get("相机厂商"), ImageViewPropertyKeys.CameraManufacturer, "ColorVision.Camera.Manufacturer");
        AddMetadata(DisplayText.Get("采集增益"), ImageViewPropertyKeys.ColorVisionGain, "Gain");
        AddMetadata(DisplayText.Get("曝光"), ImageViewPropertyKeys.ColorVisionExposure, "exp", "Exp");
        AddMetadata(DisplayText.Get("源文件通道数"), ImageViewPropertyKeys.ColorVisionSourceChannels, "CVRawSourceChannel");
        AddMetadata(DisplayText.Get("源文件位深"), ImageViewPropertyKeys.ColorVisionBpp, "CVRawSourceBpp");
        AddMetadata(DisplayText.Get("关联源文件"), ImageViewPropertyKeys.ColorVisionAssociatedSourceFileName, "srcFileName");

        string displayName = string.IsNullOrEmpty(fileName) ? SettingsText.CurrentImage : fileName;
        return new ImageStatusBarInfo(string.Join("  ·  ", chunks), displayName, filePath, fields);

        void AddMetadata(string label, params string[] keys)
        {
            foreach (string key in keys)
            {
                if (!config.Properties.TryGetValue(key, out object? value) || value == null
                    || value is string text && string.IsNullOrWhiteSpace(text) || value is Array { Length: 0 })
                    continue;

                string formatted = value is DateTime dateTime ? dateTime.ToString("G") : ImageViewConfig.FormatPropertyValue(value);
                fields.Add(new(label, formatted));
                return;
            }
        }
    }
}
