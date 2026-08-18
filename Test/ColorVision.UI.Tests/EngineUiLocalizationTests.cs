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
}
