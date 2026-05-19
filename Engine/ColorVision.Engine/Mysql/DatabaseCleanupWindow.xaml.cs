using ColorVision.Themes;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Database
{
    public class ExportDatabaseCleanupTool : MenuItemBase
    {
        public override string OwnerGuid => nameof(ExportMySqlMenuItem);
        public override string GuidId => nameof(ExportDatabaseCleanupTool);
        public override string Header => "数据清理";
        public override int Order => 3;

        public override void Execute()
        {
            DatabaseCleanupWindow.OpenWindow();
        }
    }

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