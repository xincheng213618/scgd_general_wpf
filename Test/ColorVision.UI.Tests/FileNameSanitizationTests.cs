using ColorVision.Common.Utilities;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FileNameSanitizationTests
{
    [Theory]
    [InlineData("P5QD0L??GLJ002Q_L_190236", "P5QD0L__GLJ002Q_L_190236")]
    [InlineData("A/B\\C:D*E?F\"G<H>I|J\r\n\0", "A_B_C_D_E_F_G_H_I_J___")]
    [InlineData("正常SN-123_20261023T190236.1234567", "正常SN-123_20261023T190236.1234567")]
    [InlineData("SN. ", "SN")]
    [InlineData("...", "_")]
    [InlineData("CON", "_CON")]
    [InlineData("nul.csv", "_nul.csv")]
    [InlineData("COM1.result.json", "_COM1.result.json")]
    [InlineData("LPT²", "_LPT²")]
    public void NamesAreSafeForWindowsFilesAndStableAcrossCallBoundaries(string input, string expected)
    {
        string result = Tool.SanitizeFileName(input);

        Assert.Equal(expected, result);
        Assert.Equal(result, Tool.SanitizeFileName(result));
        Assert.Equal(result, Path.GetFileName(result));
        Assert.DoesNotContain(result, character => Path.GetInvalidFileNameChars().Contains(character));
        Assert.False(result.EndsWith('.') || result.EndsWith(' '));
    }
}
