using ColorVision.Themes;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace WindowsServicePlugin.ServiceManager
{
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "The window releases log capture in OnClosed.")]
    public partial class ServiceManagerWindow : Window
    {
        public ServiceManagerWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            DataContext = ServiceManagerViewModel.Instance;
            OperationLog.StartCapture();
        }

        protected override void OnClosed(EventArgs e)
        {
            OperationLog.Dispose();
            base.OnClosed(e);
        }

        private void OpenActionsMenu(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button)
            {
                menu.PlacementTarget = button;
                menu.Placement = PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void ClearCurrentLogRecords_Click(object sender, RoutedEventArgs e)
        {
            OperationLog.Clear();
        }

        private async void ClearAllServiceLogs_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not ServiceManagerViewModel viewModel || viewModel.IsBusy)
                return;

            if (await viewModel.ClearAllServiceLogsAsync().ConfigureAwait(true))
                OperationLog.Clear();
        }
    }
}
