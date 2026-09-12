using System.ComponentModel;
using System.Windows;

namespace ColorVision;

public partial class MainWindow
{
    private void ConfigureFullScreenLayout()
    {
        Thickness? windowedMargin = null;
        void OnFullScreenChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MainWindowConfig.IsFull))
                return;
            if (Config.IsFull)
            {
                windowedMargin ??= DockingManager1.Margin;
                DockingManager1.SetCurrentValue(MarginProperty, new Thickness(0));
            }
            else if (windowedMargin is Thickness margin)
            {
                DockingManager1.SetCurrentValue(MarginProperty, margin);
                windowedMargin = null;
            }
        }
        Config.PropertyChanged += OnFullScreenChanged;
        Closed += (_, _) => Config.PropertyChanged -= OnFullScreenChanged;
    }
}
