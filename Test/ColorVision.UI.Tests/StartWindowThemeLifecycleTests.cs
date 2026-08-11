using ColorVision.Themes;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class StartWindowThemeLifecycleTests
{
    private static readonly FieldInfo SystemThemeChangedField = typeof(ThemeManager).GetField(
        nameof(ThemeManager.SystemThemeChanged),
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("SystemThemeChanged backing field was not found.");
    private static readonly MethodInfo DetachStartupAppenderMethod = typeof(StartWindow).GetMethod(
        "DetachStartupAppender",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DetachStartupAppender was not found.");

    [Fact]
    public void CloseRestoresSystemThemeSubscriberCount()
    {
        WpfTestHost.Invoke(() =>
        {
            ThemeManager publisher = ThemeManager.Current;
            int subscriberCountBefore = GetSystemThemeSubscriberCount(publisher);
            ProgramTimer.Start();
            var window = new StartWindow();

            Assert.Equal(subscriberCountBefore + 1, GetSystemThemeSubscriberCount(publisher));

            DetachStartupAppenderMethod.Invoke(window, null);
            window.Close();

            Assert.Equal(subscriberCountBefore, GetSystemThemeSubscriberCount(publisher));
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
        var window = new StartWindow();
        DetachStartupAppenderMethod.Invoke(window, null);
        window.Close();
        return new WeakReference(window);
    }

    private static int GetSystemThemeSubscriberCount(ThemeManager publisher) =>
        (SystemThemeChangedField.GetValue(publisher) as MulticastDelegate)?.GetInvocationList().Length ?? 0;

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
