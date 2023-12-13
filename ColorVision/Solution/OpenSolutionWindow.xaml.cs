using ColorVision.RecentFile;
using ColorVision.Themes.Controls;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Solution
{
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

        private void BaseWindow_Initialized(object sender, EventArgs e)
        {
            foreach (var item in SolutionHistory.RecentFiles)
            {
                DirectoryInfo Info = new DirectoryInfo(item);
                if (Info.Exists)
                {
                    SolutionInfos.Add(new SolutionInfo() { Name = Info.Name, FullName = Info.FullName, CreationTime = Info.CreationTime.ToString("yyyy/MM/dd H:mm") });
                }
            }
            SolutionInfosShow = new ObservableCollection<SolutionInfo>(SolutionInfos);
            ListView1.ItemsSource = SolutionInfosShow;

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
            this.Close();
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
                    this.Close();
                }
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

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
