using ColorVision.Common.MVVM;
using ColorVision.Update;
using ColorVision.Common.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public class ChangeLogEntry : ViewModelBase
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> Changes { get; set; }
        public string ChangeLog { get => string.Join("\n", Changes);}

        public RelayCommand UpdateCommand { get; set; }

        public bool IsUpdateAvailable => AutoUpdater.IsUpdateAvailable(Version);

        public bool IsCurrentVision  =>  Version.Trim() == AutoUpdater.CurrentVersion?.ToString();

        public string UpdateString => new Version(Version) > AutoUpdater.CurrentVersion ? Properties.Resource.Upgrade : ColorVision.Properties.Resource.Rollback;

        public ChangeLogEntry()
        {
            Changes = new List<string>();
            UpdateCommand = new RelayCommand(a =>
            {
                if (Version == null) return;
                if (new Version(Version)> AutoUpdater.CurrentVersion)
                {
                    AutoUpdater.GetInstance().Update(Version, Path.GetTempPath());
                }
                else if (new Version(Version) == AutoUpdater.CurrentVersion)
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "软件版本相同");
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "回退软件需要先卸载在安装，或者是安装后重新运行安装包；");
                    AutoUpdater.GetInstance().Update(Version, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
                }
            });
        }
    }

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
                CHANGELOG = await AutoUpdater.GetInstance().GetChangeLog(AutoUpdater.GetInstance().CHANGELOG);
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
