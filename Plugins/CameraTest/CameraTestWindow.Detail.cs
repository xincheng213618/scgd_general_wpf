using CameraTest.Application;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CameraTest;

public partial class CameraTestWindow
{
    private MeasurementDetailWindow? _measurementDetail;

    private void Result_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not DataGrid grid || e.OriginalSource is not DependencyObject source
            || ItemsControl.ContainerFromElement(grid, source) is not DataGridRow) return;
        ShowSelectedMeasurementDetail();
        e.Handled = true;
    }

    private void Result_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None) return;
        ShowSelectedMeasurementDetail();
        e.Handled = true;
    }

    private MeasurementDetailWindow? CreateSelectedMeasurementDetail()
    {
        if (_closing || _frame == null || _result == null || _result.FrameId != _frame.Id || Metrics.SelectedItem is not MetricRow row) return null;
        var snapshot = new MeasurementDetailSnapshot(_frame, _result, row.Target, row.Edge, _profile.Display.Frequency);
        return new(snapshot, row.Channel) { Owner = this };
    }

    private void ShowSelectedMeasurementDetail()
    {
        if (CreateSelectedMeasurementDetail() is not { } window) return;
        _measurementDetail?.Close();
        _measurementDetail = window;
        window.Closed += (_, _) => { if (ReferenceEquals(_measurementDetail, window)) _measurementDetail = null; };
        window.Show();
    }
}
