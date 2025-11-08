using ColorVision.Common.MVVM;
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
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Show();
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
        RecentFileList SolutionHistory = new() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        public ObservableCollection<SolutionInfo> SolutionInfos { get; set; }= new ObservableCollection<SolutionInfo>();

        private void BaseWindow_Initialized(object sender, EventArgs e)
        {
            foreach (var item in SolutionHistory.RecentFiles)
            {
                FileInfo Info = new(item);
                if (Info.Exists)
                {
                    SolutionInfos.Add(new SolutionInfo() { Name = Info.Name, FullName = Info.FullName, CreationTime = Info.CreationTime.ToString("yyyy/MM/dd H:mm") });
                }
                else
                {
                    SolutionHistory.RemoveFile(item);
                }
            }
            ListView1.ItemsSource = SolutionInfos;
            ListView1.Visibility = Visibility.Visible;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            openFileDialog.Filter = "ColorVision Solution (*.cvsln)|*.cvsln";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SolutionManager.GetInstance().OpenSolution(openFileDialog.FileName);
                Close();
            }
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;

        }

        private void ListView1_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView)
            {
                if (listView.SelectedIndex > -1)
                {
                    SolutionManager.GetInstance().OpenSolution(SolutionInfos[listView.SelectedIndex].FullName);
                    Close();
                }
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView)
            {
                if (listView.SelectedIndex > -1)
                {
                    SolutionManager.GetInstance().OpenSolution(SolutionInfos[listView.SelectedIndex].FullName);
                    Close();
                }
            }
        }



        private void SearchBar_OnSearchStarted(object sender, HandyControl.Data.FunctionEventArgs<string> e)
        {

        }

        private readonly char[] Chars = new[] { ' ' };
        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (SearchNoneText.Visibility == Visibility.Visible)
                    SearchNoneText.Visibility = Visibility.Collapsed;

                if (string.IsNullOrWhiteSpace(textBox.Text))
                {
                    ListView1.ItemsSource = SolutionInfos;
                }
                else
                {
                    var keywords = textBox.Text.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                    var filteredResults = SolutionInfos
                        .OfType<SolutionInfo>()
                        .Where(template => keywords.All(keyword =>
                            template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                            template.FullName.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)||
                            template.CreationTime.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase)
                            ))
                        .ToList();

                    // 更新 ListView 的数据源
                    ListView1.ItemsSource = filteredResults;
                    if (filteredResults.Count == 0)
                    {
                        SearchNoneText.Visibility = Visibility.Visible;
                        SearchNoneText.Text = "未找到" + textBox.Text + "相关项目";
                    }
                }
            }
        }

        private void Delete(object sender, RoutedEventArgs e)
        {
            
        }
    }

    public class SolutionInfo  :ViewModelBase
    {
        public RelayCommand CopyCommand { get; set; }

        public ContextMenu ContextMenu { get; set; }

        public SolutionInfo()
        {
            CopyCommand = new RelayCommand(a => { if (FullName != null) Common.NativeMethods.Clipboard.SetText(FullName); } , a => FullName!=null);

            MenuItem menuItem = new MenuItem() { Header ="复制路径" ,Command = CopyCommand };
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(menuItem);
        }

        public string Name { get; set; }
        public string FullName { get; set; }
        public string CreationTime { get; set; }
    }
}
