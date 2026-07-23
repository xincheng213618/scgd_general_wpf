using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;


namespace ColorVision.Solution.Editor
{
    [FolderEditor("文件夹浏览器", resourceKey: "Sol_Editor_Project", editorId: "colorvision.folder.project-list", isDefault: true, priority: 100)]
    public class ProjectEditor : EditorBase
    {
        public override void Open(string filePath)
        {
            if (!Directory.Exists(filePath))
                return;

            EditorDocumentService.Open(
                filePath,
                GetType(),
                new DirectoryInfo(filePath).Name,
                () => new FolderBrowserControl(filePath));
        }
    }

    public sealed class FolderBrowserControl : UserControl
    {
        private readonly ListView _listView;

        public FolderBrowserControl(string folderPath)
        {
            var directory = new DirectoryInfo(folderPath);
            var pathText = new TextBlock
            {
                Text = directory.FullName,
                Margin = new Thickness(8, 6, 8, 6),
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            _listView = new ListView
            {
                Margin = new Thickness(4),
                ItemsSource = LoadEntries(directory),
                View = CreateGridView(),
            };
            _listView.MouseDoubleClick += ListView_MouseDoubleClick;

            var root = new DockPanel();
            DockPanel.SetDock(pathText, Dock.Top);
            root.Children.Add(pathText);
            root.Children.Add(_listView);
            Content = root;
        }

        private async void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_listView.SelectedItem is not FolderBrowserEntry entry)
                return;

            if (entry.IsDirectory)
                ResourceOpenService.Instance.TryOpenWith(entry.FullPath, "colorvision.folder.project-list");
            else
                await ResourceOpenService.Instance.TryOpenWithFeedbackAsync(entry.FullPath);
        }

        private static IReadOnlyList<FolderBrowserEntry> LoadEntries(DirectoryInfo directory)
        {
            try
            {
                return directory.EnumerateFileSystemInfos()
                    .Where(item => (item.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
                    .Select(FolderBrowserEntry.Create)
                    .OrderByDescending(entry => entry.IsDirectory)
                    .ThenBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return Array.Empty<FolderBrowserEntry>();
            }
        }

        private static GridView CreateGridView()
        {
            var gridView = new GridView();
            gridView.Columns.Add(CreateColumn("名称", nameof(FolderBrowserEntry.Name), 300));
            gridView.Columns.Add(CreateColumn("类型", nameof(FolderBrowserEntry.Type), 100));
            gridView.Columns.Add(CreateColumn("修改时间", nameof(FolderBrowserEntry.Modified), 150));
            gridView.Columns.Add(CreateColumn("大小", nameof(FolderBrowserEntry.Size), 90));
            return gridView;
        }

        private static GridViewColumn CreateColumn(string header, string propertyName, double width)
        {
            return new GridViewColumn
            {
                Header = header,
                Width = width,
                DisplayMemberBinding = new Binding(propertyName),
            };
        }
    }

    public sealed record FolderBrowserEntry(
        string Name,
        string FullPath,
        bool IsDirectory,
        string Type,
        string Modified,
        string Size)
    {
        public static FolderBrowserEntry Create(FileSystemInfo item)
        {
            bool isDirectory = item is DirectoryInfo;
            long? length = item is FileInfo file ? file.Length : null;
            return new FolderBrowserEntry(
                item.Name,
                item.FullName,
                isDirectory,
                isDirectory ? "文件夹" : string.IsNullOrWhiteSpace(item.Extension) ? "文件" : $"{item.Extension.TrimStart('.').ToUpperInvariant()} 文件",
                item.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                length.HasValue ? FormatSize(length.Value) : string.Empty);
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? $"{bytes} B" : $"{value:0.#} {units[unit]}";
        }
    }
}
