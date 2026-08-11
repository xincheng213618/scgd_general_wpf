using ColorVision.UI.Extension;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.Tests;

public class ThemeSubscriptionLifecycleTests
{
    [Fact]
    public void IconThemeSubscription_DoesNotKeepIconOwnerAlive()
    {
        WeakReference targetReference = WpfTestHost.Invoke(CreateIconThemeSubscription);

        CollectGarbage();

        Assert.False(targetReference.IsAlive);
    }

    [Fact]
    public void DisplayThemeSubscription_DoesNotKeepDisplayOrBorderAlive()
    {
        (WeakReference displayReference, WeakReference borderReference) = WpfTestHost.Invoke(CreateDisplayThemeSubscription);

        CollectGarbage();

        Assert.False(displayReference.IsAlive);
        Assert.False(borderReference.IsAlive);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateIconThemeSubscription()
    {
        const string resourceKey = "ThemeSubscriptionLifecycleTests.Icon";
        Application.Current.Resources[resourceKey] = new DrawingImage();
        var target = new TestIcon();

        target.SetIconResource(resourceKey);

        Assert.NotNull(target.Icon);
        return new WeakReference(target);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Display, WeakReference Border) CreateDisplayThemeSubscription()
    {
        Application.Current.Resources["GlobalBorderBrush1"] = Brushes.Gray;
        var display = new TestDisplayControl();
        var border = new Border();

        display.ApplyChangedSelectedColor(border);

        Assert.Same(Brushes.Gray, border.BorderBrush);
        return (new WeakReference(display), new WeakReference(border));
    }

    private static void CollectGarbage()
    {
        for (int i = 0; i < 3; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

    private sealed class TestIcon : IIcon
    {
        public ImageSource Icon { get; set; } = null!;
    }

    private sealed class TestDisplayControl : IDisPlayControl
    {
        public event RoutedEventHandler? Selected;
        public event RoutedEventHandler? Unselected;
        public event EventHandler? SelectChanged;

        public bool IsSelected
        {
            get => field;
            set
            {
                if (field == value)
                    return;

                field = value;
                SelectChanged?.Invoke(this, EventArgs.Empty);
                if (value)
                    Selected?.Invoke(this, new RoutedEventArgs());
                else
                    Unselected?.Invoke(this, new RoutedEventArgs());
            }
        }

        public string DisPlayName => "Theme lifecycle test";
    }
}
