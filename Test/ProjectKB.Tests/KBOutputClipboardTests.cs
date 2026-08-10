using Xunit;

namespace ProjectKB.Tests;

public class KBOutputClipboardTests
{
    [Fact]
    public void FormatOutputTextForClipboardConvertsDetailRowsToTabSeparatedColumns()
    {
        string source = string.Join(Environment.NewLine,
        [
            "按键 (PT)            亮度 (Lv)     局部对比度 (LC)",
            "[Point_103]          19.30               9.53%  Fail",
            "[Point 104]          16.61             -13.09%"
        ]);

        string result = ProjectKBWindow.FormatOutputTextForClipboard(source);

        string expected = string.Join(Environment.NewLine,
        [
            "按键 (PT)\t亮度 (Lv)\t局部对比度 (LC)\t结果 (Result)",
            "[Point_103]\t19.30\t9.53%\tFail",
            "[Point 104]\t16.61\t-13.09%\t"
        ]);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void FormatOutputTextForClipboardPreservesNonDetailText()
    {
        const string source = "机种 (Model):6AJ20U\r\nSN:123456";

        string result = ProjectKBWindow.FormatOutputTextForClipboard(source);

        Assert.Equal($"机种 (Model):6AJ20U{Environment.NewLine}SN:123456", result);
    }
}
