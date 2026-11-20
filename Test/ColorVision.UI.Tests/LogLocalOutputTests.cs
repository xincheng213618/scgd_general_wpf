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
    private static readonly MethodInfo ApplySearch = typeof(LogLocalOutput).GetMethod(
        "ApplySearchFilter", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Theory]
    [InlineData("")]
    [InlineData("-longer-than-before")]
    public Task AutoRefreshReloadsAReplacementLogAtTheSamePath(string extra)
    {
        return WithLog(async (path, replacement) =>
        {
            File.WriteAllText(path, "old file\n", Encoding.ASCII);
            await WithControl(path, async control =>
            {
                Assert.Contains("old file", DisplayedText(control));

                File.WriteAllText(replacement, $"new file{extra}\n", Encoding.ASCII);
                File.Move(replacement, path, overwrite: true);
                await WpfTestHost.Invoke(control.ReadNewLogContent).WaitAsync(TestTimeout);

                string text = DisplayedText(control);
                Assert.Contains($"new file{extra}", text);
                Assert.DoesNotContain("old file", text);
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AutoRefreshKeepsTheEncodingDetectedFromTheFilePreamble(bool utf16)
    {
        return WithLog(async (path, _) =>
        {
            Encoding actualEncoding = utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            File.WriteAllText(path, string.Concat(Enumerable.Repeat("旧记录\n", 1100)), actualEncoding);
            await WithControl(path, async control =>
            {
                string initial = DisplayedText(control);

                File.AppendAllText(path, "新记录", actualEncoding);
                await WpfTestHost.Invoke(control.ReadNewLogContent).WaitAsync(TestTimeout);

                Assert.Contains("旧记录", initial);
                Assert.Contains("新记录", DisplayedText(control));
            }, Encoding.Latin1);
        });
    }

    [Fact]
    public Task LocalLogSearchPreservesUppercaseRegexEscapes()
    {
        return WithLog(async (path, _) =>
        {
            File.WriteAllLines(path, ["value=ready", "value=123"]);
            await WithControl(path, control =>
            {
                WpfTestHost.Invoke(() =>
                {
                    var search = Assert.IsType<HandyControl.Controls.SearchBar>(control.FindName("SearchBar1"));
                    search.Text = @"^value=\D+$";
                    ApplySearch.Invoke(control, null);
                });

                Assert.Equal("value=ready", DisplayedText(control));
                return Task.CompletedTask;
            });
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public Task InFlightHistoryKeepsDispatcherResponsiveAndHandlesAppendClearOrClose(bool clear, bool close)
    {
        return WithLog(async (path, _) =>
        {
            File.WriteAllText(path, "旧记录\n", Encoding.UTF8);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var allowRead = new ManualResetEventSlim();
            int opens = 0;
            var control = WpfTestHost.Invoke(() => new LogLocalOutput(path, Encoding.UTF8, file =>
            {
                if (Interlocked.Increment(ref opens) == 1) return new PausingLogFile(file, entered, allowRead);
                return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            }));
            Task completion = WpfTestHost.Invoke(() => control.ReadCompletion);
            try
            {
                await entered.Task.WaitAsync(TestTimeout);
                Assert.Empty(DisplayedText(control));
                if (clear) WpfTestHost.Invoke(control.ClearLog);
                if (close) WpfTestHost.Invoke(control.Dispose);
                File.AppendAllText(path, "新记录", Encoding.UTF8);
                WpfTestHost.Invoke(() =>
                {
                    Assert.Same(completion, control.ReadNewLogContent());
                    Assert.Same(completion, control.ReadNewLogContent());
                });
                Assert.Equal(1, Volatile.Read(ref opens));
                allowRead.Set();
                await completion.WaitAsync(TestTimeout);

                bool reverse = WpfTestHost.Invoke(() => control.Config.LogReverse);
                string combined = string.Join(Environment.NewLine, reverse ? ["新记录", "旧记录"] : new[] { "旧记录", "新记录" });
                Assert.Equal(close ? string.Empty : clear ? "新记录" : combined, DisplayedText(control));
                Assert.Equal(close ? 1 : 2, Volatile.Read(ref opens));
            }
            finally
            {
                allowRead.Set();
                try { await completion.WaitAsync(TestTimeout); }
                finally { WpfTestHost.Invoke(control.Dispose); }
            }
        });
    }

    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    private sealed class PausingLogFile(string path, TaskCompletionSource entered, ManualResetEventSlim allowRead)
        : FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)
    {
        public override int Read(Span<byte> buffer)
        {
            entered.TrySetResult();
            if (!allowRead.Wait(TestTimeout)) throw new TimeoutException("The log read was not released.");
            return base.Read(buffer);
        }
    }

    private static async Task WithControl(string path, Func<LogLocalOutput, Task> check, Encoding? encoding = null)
    {
        var control = WpfTestHost.Invoke(() => new LogLocalOutput(path, encoding));
        try
        {
            await WpfTestHost.Invoke(() => control.ReadCompletion).WaitAsync(TestTimeout);
            await check(control);
        }
        finally { WpfTestHost.Invoke(control.Dispose); }
    }

    private static string DisplayedText(LogLocalOutput control) => WpfTestHost.Invoke(() =>
    {
        var viewer = Assert.IsType<LogViewerControl>(control.FindName("LogViewer"));
        return Assert.IsType<TextBox>(viewer.FindName("PlainTextBox")).Text;
    });

    private static async Task WithLog(Func<string, string, Task> check)
    {
        DirectoryInfo root = Directory.CreateTempSubdirectory("ColorVision-LogTailTests-");
        string path = Path.Combine(root.FullName, "service.log");
        string replacement = Path.Combine(root.FullName, "replacement.log");
        const string styleKey = "SearchBarPlus";
        bool installStyle = WpfTestHost.Invoke(() =>
        {
            var resources = Application.Current.Resources;
            if (resources.Contains(styleKey)) return false;
            resources.Add(styleKey, new Style(typeof(HandyControl.Controls.SearchBar)));
            return true;
        });
        try { await check(path, replacement); }
        finally
        {
            if (installStyle) WpfTestHost.Invoke(() => Application.Current.Resources.Remove(styleKey));
            File.Delete(replacement);
            File.Delete(path);
            root.Delete();
        }
    }
}
