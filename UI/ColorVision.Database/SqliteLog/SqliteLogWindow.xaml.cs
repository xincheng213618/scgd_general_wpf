using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Database.SqliteLog
{
    public class ExportSqliteLogWindow : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string GuidId => "SqliteLogWindow";
        public override string Header => Properties.Resources.SqliteLogWindow;
        public override int Order => 21;

        public override void Execute()
        {
            new SqliteLogWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }
    }

    public class SqliteLogWindowConfig : WindowConfig
    {
        public static SqliteLogWindowConfig Instance => ConfigService.Instance.GetRequiredService<SqliteLogWindowConfig>();
    }

    /// <summary>
    /// SqliteLogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SqliteLogWindow : Window
    {
        public ObservableCollection<LogEntry> LogEntries { get; set; } = new ObservableCollection<LogEntry>();

        public SqliteLogWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            SqliteLogWindowConfig.Instance.SetWindow(this);
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            LogDataGrid.ItemsSource = LogEntries;
            LoadLogEntries();
        }

        private void LoadLogEntries()
        {
            LogEntries.Clear();

            if (!File.Exists(SqliteLogManager.SqliteDbPath))
            {
                TotalCountText.Text = "0";
                return;
            }

            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = $"Data Source={SqliteLogManager.SqliteDbPath}",
                    DbType = DbType.Sqlite,
                    IsAutoCloseConnection = true
                });

                var entries = db.Queryable<LogEntry>()
                    .OrderByDescending(x => x.Date)
                    .Take(1000)
                    .ToList();

                foreach (var entry in entries)
                {
                    LogEntries.Add(entry);
                }

                TotalCountText.Text = db.Queryable<LogEntry>().Count().ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.LoadFailed}: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadLogEntries();
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(Properties.Resources.ClearCacheConfirm, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                if (File.Exists(SqliteLogManager.SqliteDbPath))
                {
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = $"Data Source={SqliteLogManager.SqliteDbPath}",
                        DbType = DbType.Sqlite,
                        IsAutoCloseConnection = true
                    });

                    db.Deleteable<LogEntry>().ExecuteCommand();
                    LoadLogEntries();
                    MessageBox.Show(Properties.Resources.ClearCacheSuccess, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.ClearCacheFailed}: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = SqliteLogManager.DirectoryPath;
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            PlatformHelper.OpenFolder(folderPath);
        }
    }
}
