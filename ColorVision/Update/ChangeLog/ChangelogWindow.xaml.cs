using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Update
{

    public class ChangelogWindowConfig : WindowConfig
    {
        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        [JsonIgnore]
        public RelayCommand OpenInExplorerCommand { get; set; }

        [JsonIgnore]
        public RelayCommand OpenInWeview2Command { get; set; }


        public ChangelogWindowConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            OpenInExplorerCommand = new RelayCommand(a => Common.Utilities.PlatformHelper.Open("CHANGELOG.md"));

            OpenInWeview2Command = new RelayCommand(a =>
            {
                if (File.Exists("CHANGELOG.md"))
                {
                    string markdown = File.ReadAllText("CHANGELOG.md");
                    string html = Markdig.Markdown.ToHtml(markdown);
                    new MarkdownViewWindow(html) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
                }
            }
            );
        }



        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }


    /// <summary>
    /// ChangelogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        public ObservableCollection<ChangeLogEntry> ChangeLogEntrys { get; set; }

        public static ChangelogWindowConfig WindowConfig => ConfigService.Instance.GetRequiredService<ChangelogWindowConfig>();

        private bool _isUpdatingSelection = false;

        public ChangelogWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            WindowConfig.SetWindow(this);
        }
        string? CHANGELOG { get; set; }
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            this.DataContext = WindowConfig;

            LoadChangeLog();

            if (ChangeLogListView.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                WindowConfig.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                WindowConfig.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
        }

        private void LoadChangeLog()
        {
            const string changelogPath = "CHANGELOG.md";
            try
            {
                if (File.Exists(changelogPath))
                {
                    string changelogContent = File.ReadAllText(changelogPath);
                    ChangeLogEntrys = Parse(changelogContent);
                    ChangeLogListView.ItemsSource = ChangeLogEntrys;
                    ChangeLogDetailsPanel.ItemsSource = ChangeLogEntrys;
                    
                    // Attach scroll event after items are loaded
                    ChangeLogDetailsPanel.Loaded += ChangeLogDetailsPanel_Loaded;
                }
                else
                {
                    MessageBox.Show("无法找到更新记录");
                    ChangeLogEntrys = new ObservableCollection<ChangeLogEntry>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取更新记录失败: " + ex.Message);
                ChangeLogEntrys = new ObservableCollection<ChangeLogEntry>();
            }
        }

        private ScrollViewer _detailScrollViewer;

        private void ChangeLogDetailsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (_detailScrollViewer == null)
            {
                _detailScrollViewer = FindScrollViewer(ChangeLogDetailsPanel);
                if (_detailScrollViewer != null)
                {
                    _detailScrollViewer.ScrollChanged += DetailScrollViewer_ScrollChanged;
                }
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            return null;
        }

        private void DetailScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            // Find the first visible item in the detail panel
            if (ChangeLogDetailsPanel.ItemsSource is IEnumerable<ChangeLogEntry> items)
            {
                ChangeLogEntry firstVisibleItem = null;
                double minDistance = double.MaxValue;

                foreach (var item in items)
                {
                    var container = ChangeLogDetailsPanel.ItemContainerGenerator.ContainerFromItem(item);
                    if (container is FrameworkElement element)
                    {
                        var transform = element.TransformToAncestor(_detailScrollViewer);
                        var position = transform.Transform(new Point(0, 0));
                        
                        // Check if item is visible in viewport
                        if (position.Y >= 0 && position.Y < _detailScrollViewer.ViewportHeight)
                        {
                            if (position.Y < minDistance)
                            {
                                minDistance = position.Y;
                                firstVisibleItem = item;
                            }
                        }
                    }
                }

                if (firstVisibleItem != null && ChangeLogListView.SelectedItem != firstVisibleItem)
                {
                    _isUpdatingSelection = true;
                    try
                    {
                        ChangeLogListView.SelectedItem = firstVisibleItem;
                        ChangeLogListView.ScrollIntoView(firstVisibleItem);
                    }
                    finally
                    {
                        _isUpdatingSelection = false;
                    }
                }
            }
        }

        private void DetailItem_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ChangeLogEntry entry)
            {
                _isUpdatingSelection = true;
                try
                {
                    ChangeLogListView.SelectedItem = entry;
                    ChangeLogListView.ScrollIntoView(entry);
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
        }

        private void ChangeLogListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection) return;

            if (sender is ListView listView && listView.SelectedIndex > -1 && listView.SelectedItem is ChangeLogEntry selectedEntry)
            {
                _isUpdatingSelection = true;
                try
                {
                    // Scroll the details panel to the selected entry
                    var container = ChangeLogDetailsPanel.ItemContainerGenerator.ContainerFromItem(selectedEntry);
                    if (container is FrameworkElement element)
                    {
                        element.BringIntoView();
                    }
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ChangeLogListView.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ChangeLogEntry);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ChangeLogEntrys.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }
        }



        private static char[] chars = new char[] { '\n' };
        public static ObservableCollection<ChangeLogEntry> Parse(string? changelogText)
        {
            var entries = new ObservableCollection<ChangeLogEntry>();
            if (string.IsNullOrWhiteSpace(changelogText)) return entries;
            var lines = changelogText.Split(chars, StringSplitOptions.RemoveEmptyEntries);

            ChangeLogEntry currentEntry = null;

            foreach (var line in lines)
                {
                if (line.StartsWith("## [",StringComparison.CurrentCulture))
                {
                    if (currentEntry != null)
                    {
                        entries.Add(currentEntry);
                    }

                    currentEntry = new ChangeLogEntry();

                    var headerParts = line.TrimStart('#').Trim().TrimStart('[').TrimEnd(']').Split(']');
                    currentEntry.Version = headerParts[0].Trim();
                    currentEntry.ReleaseDate = DateTime.Parse(headerParts[1].Trim());
                }
                else if (currentEntry != null && Regex.IsMatch(line, @"^\d+\.", RegexOptions.CultureInvariant))
                    {
                    currentEntry?.Changes.Add(line.Trim());
                }
            }
            // Add the last entry if exists
            if (currentEntry != null)
            {
                entries.Add(currentEntry);
            }

            return entries;
        }

        private void Searchbox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }
        private readonly char[] Chars1 = new[] { ' ' };

        public List<ChangeLogEntry> filteredResults { get; set; } = new List<ChangeLogEntry>();
        
        private CancellationTokenSource _searchCts;
        private const int SearchDebounceMs = 300;

        private async void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Cancel any pending search
                _searchCts?.Cancel();
                _searchCts = new CancellationTokenSource();
                var token = _searchCts.Token;

                try
                {
                    // Debounce: wait for user to stop typing
                    await Task.Delay(SearchDebounceMs, token);

                    if (string.IsNullOrWhiteSpace(textBox.Text))
                    {
                        ChangeLogListView.ItemsSource = ChangeLogEntrys;
                        ChangeLogDetailsPanel.ItemsSource = ChangeLogEntrys;
                    }
                    else if (ChangeLogEntrys != null)
                    {
                        var keywords = textBox.Text.Split(Chars1, StringSplitOptions.RemoveEmptyEntries);

                        filteredResults = ChangeLogEntrys
                            .Where(entry => keywords.All(keyword =>
                                (!string.IsNullOrEmpty(entry.Version) && entry.Version.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                                entry.ReleaseDate.ToString("yyyy-MM-dd").Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                                entry.ChangeLog.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            ))
                            .ToList();

                        ChangeLogListView.ItemsSource = filteredResults;
                        ChangeLogDetailsPanel.ItemsSource = filteredResults;
                    }
                }
                catch (TaskCanceledException)
                {
                    // Search was cancelled, ignore
                }
            }
        }
    }
}
