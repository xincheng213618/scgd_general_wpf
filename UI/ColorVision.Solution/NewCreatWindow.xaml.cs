using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ColorVision.Themes;
using ColorVision.Solution.Mru;

namespace ColorVision.Solution
{

    public class NewCreateViewMode : ViewModelBase
    {
        public MruPathService RecentLocations { get; } = MruPathService.CreateLocal("recent-create-locations.json");

        public NewCreateViewMode()
        {
            RefreshRecentLocations();

            if (RecentLocationPaths.Count == 0)
            {
                string defaultLocation = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "ColorVision");
                Directory.CreateDirectory(defaultLocation);
                RememberLocation(defaultLocation);
            }
            DirectoryPath = RecentLocationPaths[0];
            Name = NewCreateFileName(SolutionSetting.Instance.DefaultCreatName);
            RecentNameSuggestions.Add(Name);
        }

        public string NewCreateFileName(string FileName)
        {
            if (!Directory.Exists($"{DirectoryPath}\\{FileName}"))
                return FileName;
            for (int i = 1; i < 999; i++)
            {
                if (!Directory.Exists($"{DirectoryPath}\\{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        /// <summary>
        /// 工程名称
        /// </summary>
        public string Name { get => _Name; set { IsCanCreate = !(string.IsNullOrWhiteSpace(value)||string.IsNullOrWhiteSpace(DirectoryPath)); _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        public bool IsCanCreate { get => _IsCanCreate; set { _IsCanCreate = value; OnPropertyChanged(); } }
        private bool _IsCanCreate = true;

        /// <summary>
        /// 工程位置
        /// </summary>
        public string DirectoryPath { get => _DirectoryPath; set { IsCanCreate = !(string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(Name));  _DirectoryPath = value; OnPropertyChanged(); } }
        private string _DirectoryPath = string.Empty;

        public ObservableCollection<string> RecentLocationPaths { get; } = new();
        public ObservableCollection<string> RecentNameSuggestions { get; } = new();

        public void RememberLocation(string path)
        {
            if (RecentLocations.Touch(path))
                RefreshRecentLocations();
        }

        private void RefreshRecentLocations()
        {
            RecentLocationPaths.Clear();
            foreach (string path in RecentLocations.Items
                .Select(entry => entry.Path)
                .Where(Directory.Exists))
            {
                RecentLocationPaths.Add(path);
            }
        }

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
            DataContext = NewCreateViewMode;
            this.ApplyCaption();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new();
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
                NewCreateViewMode.RememberLocation(NewCreateViewMode.DirectoryPath);
            }
        }
        public bool IsCreate { get; set; }

        private void Button_Close_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NewCreateViewMode.Name))
            {
                Close();
                return;
            }



            string SolutionDirectoryPath = NewCreateViewMode.DirectoryPath + "\\" + NewCreateViewMode.Name;


            if (SolutionDirectoryPath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || NewCreateViewMode.Name.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
            {
                MessageBox.Show("工程名不能包含特殊字符", "ColorVision");
                return;
            }
            if (!Directory.Exists(NewCreateViewMode.DirectoryPath))
            {
                if (MessageBox.Show("不存在父目录，是否创建", "ColorVision", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    Directory.CreateDirectory(NewCreateViewMode.DirectoryPath);
                }
                else
                {
                    Close();
                    return;
                }
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
            NewCreateViewMode.RememberLocation(NewCreateViewMode.DirectoryPath);


            IsCreate = true;
            Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex>-1)
            {
                NewCreateViewMode.DirectoryPath = NewCreateViewMode.RecentLocationPaths[comboBox.SelectedIndex];
                NewCreateViewMode.Name = NewCreateViewMode.NewCreateFileName(SolutionSetting.Instance.DefaultCreatName);

            }
        }
    }
}
