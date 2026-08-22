using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Draw.Ruler;
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ColorVision.UI.Tests;

public class DrawingVisualScaleHostTests
{
    [Fact]
    public void DefaultTextNotificationIsForwardedWithoutKeepingHostAlive()
    {
        RunOnStaThread(() =>
        {
            FieldInfo defaultField = typeof(DefalutTextAttribute).GetField(
                "_defalut",
                BindingFlags.Static | BindingFlags.NonPublic)!;
            IConfigService? originalService = ConfigService.Instance;
            object? originalFallback = defaultField.GetValue(null);
            DefalutTextAttribute source = new();
            try
            {
                ConfigService.SetInstance(null!);
                defaultField.SetValue(null, source);
                WeakReference hostReference = CreateHostAndVerifyNotification(source);

                CollectGarbage();

                Assert.False(hostReference.IsAlive);
                GC.KeepAlive(source);
            }
            finally
            {
                defaultField.SetValue(null, originalFallback);
                ConfigService.SetInstance(originalService!);
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateHostAndVerifyNotification(DefalutTextAttribute source)
    {
        DrawingVisualScaleHost host = new();
        bool wasNotified = false;
        PropertyChangedEventHandler handler = (_, e) => wasNotified = e.PropertyName == nameof(DrawingVisualScaleHost.IsUsePhysicalUnit);
        host.PropertyChanged += handler;

        source.IsUsePhysicalUnit = !source.IsUsePhysicalUnit;

        Assert.True(wasNotified);
        return new WeakReference(host);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void CollectGarbage()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
