using ColorVision.Common.MVVM;
using ColorVision.Update;
using ColorVision.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision
{
    public class ChangeLogEntry : ViewModelBase
    {
        public string Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public List<string> Changes { get; set; }
        public string ChangeLog { get => string.Join("\n", Changes);}

        public RelayCommand UpdateCommand { get; set; }

        public bool IsUpdateAvailable => AutoUpdater.GetInstance().IsUpdateAvailable(Version);

        public bool IsCurrentVision  =>   Version.Trim() == AutoUpdater.CurrentVersion?.ToString();

        public ChangeLogEntry()
        {
            Changes = new List<string>();
            UpdateCommand = new RelayCommand(a =>
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "回退软件需要先卸载在安装，或者是安装后重新运行安装包；");
                if (Version!=null)
                    AutoUpdater.GetInstance().Update(Version, Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
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
        private void Window_Initialized(object sender, System.EventArgs e)
        {
            string changelogPath = "CHANGELOG.md";
            string changelogContent = File.ReadAllText(changelogPath);


            ChangeLogEntrys = Parse(changelogContent);
            ChangeLogListView.ItemsSource = ChangeLogEntrys;
        }

        public ObservableCollection<ChangeLogEntry> Parse(string changelogText)
        {
            var entries = new ObservableCollection<ChangeLogEntry>();
            var lines = changelogText.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

            ChangeLogEntry currentEntry = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("## ["))
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
                else if (currentEntry != null && line.StartsWith("1.") || line.StartsWith("2.") || line.StartsWith("3."))
                {
                    currentEntry.Changes.Add(line.Trim());
                }
            }
            // Add the last entry if exists
            if (currentEntry != null)
            {
                entries.Add(currentEntry);
            }

            return entries;
        }

    }
}
