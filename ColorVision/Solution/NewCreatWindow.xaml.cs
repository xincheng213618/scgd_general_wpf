using ColorVision.MVVM;
using ColorVision.SettingUp;
using ColorVision.Solution.RecentFile;
using ScottPlot.Styles;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Solution
{
    public class NewCreateViewMode : ViewModelBase
    {


        public RecentFileList RecentNewCreateCache { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\RecentNewCreateCache") };

        public NewCreateViewMode()
        {
            foreach (var item in RecentNewCreateCache.RecentFiles)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(item);
                if (directoryInfo.Exists)
                {
                    RecentNewCreateCacheList.Add(directoryInfo.FullName);
                }
            }
            if (RecentNewCreateCacheList.Count == 0)
            {
                string Default = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision";
                RecentNewCreateCache.InsertFile(Default);
                RecentNewCreateCacheList.Add(Default);
                if (Directory.Exists(Default))
                    Directory.CreateDirectory(Default);
            }


            DirectoryPath = RecentNewCreateCacheList[0];
            this.Name = NewCreateFileName(GlobalSetting.GetInstance().SoftwareConfig.ProjectConfig.ProjectControl.DefaultCreatName);
            RecentNewCreateNameCacheList.Add(Name);
        }

        public string NewCreateFileName(string FileName)
        {
            if (!Directory.Exists($"{this.DirectoryPath}\\{FileName}"))
                return FileName;
            for (int i = 1; i < 999; i++)
            {
                if (!Directory.Exists($"{this.DirectoryPath}\\{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        /// <summary>
        /// 工程名称
        /// </summary>
        public string Name { get => _Name; set { IsCanCreate = !(string.IsNullOrWhiteSpace(value)||string.IsNullOrWhiteSpace(DirectoryPath)); _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;

        public bool IsCanCreate { get => _IsCanCreate; set { _IsCanCreate = value; NotifyPropertyChanged(); } }
        private bool _IsCanCreate = true;

        /// <summary>
        /// 工程位置
        /// </summary>
        public string DirectoryPath { get => _DirectoryPath; set { IsCanCreate = !(string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(Name));  _DirectoryPath = value; NotifyPropertyChanged(); } }
        private string _DirectoryPath = string.Empty;

        public ObservableCollection<string> RecentNewCreateCacheList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> RecentNewCreateNameCacheList { get; set; } = new ObservableCollection<string>();

    }


    /// <summary>
    /// NewCreateWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewCreateWindow : Window
    {
        public NewCreateViewMode NewCreateViewMode { get; set; }
        public NewCreateWindow()
        {
            InitializeComponent();
            NewCreateViewMode = new NewCreateViewMode();
            this.DataContext = NewCreateViewMode;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.UseDescriptionForTitle = true;
            dialog.Description = "为新项目选择位置";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (string.IsNullOrEmpty(dialog.SelectedPath))
                {
                    MessageBox.Show("文件夹路径不能为空", "提示");
                    return;
                }
                NewCreateViewMode.DirectoryPath = dialog.SelectedPath;
                NewCreateViewMode.RecentNewCreateCache.InsertFile(NewCreateViewMode.DirectoryPath);
            }
        }
        public bool IsCreate { get; set; }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewCreateViewMode.Name))
            {
                this.Close();
                return;
            }
            string SolutionDirectoryPath = NewCreateViewMode.DirectoryPath + "\\" + NewCreateViewMode.Name;


            if (SolutionDirectoryPath.IndexOfAny(System.IO.Path.GetInvalidPathChars()) >= 0 || NewCreateViewMode.Name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
            {
                MessageBox.Show("工程名不能包含特殊字符", "ColorVision");
                return;
            }


            if (Directory.Exists(SolutionDirectoryPath))
            {
                var result = MessageBox.Show("文件夹不为空，是否清空文件夹", "ColorVision", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(SolutionDirectoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "ColorVision");
                    }
                }
            }
            Directory.CreateDirectory(SolutionDirectoryPath);
            NewCreateViewMode.RecentNewCreateCache.InsertFile(NewCreateViewMode.DirectoryPath);

            IsCreate = true;
            this.Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex>-1)
            {
                NewCreateViewMode.DirectoryPath = NewCreateViewMode.RecentNewCreateCacheList[comboBox.SelectedIndex];
                NewCreateViewMode.Name = NewCreateViewMode.NewCreateFileName(GlobalSetting.GetInstance().SoftwareConfig.ProjectConfig.ProjectControl.DefaultCreatName);

            }
        }
    }
}
