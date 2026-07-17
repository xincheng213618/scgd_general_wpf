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
        public override string InputGestureText => "Ctrl+O";
        public override ICommand Command => ApplicationCommands.Open;
    }

    public class MenuOpenFolder : MenuItemFileBase
    {
        public override string OwnerGuid => nameof(MenuOpen);

        public override string GuidId => nameof(MenuOpenFolder);

        public override int Order => 2;

        public override string Header => ColorVision.UI.Properties.Resources.OpenFolder;
        public override ICommand Command => SolutionWorkspaceCommands.OpenFolder;
    }

    /// <summary>
    /// NewCreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class OpenSolutionWindow: BaseWindow
    {
        private CancellationTokenSource? _openCancellation;
        private bool _isOpening;

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
            if (!ResourceOpenService.TryDescribeWorkspaceResource(
                path,
                out WorkspaceResourceInfo resourceInfo))
                return false;

            solutionInfo = new SolutionInfo
            {
                Name = resourceInfo.DisplayName,
                KindName = resourceInfo.KindDisplayName,
                FullName = resourceInfo.FullPath,
                CreationTime = resourceInfo.CreationTime.ToString("yyyy/MM/dd H:mm"),
            };
            return true;
        }

        private async void OpenSolutionFile_Click(object sender, RoutedEventArgs e)
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
            if (openFileDialog.ShowDialog(this) == true
                && await OpenPathAsync(openFileDialog.FileName))
                Close();
        }

        private async void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = ColorVision.UI.Properties.Resources.OpenFolder,
                Multiselect = false,
            };
            if (dialog.ShowDialog(this) == true
                && await OpenPathAsync(dialog.FolderName))
                Close();
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

        private async void RecentSolutions_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left
                || sender is not ListView listView
                || ItemsControl.ContainerFromElement(listView, e.OriginalSource as DependencyObject) is not ListViewItem)
                return;

            await OpenSelectedRecentSolutionAsync();
        }

        private async void RecentSolutions_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || ListView1.SelectedItem is not SolutionInfo)
                return;
            e.Handled = true;
            await OpenSelectedRecentSolutionAsync();
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

        private async void OpenRecentSolution_Click(object sender, RoutedEventArgs e)
        {
            await OpenSelectedRecentSolutionAsync();
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

        private async Task<bool> OpenSelectedRecentSolutionAsync()
        {
            if (ListView1.SelectedItem is not SolutionInfo solutionInfo)
                return false;

            if (await OpenPathAsync(solutionInfo.FullName))
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

        private async Task<bool> OpenPathAsync(string path)
        {
            if (_isOpening)
                return false;

            var cancellation = new CancellationTokenSource();
            _openCancellation = cancellation;
            SetOpeningState(true, path);
            try
            {
                return await ResourceOpenService.Instance.TryOpenWithFeedbackAsync(
                    path,
                    this,
                    cancellation.Token);
            }
            finally
            {
                if (ReferenceEquals(_openCancellation, cancellation))
                    _openCancellation = null;
                cancellation.Dispose();
                SetOpeningState(false, string.Empty);
            }
        }

        private void SetOpeningState(bool isOpening, string path)
        {
            _isOpening = isOpening;
            WorkspacePickerContent.IsEnabled = !isOpening;
            OpeningOverlay.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
            OpeningPathText.Text = path;
        }

        private void CancelOpen_Click(object sender, RoutedEventArgs e)
        {
            _openCancellation?.Cancel();
        }

        protected override void OnClosed(EventArgs e)
        {
            _openCancellation?.Cancel();
            base.OnClosed(e);
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
        public string KindName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CreationTime { get; set; } = string.Empty;
    }
}
