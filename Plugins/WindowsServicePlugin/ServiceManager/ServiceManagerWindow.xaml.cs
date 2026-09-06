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
    }
}
