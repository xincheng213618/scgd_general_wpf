using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Controls;

public partial class FlowExecutionStatus : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(FlowExecutionStatusInfo), typeof(FlowExecutionStatus), new PropertyMetadata(FlowExecutionStatusInfo.Idle));

    public FlowExecutionStatusInfo Status
    {
        get => (FlowExecutionStatusInfo)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public FlowExecutionStatus()
    {
        InitializeComponent();
        Unloaded += (_, _) => DetailsPopup.IsOpen = false;
    }

    private void LayoutRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ElapsedText.Visibility = e.NewSize.Width < 440 ? Visibility.Collapsed : Visibility.Visible;
        DetailsSurface.Width = Math.Min(460, Math.Max(240, e.NewSize.Width));
    }

    private void Details_Click(object sender, RoutedEventArgs e) => DetailsPopup.IsOpen = !DetailsPopup.IsOpen;
    private void CloseDetails_Click(object sender, RoutedEventArgs e) => DetailsPopup.IsOpen = false;
    private void DetailsPopup_Opened(object sender, EventArgs e)
    {
        CopyButton.Content = "复制";
        DetailsText.Focus();
    }
    private void DetailsPopup_Closed(object sender, EventArgs e) => DetailsButton.Focus();
    private void Details_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DetailsPopup.IsOpen = false;
        e.Handled = true;
    }
    private void CopyDetails_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(Status.Details);
            CopyButton.Content = "已复制";
        }
        catch (ExternalException)
        {
            CopyButton.Content = "重试复制";
            DetailsText.Focus();
            DetailsText.SelectAll();
        }
    }
}
