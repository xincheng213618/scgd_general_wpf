using ColorVision.Themes;
using log4net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class StartWindowThemeLifecycleTests
{
    private static readonly FieldInfo SystemThemeChangedField = typeof(ThemeManager).GetField(
        nameof(ThemeManager.SystemThemeChanged),
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("SystemThemeChanged backing field was not found.");
    private static readonly FieldInfo CurrentUiThemeChangedField = typeof(ThemeManager).GetField(
        nameof(ThemeManager.CurrentUIThemeChanged),
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("CurrentUIThemeChanged backing field was not found.");

    [Fact]
    public void CloseRestoresSystemThemeSubscriberCount()
    {
        WpfTestHost.Invoke(() =>
        {
            ThemeManager publisher = ThemeManager.Current;
            int subscriberCountBefore = GetSystemThemeSubscriberCount(publisher);
            int uiSubscriberCountBefore = GetCurrentUiThemeSubscriberCount(publisher);
            ProgramTimer.Start();
            InitAppender startupAppender = ProgramTimer.InitAppender;
            var retainedAppenders = LogManager.GetRepository().GetAppenders().Where(appender => appender != startupAppender).ToArray();
            Window? previousMainWindow = Application.Current.MainWindow;
            StartWindow? window = null;
            try
            {
                window = new StartWindow();
                Assert.Equal(subscriberCountBefore + 1, GetSystemThemeSubscriberCount(publisher));
                Assert.Equal(uiSubscriberCountBefore + 1, GetCurrentUiThemeSubscriberCount(publisher));
                Assert.DoesNotContain(startupAppender, LogManager.GetRepository().GetAppenders());
                Assert.Empty(startupAppender.Buffer.ToString());
                foreach (var appender in retainedAppenders)
                    Assert.Contains(appender, LogManager.GetRepository().GetAppenders());
            }
            finally
            {
                try { window?.Close(); }
                finally { Application.Current.MainWindow = previousMainWindow; }
            }

            Assert.Equal(subscriberCountBefore, GetSystemThemeSubscriberCount(publisher));
            Assert.Equal(uiSubscriberCountBefore, GetCurrentUiThemeSubscriberCount(publisher));
        });
    }

    [Fact]
    public void ClosedStartWindowCanBeCollected()
    {
        WeakReference windowReference = WpfTestHost.Invoke(CreateClosedStartWindowReference);

        CollectGarbage();

        Assert.False(windowReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateClosedStartWindowReference()
    {
        ProgramTimer.Start();
        Window? previousMainWindow = Application.Current.MainWindow;
        StartWindow? window = null;
        try { window = new StartWindow(); }
        finally
        {
            try { window?.Close(); }
            finally { Application.Current.MainWindow = previousMainWindow; }
        }
        return new WeakReference(window);
    }

    private static int GetSystemThemeSubscriberCount(ThemeManager publisher) =>
        (SystemThemeChangedField.GetValue(publisher) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

    private static int GetCurrentUiThemeSubscriberCount(ThemeManager publisher) =>
        (CurrentUiThemeChangedField.GetValue(publisher) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

    private static void CollectGarbage()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}
