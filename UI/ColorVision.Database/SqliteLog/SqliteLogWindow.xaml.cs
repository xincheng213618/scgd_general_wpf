using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

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

        [DisplayName("DefaultPageSize")]
        public int DefaultPageSize { get => _DefaultPageSize; set { _DefaultPageSize = value; OnPropertyChanged(); } }
        private int _DefaultPageSize = 100;

        [DisplayName("ShowDetailPanel")]
        public bool ShowDetailPanel { get => _ShowDetailPanel; set { _ShowDetailPanel = value; OnPropertyChanged(); } }
        private bool _ShowDetailPanel = true;

        [DisplayName("DetailPanelWidth")]
        public double DetailPanelWidth { get => _DetailPanelWidth; set { _DetailPanelWidth = value; OnPropertyChanged(); } }
        private double _DetailPanelWidth = 300;
    }

    /// <summary>
    /// SqliteLogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SqliteLogWindow : Window
    {
        public ObservableCollection<LogEntry> LogEntries { get; set; } = new ObservableCollection<LogEntry>();

        private int _currentPage = 1;
        private int _pageSize = 100;
        private int _totalRecords = 0;
        private int _totalPages = 1;

        // Query filters
        private string _levelFilter = "";
        private string _loggerFilter = "";
        private string _messageFilter = "";
        private DateTime? _startDate = null;
        private DateTime? _endDate = null;

        public SqliteLogWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            SqliteLogWindowConfig.Instance.SetWindow(this);
        }

        public SqliteLogManager SqliteLogManager { get; set; }
        private void Window_Initialized(object sender, EventArgs e)
        {
            SqliteLogManager = SqliteLogManager.GetInstance();

            LogDataGrid.ItemsSource = LogEntries;
            
            // Load config
            _pageSize = SqliteLogWindowConfig.Instance.DefaultPageSize;
            ToggleDetailPanel.IsChecked = SqliteLogWindowConfig.Instance.ShowDetailPanel;
            DetailColumn.Width = new GridLength(SqliteLogWindowConfig.Instance.DetailPanelWidth);
            
            // Set page size combobox
            foreach (ComboBoxItem item in PageSizeComboBox.Items)
            {
                if (item.Content.ToString() == _pageSize.ToString())
                {
                    PageSizeComboBox.SelectedItem = item;
                    break;
                }
            }
            
            LoadLogEntries();
        }

        private void LoadLogEntries()
        {
            LogEntries.Clear();

            if (!File.Exists(SqliteLogManager.SqliteDbPath))
            {
                _totalRecords = 0;
                _totalPages = 1;
                UpdatePaginationUI();
                return;
            }

            try
            {
                using var db = SqliteLogManager.CreateDbClient();

                // Build query with filters
                var query = db.Queryable<LogEntry>();
                
                if (!string.IsNullOrWhiteSpace(_levelFilter))
                    query = query.Where(x => x.Level == _levelFilter);
                
                if (!string.IsNullOrWhiteSpace(_loggerFilter))
                    query = query.Where(x => x.Logger != null && x.Logger.Contains(_loggerFilter));
                
                if (!string.IsNullOrWhiteSpace(_messageFilter))
                    query = query.Where(x => x.Message != null && x.Message.Contains(_messageFilter));
                
                if (_startDate.HasValue)
                    query = query.Where(x => x.Date >= _startDate.Value);
                
                if (_endDate.HasValue)
                    query = query.Where(x => x.Date < _endDate.Value.AddDays(1)); // Exclusive upper bound: include all logs from end date

                // Get total count for pagination
                _totalRecords = query.Count();
                _totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalRecords / _pageSize));
                
                // Ensure current page is valid
                if (_currentPage > _totalPages) _currentPage = _totalPages;
                if (_currentPage < 1) _currentPage = 1;

                // Get page data
                var entries = query
                    .OrderByDescending(x => x.Date)
                    .Skip((_currentPage - 1) * _pageSize)
                    .Take(_pageSize)
                    .ToList();

                foreach (var entry in entries)
                {
                    LogEntries.Add(entry);
                }

                UpdatePaginationUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Properties.Resources.LoadFailed}: {ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdatePaginationUI()
        {
            if (TotalCountText != null)
            {
                TotalCountText.Text = _totalRecords.ToString();
            }
            if (TotalPagesText != null)
            {
                TotalPagesText.Text = _totalPages.ToString();
            }
            if (CurrentPageTextBox != null)
            {
                CurrentPageTextBox.Text = _currentPage.ToString();
            }
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            // Collect filter values
            _levelFilter = (LevelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            _loggerFilter = LoggerTextBox.Text?.Trim() ?? "";
            _messageFilter = MessageTextBox.Text?.Trim() ?? "";
            _startDate = StartDatePicker.SelectedDate;
            _endDate = EndDatePicker.SelectedDate;
            
            // Reset to first page
            _currentPage = 1;
            LoadLogEntries();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            // Clear filters
            LevelComboBox.SelectedIndex = 0;
            LoggerTextBox.Text = "";
            MessageTextBox.Text = "";
            StartDatePicker.SelectedDate = null;
            EndDatePicker.SelectedDate = null;
            
            _levelFilter = "";
            _loggerFilter = "";
            _messageFilter = "";
            _startDate = null;
            _endDate = null;
            _currentPage = 1;
            
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
                    using var db = SqliteLogManager.CreateDbClient();
                    db.Deleteable<LogEntry>().ExecuteCommand();
                    _currentPage = 1;
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

        private void ToggleDetailPanel_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = ToggleDetailPanel.IsChecked == true;
            SqliteLogWindowConfig.Instance.ShowDetailPanel = isChecked;
            
            if (!isChecked)
            {
                // Save current width before hiding
                if (DetailColumn.Width.Value > 0)
                    SqliteLogWindowConfig.Instance.DetailPanelWidth = DetailColumn.Width.Value;
                DetailColumn.Width = new GridLength(0);
            }
            else
            {
                DetailColumn.Width = new GridLength(SqliteLogWindowConfig.Instance.DetailPanelWidth);
            }
        }

        private void DetailSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            // Save the detail panel width when user resizes it
            if (DetailColumn.Width.Value > 0)
            {
                SqliteLogWindowConfig.Instance.DetailPanelWidth = DetailColumn.Width.Value;
            }
        }

        private void LogDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LogDataGrid.SelectedItem is LogEntry entry)
            {
                DetailId.Text = entry.Id.ToString();
                DetailDate.Text = entry.Date.ToString("yyyy-MM-dd HH:mm:ss.fff");
                DetailLevel.Text = entry.Level;
                DetailThread.Text = entry.Thread;
                DetailLogger.Text = entry.Logger;
                DetailMessage.Text = entry.Message;
                DetailException.Text = entry.Exception;
            }
            else
            {
                DetailId.Text = "";
                DetailDate.Text = "";
                DetailLevel.Text = "";
                DetailThread.Text = "";
                DetailLogger.Text = "";
                DetailMessage.Text = "";
                DetailException.Text = "";
            }
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PageSizeComboBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Content.ToString(), out int pageSize))
            {
                _pageSize = pageSize;
                SqliteLogWindowConfig.Instance.DefaultPageSize = pageSize;
                _currentPage = 1;
                LoadLogEntries();
            }
        }

        private void FirstPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != 1)
            {
                _currentPage = 1;
                LoadLogEntries();
            }
        }

        private void PrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadLogEntries();
            }
        }

        private void NextPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                LoadLogEntries();
            }
        }

        private void LastPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage != _totalPages)
            {
                _currentPage = _totalPages;
                LoadLogEntries();
            }
        }

        private void CurrentPageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (int.TryParse(CurrentPageTextBox.Text, out int page))
                {
                    page = Math.Max(1, Math.Min(page, _totalPages));
                    if (page != _currentPage)
                    {
                        _currentPage = page;
                        LoadLogEntries();
                    }
                }
                else
                {
                    CurrentPageTextBox.Text = _currentPage.ToString();
                }
            }
        }
    }
}
