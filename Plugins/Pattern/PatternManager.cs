using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;

namespace Pattern
{

    public class PatternManagerConfig:ViewModelBase,IConfig
    {
        [DisplayName("图卡生成路径"), PropertyEditorType(PropertyEditorType.TextSelectFolder)]
        public string SaveFilePath { get => _SaveFilePath; set { _SaveFilePath = value; OnPropertyChanged(); } }
        private string _SaveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Pattern");

        [DisplayName("切换模板后创建图像")]
        public bool IsSwitchCreate { get => _IsSwitchCreate; set { _IsSwitchCreate = value; OnPropertyChanged(); } }
        private bool _IsSwitchCreate = true;

        [DisplayName("保存格式")]
        public PatternFormat PatternFormat { get => _PatternFormat; set { _PatternFormat = value; OnPropertyChanged(); } }
        private PatternFormat _PatternFormat = PatternFormat.bmp;



    }

    public class PatternManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PatternManager));
        private static PatternManager _instance;
        private static readonly object _locker = new();
        public static PatternManager GetInstance() { lock (_locker) { _instance ??= new PatternManager(); return _instance; } }

        public PatternManagerConfig Config { get; set; } = ConfigService.Instance.GetRequiredService<PatternManagerConfig>();    

        public ObservableCollection<TemplatePatternFile> TemplatePatternFiles { get; set; } = new ObservableCollection<TemplatePatternFile>();
        public List<PatternMeta> Patterns { get; set; } = new List<PatternMeta>();

        public string PatternPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Pattern");
       
        public RelayCommand EditCommand { get; set; }
        public RelayCommand OpenPatternPathCommand { get; set; }
        public RelayCommand OpenSaveFilePathCommand { get; set; }
        public RelayCommand ClearSaveFilePathCommand { get; set; }
        public RelayCommand ClearTemplatePatternFilesCommand { get; set; }

        private PatternManager()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IPattern).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    try
                    {
                        var displayName = type.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? type.Name;
                        var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                        var category = type.GetCustomAttribute<CategoryAttribute>()?.Category ?? "";

                        IPattern pattern = (IPattern)Activator.CreateInstance(type);
                        if (pattern != null)
                        {
                            var patternMeta = new PatternMeta
                            {
                                Name = displayName,
                                Description = description,
                                Category = category,
                                Pattern = pattern
                            };
                            Patterns.Add(patternMeta);
                            log.Info($"已加载图案生成器: {type.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"加载图案生成器失败: {type.FullName}", ex);
                    }

                }
            }

            if (!Directory.Exists(PatternPath))
                Directory.CreateDirectory(PatternPath);
            foreach (var item in Directory.GetFiles(PatternPath))
            {
                if (item.EndsWith(".json", StringComparison.CurrentCulture))
                {
                    TemplatePatternFiles.Add(new TemplatePatternFile(item));
                }
            }
            EditCommand = new RelayCommand(a => Edit());
            OpenPatternPathCommand = new RelayCommand(a => OpenPatternPath());
            OpenSaveFilePathCommand = new RelayCommand(a => OpenSaveFilePath());
            ClearSaveFilePathCommand = new RelayCommand(a => ClearSaveFilePath());
            ClearTemplatePatternFilesCommand = new RelayCommand(a => ClearTemplatePatternFiles());
        }

        public void OpenSaveFilePath()
        {
            PlatformHelper.OpenFolder(Config.SaveFilePath);
        }

        public void ClearTemplatePatternFiles()
        {
            TemplatePatternFiles.Clear();
            Directory.Delete(PatternPath, true);
            if (!Directory.Exists(PatternPath))
                Directory.CreateDirectory(PatternPath);
        }

        public void ClearSaveFilePath()
        {
            // 2. 检查目录是否存在
            if (Directory.Exists(Config.SaveFilePath))
            {
                var confirmResult = MessageBox.Show("确定要清空内容吗？", "清空确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirmResult != MessageBoxResult.Yes)
                {
                    return; // 用户取消
                }
                try
                {
                    Directory.Delete(Config.SaveFilePath, true);
                    if (!Directory.Exists(Config.SaveFilePath))
                        Directory.CreateDirectory(Config.SaveFilePath);
                    // 3. 清空成功提示
                    MessageBox.Show("清空成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"清空失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("目录不存在！", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public void OpenPatternPath()
        {
            PlatformHelper.OpenFolder(PatternPath);
        }

        public void Edit()
        {
            new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
        }
    }
}
