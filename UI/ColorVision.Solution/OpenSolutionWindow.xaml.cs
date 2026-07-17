using ColorVision.Common.MVVM;
using ColorVision.Solution.Editor;
using ColorVision.Solution.RecentFile;
using ColorVision.Themes.Controls;
using ColorVision.UI.Menus.Base;
using ColorVision.UI.Menus.Base.File;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution
{

    public class MenuOpenSolution : MenuItemFileBase
    {
        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuOpenSolution);

        public override int Order => 1;

        public override string Header => ColorVision.UI.Properties.Resources.ProjectSolution_P;

        public override void Execute()
        {
            SolutionManager.OpenSolutionWindow();
        }
    }

    public class MenuOpenFolder : MenuItemFileBase
    {
        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuOpenFolder);

        public override int Order => 2;

        public override string Header => ColorVision.UI.Properties.Resources.OpenFolder;

        public override void Execute()
        {
            SolutionManager.OpenFolderDialog();
        }
    }

    /// <summary>
    /// NewCreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class OpenSolutionWindow: BaseWindow
    {
        public OpenSolutionWindow()
        {
            InitializeComponent();
        }
        private static RecentFileList SolutionHistory => SolutionManager.GetInstance().SolutionHistory;

        public ObservableCollection<SolutionInfo> SolutionInfos { get; set; }= new ObservableCollection<SolutionInfo>();

        private void BaseWindow_Initialized(object sender, EventArgs e)
        {
            SolutionInfos.Clear();
            foreach (var item in SolutionHistory.RecentFiles)
            {
                if (TryCreateSolutionInfo(item, out SolutionInfo solutionInfo))
                {
                    SolutionInfos.Add(solutionInfo);
                }
                else
                {
                    SolutionHistory.RemoveFile(item);
                }
            }
            ListView1.ItemsSource = SolutionInfos;
            ListView1.Visibility = Visibility.Visible;
        }

        internal static bool TryCreateSolutionInfo(string path, out SolutionInfo solutionInfo)
        {
            solutionInfo = null!;

            string normalizedPath = SolutionManager.NormalizeRecentPath(path);
            if (!SolutionManager.IsSupportedOpenPath(normalizedPath))
                return false;

            if (Directory.Exists(normalizedPath))
            {
                DirectoryInfo directoryInfo = new(normalizedPath);
                solutionInfo = new SolutionInfo()
                {
                    Name = directoryInfo.Name,
                    FullName = directoryInfo.FullName,
                    CreationTime = directoryInfo.CreationTime.ToString("yyyy/MM/dd H:mm")
                };
                return true;
            }

            if (File.Exists(normalizedPath))
            {
                FileInfo fileInfo = new(normalizedPath);
                solutionInfo = new SolutionInfo()
                {
                    Name = fileInfo.Name,
                    FullName = fileInfo.FullName,
                    CreationTime = fileInfo.CreationTime.ToString("yyyy/MM/dd H:mm")
                };
                return true;
            }

            return false;
        }

        private void OpenSolutionFile_Click(object sender, RoutedEventArgs e)
        {
            string solutionPatterns = SolutionManager.GetSolutionFileDialogPattern();
            string projectPatterns = Explorer.ProjectProviderRegistry.GetProjectFileDialogPattern();
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = true,
                Filter = $"支持的解决方案或项目 ({solutionPatterns};{projectPatterns})|{solutionPatterns};{projectPatterns}|解决方案 ({solutionPatterns})|{solutionPatterns}|项目 ({projectPatterns})|{projectPatterns}",
                Multiselect = false,
                RestoreDirectory = true,
            };
            if (openFileDialog.ShowDialog(this) == true)
            {
                if (ResourceOpenService.Instance.TryOpenWithFeedback(openFileDialog.FileName, this))
                    Close();
            }
        }

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (SolutionManager.OpenFolderDialog())
            {
                Close();
            }
        }

        private void CreateSolution_Click(object sender, RoutedEventArgs e)
        {
            SolutionManager.GetInstance().NewCreateWindow();
            Close();
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;

        }

        private void RecentSolutions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left
                || sender is not ListView listView
                || ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) is not ListViewItem)
                return;

            OpenSelectedRecentSolution();
        }

        private void RecentSolutions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && OpenSelectedRecentSolution())
                e.Handled = true;
        }

        private void RecentSolutions_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not ListView listView)
                return;

            if (ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) is ListViewItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
            else
            {
                listView.SelectedItem = null;
            }
        }

        private void RecentSolutions_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (ListView1.SelectedItem is not SolutionInfo)
                e.Handled = true;
        }

        private void OpenRecentSolution_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedRecentSolution();
        }

        private void CopyRecentPath_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItem is SolutionInfo solutionInfo)
                Common.Clipboard.SetText(solutionInfo.FullName);
        }

        private void RemoveRecentSolution_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedItem is not SolutionInfo solutionInfo)
                return;

            SolutionHistory.RemoveFile(solutionInfo.FullName);
            SolutionInfos.Remove(solutionInfo);
            ApplyRecentFilter();
        }

        private bool OpenSelectedRecentSolution()
        {
            if (ListView1.SelectedItem is not SolutionInfo solutionInfo)
                return false;

            if (ResourceOpenService.Instance.TryOpenWithFeedback(solutionInfo.FullName, this))
            {
                Close();
                return true;
            }

            if (!SolutionManager.IsSupportedOpenPath(solutionInfo.FullName))
            {
                SolutionInfos.Remove(solutionInfo);
                ApplyRecentFilter();
            }
            return false;
        }



        private void SearchBar_OnSearchStarted(object sender, HandyControl.Data.FunctionEventArgs<string> e)
        {

        }

        private readonly char[] Chars = new[] { ' ' };
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox)
                ApplyRecentFilter();
        }

        private void ApplyRecentFilter()
        {
            if (SearchNoneText.Visibility == Visibility.Visible)
                SearchNoneText.Visibility = Visibility.Collapsed;

            string searchText = Searchbox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ListView1.ItemsSource = SolutionInfos;
                return;
            }

            string[] keywords = searchText.Split(Chars, StringSplitOptions.RemoveEmptyEntries);
            var filteredResults = SolutionInfos
                .Where(template => keywords.All(keyword =>
                    template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || template.FullName.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || template.CreationTime.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ListView1.ItemsSource = filteredResults;
            if (filteredResults.Count == 0)
            {
                SearchNoneText.Visibility = Visibility.Visible;
                SearchNoneText.Text = $"{Properties.Resources.NoFound} {searchText} {Properties.Resources.RelateItem}";
            }
        }
    }

    public sealed class SolutionInfo
    {
        public string Name { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CreationTime { get; set; } = string.Empty;
    }
}
