using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Database
{
    public partial class DatabaseCleanupWindow : Window
    {
        private static DatabaseCleanupWindow? _instance;

        public DatabaseCleanupWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        public static void OpenWindow()
        {
            if (_instance != null)
            {
                if (_instance.WindowState == WindowState.Minimized)
                {
                    _instance.WindowState = WindowState.Normal;
                }

                _instance.Activate();
                return;
            }

            _instance = new DatabaseCleanupWindow
            {
                Owner = WindowHelpers.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            _instance.Closed += (_, _) => _instance = null;
            _instance.Show();
        }

        private async void Window_Initialized(object sender, System.EventArgs e)
        {
            var viewModel = new DatabaseCleanupWindowViewModel();
            DataContext = viewModel;
            await viewModel.RefreshAllAsync();
        }
    }
}
