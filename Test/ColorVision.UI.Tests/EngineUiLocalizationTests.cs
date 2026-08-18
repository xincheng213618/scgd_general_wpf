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
}
