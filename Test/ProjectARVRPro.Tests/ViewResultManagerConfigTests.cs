using Newtonsoft.Json;
using System.ComponentModel;

namespace ProjectARVRPro.Tests;

public sealed class ViewResultManagerConfigTests
{
    [Fact]
    public void RuntimeUiState_IsBoundButNotUserEditable()
    {
        ProjectARVRProConfig config = new();
        List<string?> propertyChanges = [];
        string? changedSerialNumber = null;
        config.PropertyChanged += (_, args) => propertyChanges.Add(args.PropertyName);
        config.SNChanged += (_, value) => changedSerialNumber = value;

        PropertyDescriptor stepIndex = TypeDescriptor.GetProperties(typeof(ProjectARVRProConfig))[nameof(ProjectARVRProConfig.StepIndex)]!;
        PropertyDescriptor serialNumber = TypeDescriptor.GetProperties(typeof(ProjectARVRProConfig))[nameof(ProjectARVRProConfig.SN)]!;

        Assert.False(stepIndex.IsBrowsable);
        Assert.False(serialNumber.IsBrowsable);

        config.StepIndex = 2;
        config.SN = "SN-BOUND";

        Assert.Equal(2, config.StepIndex);
        Assert.Equal("SN-BOUND", config.SN);
        Assert.Equal("SN-BOUND", changedSerialNumber);
        Assert.Contains(nameof(ProjectARVRProConfig.StepIndex), propertyChanges);
        Assert.Contains(nameof(ProjectARVRProConfig.SN), propertyChanges);

        string json = JsonConvert.SerializeObject(config);
        Assert.Contains($"\"{nameof(ProjectARVRProConfig.StepIndex)}\":2", json, StringComparison.Ordinal);
        Assert.DoesNotContain($"\"{nameof(ProjectARVRProConfig.SN)}\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectResultOverlayDefaultsMatchProductionDisplay()
    {
        ProjectARVRProConfig config = new();

        Assert.True(config.ResultOverlayShowName);
        Assert.True(config.ResultOverlayShowDetail);
        Assert.Equal(80, config.ResultOverlayFontSize);
        Assert.False(config.ResultOverlayAutoRefresh);
    }

    [Fact]
    public void ImageExportSettings_DefaultToIndependentDisabledLanes()
    {
        ViewResultManagerConfig config = new();

        Assert.False(config.IsSaveImageReuslt);
        Assert.Equal(ResultImageFormat.PNG, config.ResultSnapshotFormat);
        Assert.Equal(ImageExportSize.完整尺寸, config.ResultSnapshotSize);
        Assert.True(config.ResultSnapshotIncludeOverlays);
        Assert.Equal(
            [ResultImageFormat.PNG, ResultImageFormat.JPEG],
            Enum.GetValues<ResultImageFormat>());

        Assert.False(config.IsSaveSourceImage);
        Assert.Equal(SourceImageFormat.TIFF, config.SourceExportFormat);
        Assert.Equal(SourceTiffCompression.LZW, config.SourceTiffCompressionMode);
        Assert.False(config.ShowSourceTiffCompression);
        Assert.False(config.SourceImageSupportsBmp);
        Assert.False(config.ShowSourceFormatWithBmp);
        Assert.False(config.ShowSourceFormatWithoutBmp);
        Assert.Null(typeof(ViewResultManagerConfig).GetProperty("SourceImageSize"));
        Assert.Equal(
            [SourceImageFormat.TIFF, SourceImageFormat.PNG, SourceImageFormat.BMP],
            Enum.GetValues<SourceImageFormat>());
    }

    [Fact]
    public void SourceFormatChoices_RemoveBmpForHighBitImages()
    {
        ViewResultManagerConfig config = new()
        {
            IsSaveSourceImage = true,
            SourceImageSupportsBmp = true,
            SourceExportFormatWithBmp = SourceImageFormat.BMP,
        };

        Assert.True(config.ShowSourceFormatWithBmp);
        Assert.False(config.ShowSourceFormatWithoutBmp);
        Assert.Equal(SourceImageFormat.BMP, config.SourceExportFormat);

        config.SourceImageSupportsBmp = false;

        Assert.False(config.ShowSourceFormatWithBmp);
        Assert.True(config.ShowSourceFormatWithoutBmp);
        Assert.Equal(
            [SourceImageHighBitFormat.TIFF, SourceImageHighBitFormat.PNG],
            Enum.GetValues<SourceImageHighBitFormat>());
        Assert.Equal(SourceImageFormat.TIFF, config.SourceExportFormat);
    }

    [Fact]
    public void SourceTiffCompression_IsVisibleOnlyForEnabledTiffSourceExport()
    {
        ViewResultManagerConfig config = new()
        {
            IsSaveSourceImage = true,
        };

        Assert.True(config.ShowSourceTiffCompression);

        config.SourceExportFormat = SourceImageFormat.PNG;
        Assert.False(config.ShowSourceTiffCompression);

        config.IsSaveImageReuslt = true;
        Assert.True(config.IsSaveImageReuslt);
        Assert.True(config.IsSaveSourceImage);
    }

    [Fact]
    public void ImageExportEnums_NormalizeUnknownAndLegacyValues()
    {
        ViewResultManagerConfig config = new();

        config.ResultSnapshotFormat = (ResultImageFormat)123;
        Assert.Equal(ResultImageFormat.PNG, config.ResultSnapshotFormat);

        config.ResultSnapshotSize = (ImageExportSize)4096;
        Assert.Equal(ImageExportSize.二分之一尺寸, config.ResultSnapshotSize);
        config.ResultSnapshotSize = (ImageExportSize)123;
        Assert.Equal(ImageExportSize.完整尺寸, config.ResultSnapshotSize);

        config.SourceExportFormat = (SourceImageFormat)123;
        Assert.Equal(SourceImageFormat.TIFF, config.SourceExportFormat);
        config.SourceTiffCompressionMode = (SourceTiffCompression)123;
        Assert.Equal(SourceTiffCompression.LZW, config.SourceTiffCompressionMode);
    }

    [Fact]
    public void CompressionLevels_AreNotUserConfigurable()
    {
        ViewResultManagerConfig config = new();
        var jpegQuality = typeof(ViewResultManagerConfig).GetProperty(nameof(ViewResultManagerConfig.ResultSnapshotJpegQuality));
        var legacyDelay = typeof(ViewResultManagerConfig).GetProperty(nameof(ViewResultManagerConfig.SaveImageReusltDelay));

        Assert.NotNull(jpegQuality);
        Assert.False(jpegQuality.GetCustomAttributes(typeof(BrowsableAttribute), inherit: true)
            .Cast<BrowsableAttribute>()
            .Single()
            .Browsable);
        jpegQuality.SetValue(config, 1);
        Assert.Equal(100, jpegQuality.GetValue(config));

        Assert.NotNull(legacyDelay);
        Assert.False(legacyDelay.GetCustomAttributes(typeof(BrowsableAttribute), inherit: true)
            .Cast<BrowsableAttribute>()
            .Single()
            .Browsable);
        legacyDelay.SetValue(config, 1000);
        Assert.Equal(0, legacyDelay.GetValue(config));
    }

    [Fact]
    public void ResultPaneHeight_IsPersistedButNotUserEditable()
    {
        PropertyDescriptor height = TypeDescriptor.GetProperties(typeof(ViewResultManagerConfig))[nameof(ViewResultManagerConfig.Height)]!;
        ViewResultManagerConfig original = new() { Height = 321 };

        Assert.False(height.IsBrowsable);

        string json = JsonConvert.SerializeObject(original);
        ViewResultManagerConfig restored = JsonConvert.DeserializeObject<ViewResultManagerConfig>(json)!;

        Assert.Contains($"\"{nameof(ViewResultManagerConfig.Height)}\":321", json, StringComparison.Ordinal);
        Assert.Equal(321, restored.Height);
    }

    [Fact]
    public void ImageExportSettings_RoundTripWithoutLegacyCompressionFields()
    {
        ViewResultManagerConfig original = new()
        {
            IsSaveImageReuslt = true,
            ResultSnapshotFormat = ResultImageFormat.JPEG,
            ResultSnapshotSize = ImageExportSize.四分之一尺寸,
            ResultSnapshotIncludeOverlays = false,
            IsSaveSourceImage = true,
            SourceExportFormat = SourceImageFormat.TIFF,
            SourceTiffCompressionMode = SourceTiffCompression.ZIP,
        };

        string json = JsonConvert.SerializeObject(original);
        ViewResultManagerConfig restored = JsonConvert.DeserializeObject<ViewResultManagerConfig>(json)!;

        Assert.True(restored.IsSaveImageReuslt);
        Assert.Equal(ResultImageFormat.JPEG, restored.ResultSnapshotFormat);
        Assert.Equal(ImageExportSize.四分之一尺寸, restored.ResultSnapshotSize);
        Assert.False(restored.ResultSnapshotIncludeOverlays);
        Assert.True(restored.IsSaveSourceImage);
        Assert.Equal(SourceImageFormat.TIFF, restored.SourceExportFormat);
        Assert.Equal(SourceTiffCompression.ZIP, restored.SourceTiffCompressionMode);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.SaveImageReusltDelay), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.ResultSnapshotJpegQuality), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.ShowSourceTiffCompression), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.SourceImageSupportsBmp), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.SourceExportFormatWithBmp), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(ViewResultManagerConfig.SourceExportFormatWithoutBmp), json, StringComparison.Ordinal);
    }
}
