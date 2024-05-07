using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.HotKey;
using ColorVision.Properties;
using ColorVision.RecentFile;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using Mysqlx.Prepare;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Solution
{

    public class HotKeyOpenSolution : IHotKey,IMenuItem
    {
        public string? OwnerGuid => "File";

        public string? GuidId => "OpenSolution";

        public int Order => 1;

        public string? Header => Resource.MenuOpen;

        public string? InputGestureText => "Ctrl + O";

        public object? Icon
        {
            get
            {
                TextBlock text = new TextBlock
                {
                    Text = "\uE8E5", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }


        public RelayCommand Command => new RelayCommand(A => Execute());


        public HotKeys HotKeys => new HotKeys(Properties.Resource.OpenSolution, new Hotkey(Key.O, ModifierKeys.Control), Execute);


        private void Execute()
        {
            OpenSolutionWindow openSolutionWindow = new OpenSolutionWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            openSolutionWindow.Closed += delegate
            {
                if (!string.IsNullOrWhiteSpace(openSolutionWindow.FullName))
                {
                    if (Directory.Exists(openSolutionWindow.FullName))
                        SolutionManager.GetInstance().OpenSolution(openSolutionWindow.FullName);
                    else
                        MessageBox.Show(Application.Current.GetActiveWindow(),"找不到工程","ColorVision");
                }

            };
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
        public string FullName { get; set; } = string.Empty;

        RecentFileList SolutionHistory = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\SolutionHistory") };

        public ObservableCollection<SolutionInfo> SolutionInfos { get; set; }= new ObservableCollection<SolutionInfo>();
        public ObservableCollection<SolutionInfo> SolutionInfosShow { get; set; }

        private async void BaseWindow_Initialized(object sender, EventArgs e)
        {

            await Task.Delay(50);
            
            foreach (var item in SolutionHistory.RecentFiles)
            {
                FileInfo Info = new FileInfo(item);
                if (Info.Exists)
                {
                    SolutionInfos.Add(new SolutionInfo() { Name = Info.Name, FullName = Info.FullName, CreationTime = Info.CreationTime.ToString("yyyy/MM/dd H:mm") });
                }
                else
                {
                    SolutionHistory.RemoveFile(item);
                }
            }
            SolutionInfosShow = new ObservableCollection<SolutionInfo>(SolutionInfos);
            ListView1.ItemsSource = SolutionInfosShow;
            ListView1.Visibility = Visibility.Visible;
        }





        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = "请选择文件夹";
            dialog.ShowNewFolderButton = true; // 允许用户创建新文件夹

            // 显示对话框
            System.Windows.Forms.DialogResult result = dialog.ShowDialog();

            // 处理对话框返回的结果
            if (result == System.Windows.Forms.DialogResult.OK)
            { 
            }
            Close();
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
                    FullName = SolutionInfos[listView.SelectedIndex].FullName;
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
                    FullName = SolutionInfos[listView.SelectedIndex].FullName;
                    Close();
                }
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (sender is Button button&& button.Tag is SolutionInfo soulutioninfo)
            {
                SolutionInfos.Remove(soulutioninfo);
                SolutionInfosShow.Remove(soulutioninfo);
                SolutionHistory.RemoveFile(soulutioninfo.FullName);

                if (!string.IsNullOrEmpty(Searchbox.Text)&&SolutionInfosShow.Count == 0)
                {
                    SearchNoneText.Visibility = Visibility.Visible;
                    SearchNoneText.Text = "未找到" + Searchbox.Text + "相关项目";
                }
            }
        }


        private void SearchBar_OnSearchStarted(object sender, HandyControl.Data.FunctionEventArgs<string> e)
        {

        }

        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (SearchNoneText.Visibility == Visibility.Visible)
                    SearchNoneText.Visibility = Visibility.Hidden;
                if (string.IsNullOrEmpty(textBox.Text))
                {
                    ListView1.ItemsSource = SolutionInfos;

                }
                else
                {
                    SolutionInfosShow = new ObservableCollection<SolutionInfo>();
                    foreach (var item in SolutionInfos)
                    {
                        if (item.FullName.Contains(textBox.Text))
                            SolutionInfosShow.Add(item);
                    }
                    ListView1.ItemsSource = SolutionInfosShow;
                    if (SolutionInfosShow.Count == 0)
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

    public class SolutionInfo
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string CreationTime { get; set; }
    }
}
