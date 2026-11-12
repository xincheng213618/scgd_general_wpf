using ColorVision.UI.LogImp;
using ColorVision.UI.LogImp.Controls;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class LogLocalOutputTests
{
    private static readonly MethodInfo Poll = typeof(LogLocalOutput).GetMethod(
        "ReadNewLogContent", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData("")]
    [InlineData("-longer-than-before")]
    public void AutoRefreshReloadsAReplacementLogAtTheSamePath(string extra)
    {
        WithLog((path, replacement) =>
        {
            File.WriteAllText(path, "old file\n", Encoding.ASCII);
            using var control = new LogLocalOutput(path);
            Assert.Contains("old file", DisplayedText(control));

            File.WriteAllText(replacement, $"new file{extra}\n", Encoding.ASCII);
            File.Move(replacement, path, overwrite: true);
            Poll.Invoke(control, null);

            string text = DisplayedText(control);
            Assert.Contains($"new file{extra}", text);
            Assert.DoesNotContain("old file", text);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AutoRefreshKeepsTheEncodingDetectedFromTheFilePreamble(bool utf16)
    {
        WithLog((path, _) =>
        {
            Encoding actualEncoding = utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            File.WriteAllText(path, string.Concat(Enumerable.Repeat("旧记录\n", 1100)), actualEncoding);
            using var control = new LogLocalOutput(path, Encoding.Latin1);
            string initial = DisplayedText(control);

            File.AppendAllText(path, "新记录\n", actualEncoding);
            Poll.Invoke(control, null);

            Assert.Contains("旧记录", initial);
            Assert.Contains("新记录", DisplayedText(control));
        });
    }

    private static string DisplayedText(LogLocalOutput control)
    {
        var viewer = Assert.IsType<LogViewerControl>(control.FindName("LogViewer"));
        return Assert.IsType<TextBox>(viewer.FindName("PlainTextBox")).Text;
    }

    private static void WithLog(Action<string, string> check)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("ColorVision-LogTailTests-");
        string path = Path.Combine(root.FullName, "service.log");
        string replacement = Path.Combine(root.FullName, "replacement.log");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                var resources = Application.Current.Resources;
                const string styleKey = "SearchBarPlus";
                bool installStyle = !resources.Contains(styleKey);
                if (installStyle)
                    resources.Add(styleKey, new Style(typeof(HandyControl.Controls.SearchBar)));
                try { check(path, replacement); }
                finally { if (installStyle) resources.Remove(styleKey); }
            });
        }
        finally
        {
            File.Delete(replacement);
            File.Delete(path);
            root.Delete();
        }
    }
}
