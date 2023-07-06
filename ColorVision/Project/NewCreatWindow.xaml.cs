using ColorVision.MVVM;
using ColorVision.Project.RecentFile;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Project
{
    public class NewCreateViewMode : ViewModelBase
    {
        public RecentFileList RecentNewCreatCache { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\RecentNewCreatCache") };

        public NewCreateViewMode()
        {
            if (RecentNewCreatCache.RecentFiles.Count == 0)
                RecentNewCreatCache.InsertFile(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision");

            foreach (var item in RecentNewCreatCache.RecentFiles)
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(item);
                if (directoryInfo.Exists)
                {
                    RecentNewCreatCacheList.Add(directoryInfo.FullName);
                }
            }
            if (RecentNewCreatCacheList.Count == 0)
            {
                RecentNewCreatCache.InsertFile(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision");
                RecentNewCreatCacheList.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\ColorVision");
            }

            DirectoryPath = RecentNewCreatCacheList[0];
            this.Name = NewCreateFileName("新建工程");
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
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name = string.Empty;

        /// <summary>
        /// 工程位置
        /// </summary>
        public string DirectoryPath { get => _DirectoryPath; set { _DirectoryPath = value; NotifyPropertyChanged(); } }
        private string _DirectoryPath = string.Empty;

        public ObservableCollection<string> RecentNewCreatCacheList { get; set; } = new ObservableCollection<string>();
    }


    /// <summary>
    /// NewCreatWindow.xaml 的交互逻辑
    /// </summary>
    public partial class NewCreatWindow : Window
    {
        public NewCreateViewMode newCreatViewMode { get; set; }
        public NewCreatWindow()
        {
            InitializeComponent();
            newCreatViewMode = new NewCreateViewMode();
            this.DataContext = newCreatViewMode;
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
                newCreatViewMode.DirectoryPath = dialog.SelectedPath;
                newCreatViewMode.RecentNewCreatCache.InsertFile(newCreatViewMode.DirectoryPath);
            }
        }
        public bool IsCreate { get; set; }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(newCreatViewMode.Name))
            {
                this.Close();
            }
            string SolutionDirectoryPath = newCreatViewMode.DirectoryPath + "\\" + newCreatViewMode.Name;

            if (Directory.Exists(SolutionDirectoryPath))
            {
                var result = MessageBox.Show("文件夹不为空，是否清空文件夹", "Grid", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Directory.Delete(SolutionDirectoryPath, true);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Grid");
                    }
                }
            }
            Directory.CreateDirectory(SolutionDirectoryPath);
            IsCreate = true;
            this.Close();
        }
    }
}
