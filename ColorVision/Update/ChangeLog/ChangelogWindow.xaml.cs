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
using System.Windows.Threading;

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
        public ObservableCollection<MajorVersionNode> VersionTree { get; set; } = new ObservableCollection<MajorVersionNode>();

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
                    
                    // Build hierarchical tree structure
                    BuildVersionTree();
                    
                    ChangeLogTreeView.ItemsSource = VersionTree;
                    ChangeLogDetailsPanel.ItemsSource = ChangeLogEntrys;
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

        private void ChangeLogListView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (sender is TreeView treeView && treeView.SelectedItem is ChangeLogEntry selectedEntry)
            {
                if (ChangeLogDetailsPanel.SelectedItem != selectedEntry)
                {
                    ChangeLogDetailsPanel.SelectedItem = selectedEntry;
                    ChangeLogDetailsPanel.ScrollIntoView(selectedEntry);
                }
            }
        }

        private void ChangeLogDetailsPanel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1 && listView.SelectedItem is ChangeLogEntry selectedEntry)
            {
                try
                {
                    SelectEntryInTreeView(selectedEntry);
                }
                finally
                {
                    _isUpdatingSelection = false;
                }
            }
        }

        /// <summary>
        /// Recursively find and select an entry in the TreeView
        /// </summary>
        private void SelectEntryInTreeView(ChangeLogEntry entry)
        {
            foreach (var majorNode in VersionTree)
            {
                foreach (var minorNode in majorNode.MinorVersions)
                {
                    var foundEntry = minorNode.Entries.FirstOrDefault(e => e.Version == entry.Version);
                    if (foundEntry != null)
                    {
                        // Expand parent nodes
                        majorNode.IsExpanded = true;
                        minorNode.IsExpanded = true;
                        
                        // Select the entry
                        foundEntry.IsSelected = true;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            var treeViewItem = GetTreeViewItem(ChangeLogTreeView, foundEntry);
                            if (treeViewItem != null)
                            {
                                treeViewItem.BringIntoView();
                            }
                        }), DispatcherPriority.Loaded);
                        return;
                    }
                }
            }
        }

        public static TreeViewItem GetTreeViewItem(TreeView treeView, object item)
        {
            return (TreeViewItem)treeView.ItemContainerGenerator.ContainerFromItem(item)
                ?? GetTreeViewItemRecursive(treeView, item);
        }

        private static TreeViewItem GetTreeViewItemRecursive(ItemsControl parent, object item)
        {
            foreach (object child in parent.Items)
            {
                TreeViewItem childItem = parent.ItemContainerGenerator.ContainerFromItem(child) as TreeViewItem;
                if (childItem != null)
                {
                    if (child == item)
                        return childItem;
                    TreeViewItem result = GetTreeViewItemRecursive(childItem, item);
                    if (result != null)
                        return result;
                }
            }
            return null;
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            // Context menu for TreeView - can be extended in future if needed
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            // Sorting is now handled by tree structure grouping
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

        /// <summary>
        /// Parse ServiceVersion string to comparable tuple
        /// </summary>
        private (int major, int minor, int build, int revision) ParseVersion(string version)
        {
            var parts = version.Split('.');
            int major = parts.Length > 0 && int.TryParse(parts[0], out int m) ? m : 0;
            int minor = parts.Length > 1 && int.TryParse(parts[1], out int mi) ? mi : 0;
            int build = parts.Length > 2 && int.TryParse(parts[2], out int b) ? b : 0;
            int revision = parts.Length > 3 && int.TryParse(parts[3], out int r) ? r : 0;
            return (major, minor, build, revision);
        }

        /// <summary>
        /// Build hierarchical tree structure from flat changelog entries
        /// </summary>
        private void BuildVersionTree()
        {
            VersionTree.Clear();

            // Group by major ServiceVersion
            var majorGroups = ChangeLogEntrys
                .GroupBy(entry =>
                {
                    var versionParts = entry.Version.Split('.');
                    return int.TryParse(versionParts[0], out int major) ? major : 0;
                })
                .OrderByDescending(g => g.Key);

            int latestMajor = majorGroups.FirstOrDefault()?.Key ?? 0;
            int latestMinor = -1;

            foreach (var majorGroup in majorGroups)
            {
                var majorNode = new MajorVersionNode
                {
                    MajorVersion = majorGroup.Key,
                    DisplayName = $"{majorGroup.Key}.x.x.x ({majorGroup.Count()} versions)",
                    IsExpanded = majorGroup.Key == latestMajor // Expand latest major ServiceVersion
                };

                // Group by minor ServiceVersion within major
                var minorGroups = majorGroup
                    .GroupBy(entry =>
                    {
                        var versionParts = entry.Version.Split('.');
                        return versionParts.Length > 1 && int.TryParse(versionParts[1], out int minor) ? minor : 0;
                    })
                    .OrderByDescending(g => g.Key);

                // Get latest minor ServiceVersion for the latest major ServiceVersion
                if (majorGroup.Key == latestMajor)
                {
                    latestMinor = minorGroups.FirstOrDefault()?.Key ?? -1;
                }

                foreach (var minorGroup in minorGroups)
                {
                    var minorNode = new MinorVersionNode
                    {
                        MajorVersion = majorGroup.Key,
                        MinorVersion = minorGroup.Key,
                        DisplayName = $"{majorGroup.Key}.{minorGroup.Key}.x.x ({minorGroup.Count()} versions)",
                        // Expand latest minor ServiceVersion within latest major ServiceVersion
                        IsExpanded = majorGroup.Key == latestMajor && minorGroup.Key == latestMinor
                    };

                    // Add individual entries, sorted numerically by ServiceVersion
                    foreach (var entry in minorGroup.OrderByDescending(e => ParseVersion(e.Version)))
                    {
                        minorNode.Entries.Add(entry);
                    }

                    majorNode.MinorVersions.Add(minorNode);
                }
                majorNode.MinorVersions[0].Entries[0].IsSelected = true;

                VersionTree.Add(majorNode);
            }
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
                        // Reset to full tree
                        BuildVersionTree();
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

                        // Rebuild tree with filtered results
                        var tempEntries = ChangeLogEntrys;
                        ChangeLogEntrys = new ObservableCollection<ChangeLogEntry>(filteredResults);
                        BuildVersionTree();
                        ChangeLogEntrys = tempEntries;
                        
                        ChangeLogDetailsPanel.ItemsSource = filteredResults;
                        
                        // Expand all nodes in search results
                        foreach (var majorNode in VersionTree)
                        {
                            majorNode.IsExpanded = true;
                            foreach (var minorNode in majorNode.MinorVersions)
                            {
                                minorNode.IsExpanded = true;
                            }
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    // Search was cancelled, ignore
                }
            }
        }

        private void TextBox_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is ChangeLogEntry selectedEntry)
            {
                if (ChangeLogDetailsPanel.SelectedItem != selectedEntry)
                {
                    ChangeLogDetailsPanel.SelectedItem = selectedEntry;
                }
            }
        }
    }
}
