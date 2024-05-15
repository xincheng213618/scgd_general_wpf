using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{

    /// <summary>
    /// ChangelogWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ChangelogWindow : Window
    {
        public ObservableCollection<ChangeLogEntry> ChangeLogEntrys { get; set; }
        public ChangelogWindow()
        {
            InitializeComponent();
        }
        string? CHANGELOG { get; set; }
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            string changelogPath = "CHANGELOG.md";
            string changelogContent = File.ReadAllText(changelogPath);

            ChangeLogEntrys = Parse(changelogContent);
            ChangeLogListView.ItemsSource = ChangeLogEntrys;
            Task.Run(async () =>
            {
                CHANGELOG = await AutoUpdater.GetInstance().GetChangeLog(AutoUpdater.GetInstance().CHANGELOGUrl);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CHANGELOGStackPanel.Visibility = CHANGELOG == null ? Visibility.Collapsed : Visibility.Visible;
                });
            });
        }

        public static ObservableCollection<ChangeLogEntry> Parse(string? changelogText)
        {
            var entries = new ObservableCollection<ChangeLogEntry>();
            if (string.IsNullOrWhiteSpace(changelogText)) return entries;
            var lines = changelogText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

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

        private void Local_CHANGELOG_Click(object sender, RoutedEventArgs e)
        {
            string changelogPath = "CHANGELOG.md";
            string changelogContent = File.ReadAllText(changelogPath);
            ChangeLogEntrys = Parse(changelogContent);
            ChangeLogListView.ItemsSource = ChangeLogEntrys;
        }

        private void CHANGELOG_Click(object sender, RoutedEventArgs e)
        {
            ChangeLogEntrys = Parse(CHANGELOG);
            ChangeLogListView.ItemsSource = ChangeLogEntrys;
        }
    }
}
