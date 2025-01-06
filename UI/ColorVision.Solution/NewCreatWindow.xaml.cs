using ColorVision.Common.MVVM;
using ColorVision.Solution.Properties;
using ColorVision.RecentFile;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ColorVision.Themes;

namespace ColorVision.Solution
{
    public class HotKeyNewCreate : IHotKey,IMenuItem
    {

        public string? OwnerGuid => "File";

        public string? GuidId => "MenuNew";

        public int Order => 0;

        public string? Header => Resources.MenuNew;

        public string? InputGestureText => "Ctrl + N";

        public object? Icon
        {
            get
            {
                TextBlock text = new()
                {
                    Text = "\uE8F4", // 使用Unicode字符
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    FontSize = 15,
                };
                text.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                return text;
            }
        }
        public ICommand Command => new RelayCommand(A => Execute());

        public HotKeys HotKeys => new(Resources.NewSolution, new Hotkey(Key.N, ModifierKeys.Control), Execute);
        public Visibility Visibility => Visibility.Visible;
        private void Execute()
        {
            NewCreateWindow newCreatWindow = new NewCreateWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            newCreatWindow.Closed += delegate
            {
                if (newCreatWindow.IsCreate)
                {
                    string SolutionDirectoryPath = newCreatWindow.NewCreateViewMode.DirectoryPath + "\\" + newCreatWindow.NewCreateViewMode.Name;
                    SolutionManager.GetInstance().CreateSolution(SolutionDirectoryPath);
                }
            };
            newCreatWindow.ShowDialog();
        }
    }

    public class NewCreateViewMode : ViewModelBase
    {
        public RecentFileList RecentNewCreateCache { get; set; } = new RecentFileList() { Persister = new RegistryPersister("Software\\ColorVision\\RecentNewCreateCache") };

        public NewCreateViewMode()
        {
            foreach (var item in RecentNewCreateCache.RecentFiles)
            {
                DirectoryInfo directoryInfo = new(item);
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
            Name = NewCreateFileName(SolutionSetting.Instance.DefaultCreatName);
            RecentNewCreateNameCacheList.Add(Name);
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
                NewCreateViewMode.RecentNewCreateCache.InsertFile(NewCreateViewMode.DirectoryPath);
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
            NewCreateViewMode.RecentNewCreateCache.InsertFile(NewCreateViewMode.DirectoryPath);


            IsCreate = true;
            Close();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex>-1)
            {
                NewCreateViewMode.DirectoryPath = NewCreateViewMode.RecentNewCreateCacheList[comboBox.SelectedIndex];
                NewCreateViewMode.Name = NewCreateViewMode.NewCreateFileName(SolutionSetting.Instance.DefaultCreatName);

            }
        }
    }
}
