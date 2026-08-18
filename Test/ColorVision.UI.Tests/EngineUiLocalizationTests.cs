using ColorVision.Engine;
using System.Globalization;

namespace ColorVision.UI.Tests;

public class EngineUiLocalizationTests
{
    [Fact]
    public void EnglishMySqlBackupUiUsesLocalizedText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Rename", new EngineLangExtension("重命名").ProvideValue(null!));
            Assert.Equal("Rename Backup", EngineLocalization.Get("重命名备份"));
            Assert.Equal("Failed to rename: denied", EngineLocalization.Format($"重命名失败：{"denied"}"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void EnglishDatabaseCleanupUiUsesLocalizedStaticAndFormattedText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Database Cleanup", new EngineLangExtension("数据库清理").ProvideValue(null!));
            Assert.Equal("3 table(s) · 120 row(s) · 2 MB", EngineLocalization.Format($"{3:N0} 张表 · {120:N0} 行 · {"2 MB"}"));
            Assert.Equal(
                "Dangerous operation: all 2 available table(s) in MySQL Result Tables will be cleared.",
                EngineLocalization.Format($"危险操作：将清空 {"MySQL Result Tables"} 中全部 {2:N0} 张可用数据表。"));
            Assert.Equal("Socket Messages SQLite", EngineLocalization.Get("Socket 消息 SQLite"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void EnglishSpectrumCorrectionUiUsesLocalizedStaticAndFormattedText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Spectral Correction", new EngineLangExtension("光谱校正").ProvideValue(null!));
            Assert.Equal(
                "Capture complete: 401 points, 380–780 nm, interval 1 nm.",
                EngineLocalization.Format($"采集完成：{401} 点，{380d:G6}–{780d:G6} nm，间隔 {1d:G6} nm。"));
            Assert.Equal("Standard Spectrum", EngineLocalization.Get("标准光谱"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void EnglishPropertyGridMetadataUsesExistingResourceLookup()
    {
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal(
                "Radius",
                ColorVision.UI.PropertyEditorHelper.GetLocalizedString(ColorVision.Engine.Properties.Resources.ResourceManager, "半径"));
            Assert.Equal(
                "Use Three-channel Exposure",
                ColorVision.UI.PropertyEditorHelper.GetLocalizedString(ColorVision.Engine.Properties.Resources.ResourceManager, "使用三通道曝光"));
            Assert.Equal(
                "Configure separate R, G, and B exposure values. When disabled, a single exposure value is used.",
                ColorVision.UI.PropertyEditorHelper.GetLocalizedString(
                    ColorVision.Engine.Properties.Resources.ResourceManager,
                    "分别配置 R、G、B 三个曝光值；关闭时只使用一个曝光值。"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}
