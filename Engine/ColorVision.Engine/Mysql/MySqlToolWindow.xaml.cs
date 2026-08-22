using ColorVision.Themes;
using ColorVision.UI.Menus;
using ColorVision.Engine;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DatabaseResources = ColorVision.Database.Properties.Resources;
using EngineResources = ColorVision.Engine.Properties.Resources;

namespace ColorVision.Database
{
    public sealed class SqlFileNameWithoutExtensionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string fileName)
                return string.Empty;

            return string.Equals(Path.GetExtension(fileName), ".sql", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileNameWithoutExtension(fileName)
                : fileName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    public class ExportMySqlTool : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string GuidId => nameof(ExportMySqlTool);
        public override string Header => ColorVision.UI.Properties.Resources.MysqlTool;
        public override int Order => 20;

        public override void Execute()
        {
            new MySqlToolWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    /// <summary>
    /// MySqlToolWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MySqlToolWindow : Window
    {
        private ICollectionView? _backupsView;
        private GridViewColumnHeader? _sortedHeader;
        private object? _sortedHeaderContent;
        private ListSortDirection _sortDirection = ListSortDirection.Descending;

        public static MySqlControl MySqlControl => MySqlControl.GetInstance();
        public MySqlToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, System.EventArgs e)
        {
            var manager = MySqlLocalServicesManager.GetInstance();
            this.DataContext = manager;

            _backupsView = new ListCollectionView(manager.Backups);
            _backupsView.Filter = item => item is MysqlBack backup && IsSqlBackup(backup.FilePath);
            listView1.ItemsSource = _backupsView;
            ApplySort(CreationTimeColumnHeader, nameof(MysqlBack.CreationTime), ListSortDirection.Descending);

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, (s, e) => 
            {
                if (listView1.SelectedItem is not MysqlBack selectedBackup)
                    return;

                var selectedFilePath = selectedBackup.FilePath;
                StringCollection paths = new();
                paths.Add(selectedFilePath);
                Clipboard.SetFileDropList(paths);

            }, (s, e) => { e.CanExecute = listView1.SelectedItem is MysqlBack; }));

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => 
            {
                if (listView1.SelectedItem is not MysqlBack selectedBackup)
                    return;

                manager.Backups.Remove(selectedBackup);
                File.Delete(selectedBackup.FilePath);
            }, (s, e) => { e.CanExecute = listView1.SelectedItem is MysqlBack; }));
        }

        private static bool IsSqlBackup(string filePath)
        {
            return string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase);
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not GridViewColumnHeader header || header.Tag is not string propertyName)
                return;

            var direction = _sortedHeader == header && _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            ApplySort(header, propertyName, direction);
        }

        private void ApplySort(GridViewColumnHeader header, string propertyName, ListSortDirection direction)
        {
            if (_backupsView == null)
                return;

            if (_sortedHeader != null)
                _sortedHeader.Content = _sortedHeaderContent;

            _backupsView.SortDescriptions.Clear();
            _backupsView.SortDescriptions.Add(new SortDescription(propertyName, direction));

            _sortedHeader = header;
            _sortedHeaderContent = header.Content;
            _sortDirection = direction;
            header.Content = $"{_sortedHeaderContent} {(direction == ListSortDirection.Ascending ? "▲" : "▼")}";
        }

        private void RenameBackup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement { Tag: MysqlBack backup })
                RenameBackup(backup);
        }

        private void ListView1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F2 || listView1.SelectedItem is not MysqlBack backup)
                return;

            e.Handled = true;
            RenameBackup(backup);
        }

        private void RenameBackup(MysqlBack backup)
        {
            string proposedName = Path.GetFileNameWithoutExtension(backup.Name);
            while (true)
            {
                string? input = ShowRenameDialog(proposedName);
                if (input == null)
                    return;

                if (!TryGetRenameTarget(backup, input, out string targetPath, out string validationError))
                {
                    MessageBox.Show(this, validationError, EngineLocalization.Get("重命名备份"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    proposedName = input;
                    continue;
                }

                string sourcePath = Path.GetFullPath(backup.FilePath);
                if (string.Equals(sourcePath, targetPath, StringComparison.Ordinal))
                    return;

                try
                {
                    File.Move(sourcePath, targetPath);
                    backup.FilePath = targetPath;
                    backup.Name = Path.GetFileName(targetPath);
                    _backupsView?.Refresh();
                    listView1.SelectedItem = backup;
                    listView1.ScrollIntoView(backup);
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    MessageBox.Show(this, EngineLocalization.Format($"重命名失败：{ex.Message}"), EngineLocalization.Get("重命名备份"), MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
        }

        private static bool TryGetRenameTarget(MysqlBack backup, string input, out string targetPath, out string error)
        {
            targetPath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = EngineLocalization.Get("备份名称不能为空。");
                return false;
            }

            if (!string.Equals(input, input.Trim(), StringComparison.Ordinal))
            {
                error = EngineLocalization.Get("备份名称不能以空格开头或结尾。");
                return false;
            }

            string name = input;
            if (name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                name = name[..^4];

            if (string.IsNullOrWhiteSpace(name))
            {
                error = EngineLocalization.Get("备份名称不能为空。");
                return false;
            }

            if (name.Length + ".sql".Length > 255)
            {
                error = EngineLocalization.Get("备份名称过长，请缩短后重试。");
                return false;
            }

            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.EndsWith('.') || name.EndsWith(' '))
            {
                error = EngineLocalization.Get("备份名称包含 Windows 文件名不允许的字符或结尾。");
                return false;
            }

            string reservedName = name.Split('.')[0];
            string[] reservedNames = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
            if (reservedNames.Contains(reservedName, StringComparer.OrdinalIgnoreCase))
            {
                error = EngineLocalization.Get("该名称是 Windows 保留文件名，请使用其他名称。");
                return false;
            }

            try
            {
                var manager = MySqlLocalServicesManager.GetInstance();
                string backupDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(manager.BackupPath));
                string sourcePath = Path.GetFullPath(backup.FilePath);
                if (!File.Exists(sourcePath) || !IsSqlBackup(sourcePath) ||
                    !string.Equals(Path.GetDirectoryName(sourcePath), backupDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    error = EngineLocalization.Get("只能重命名备份目录中的 SQL 文件。");
                    return false;
                }

                string candidateTargetPath = Path.GetFullPath(Path.Combine(backupDirectory, $"{name}.sql"));
                if (!IsSqlBackup(candidateTargetPath) ||
                    !string.Equals(Path.GetDirectoryName(candidateTargetPath), backupDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    error = EngineLocalization.Get("目标文件名超出了备份目录。");
                    return false;
                }

                bool isSameFile = string.Equals(sourcePath, candidateTargetPath, StringComparison.OrdinalIgnoreCase);
                if (!isSameFile && (File.Exists(candidateTargetPath) || manager.Backups.Any(item =>
                    !ReferenceEquals(item, backup) &&
                    string.Equals(Path.GetFullPath(item.FilePath), candidateTargetPath, StringComparison.OrdinalIgnoreCase))))
                {
                    error = EngineLocalization.Get("同名备份已经存在，请使用其他名称。");
                    return false;
                }

                targetPath = candidateTargetPath;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or PathTooLongException)
            {
                error = EngineLocalization.Format($"备份名称无效：{ex.Message}");
                return false;
            }
        }

        private string? ShowRenameDialog(string currentName)
        {
            var dialog = new Window
            {
                Owner = this,
                Title = EngineLocalization.Get("重命名备份"),
                ShowInTaskbar = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = TryFindResource("GlobalBackground") as System.Windows.Media.Brush
            };

            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock { Text = EngineLocalization.Get("备份名称（无需输入 .sql）："), Margin = new Thickness(0, 0, 0, 8) });
            var textBox = new TextBox { Text = currentName, MinWidth = 360, Margin = new Thickness(0, 0, 0, 12) };
            panel.Children.Add(textBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okButton = new Button { Content = EngineLocalization.Get("确定"), IsDefault = true, MinWidth = 72, Margin = new Thickness(0, 0, 8, 0) };
            okButton.Click += (_, _) => dialog.DialogResult = true;
            buttons.Children.Add(okButton);
            buttons.Children.Add(new Button { Content = EngineLocalization.Get("取消"), IsCancel = true, MinWidth = 72 });
            panel.Children.Add(buttons);

            dialog.Content = panel;
            dialog.ApplyCaption();
            dialog.Loaded += (_, _) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            return dialog.ShowDialog() == true ? textBox.Text : null;
        }

        private void OpenCleanupWindow_Click(object sender, RoutedEventArgs e)
        {
            DatabaseCleanupWindow.OpenWindow();
        }

        private async void InitializeTables_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                $"{DatabaseResources.MenuMySqlInitTables}\r\n\r\n{EngineResources.ResetDatabasePrompt}",
                EngineResources.Engine_Msg_ConfirmResetTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
                return;

            InitializeTablesButton.IsEnabled = false;
            try
            {
                await MySqlTableInitializer.InitializeWithNotificationAsync(this);
            }
            finally
            {
                InitializeTablesButton.IsEnabled = true;
            }
        }

    }
}
