using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ImageProjector;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace Pattern
{
    public class PatternManagerConfig:ViewModelBase,IConfig
    {
        [DisplayName("图卡生成路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
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
        public ObservableCollection<PatternMeta> Patterns { get; set; } = new ObservableCollection<PatternMeta>();

        public string PatternPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Pattern");
       
        public RelayCommand EditCommand { get; set; }
        public RelayCommand OpenPatternPathCommand { get; set; }
        public RelayCommand OpenSaveFilePathCommand { get; set; }
        public RelayCommand ClearSaveFilePathCommand { get; set; }
        public RelayCommand ClearTemplatePatternFilesCommand { get; set; }
        public RelayCommand ExportZipCommand { get; set; }
        public RelayCommand ImportZipCommand { get; set; }

        public RelayCommand OpenImageProjectorCommand { get; set; }

        private PatternManager()
        {
            // 异步加载插件和模板文件
            Task.Run(() => LoadPatternsAndFilesAsync());

            EditCommand = new RelayCommand(a => Edit());
            OpenPatternPathCommand = new RelayCommand(a => OpenPatternPath());
            OpenSaveFilePathCommand = new RelayCommand(a => OpenSaveFilePath());
            ClearSaveFilePathCommand = new RelayCommand(a => ClearSaveFilePath());
            ClearTemplatePatternFilesCommand = new RelayCommand(a => ClearTemplatePatternFiles());
            ExportZipCommand = new RelayCommand(async a => await ExportPatternZipAsync());
            ImportZipCommand = new RelayCommand(async a => await ImportPatternZipAsync());
            OpenImageProjectorCommand = new RelayCommand(a =>
            {
                ImageProjectorWindow window = new ImageProjectorWindow
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Show();
            });
        }



        private async Task LoadPatternsAndFilesAsync()
        {
            await Task.Delay(30);
            // 1. 加载插件
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
                            lock (Patterns)
                            {
                                Application.Current.Dispatcher.Invoke(() => Patterns.Add(patternMeta), DispatcherPriority.Background);
                            }
                            log.Info($"已加载图案生成器: {type.FullName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"加载图案生成器失败: {type.FullName}", ex);
                    }
                }
            }

            // 2. 加载模板文件
            if (!Directory.Exists(PatternPath))
                Directory.CreateDirectory(PatternPath);
            var files = Directory.GetFiles(PatternPath);
            Application.Current.Dispatcher.Invoke(() =>
            {
                TemplatePatternFiles.Clear();
                foreach (var item in files)
                {
                    if (item.EndsWith(".json", StringComparison.CurrentCulture))
                    {
                        TemplatePatternFiles.Add(new TemplatePatternFile(item));
                    }
                }
            });
        }

        /// <summary>
        /// 异步打包PatternPath目录为zip，并让用户选择导出位置
        /// </summary>
        public async Task ExportPatternZipAsync()
        {
            try
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Zip 文件 (*.zip)|*.zip",
                    FileName = "Pattern.zip"
                };
                if (saveFileDialog.ShowDialog() != true) return;

                string zipPath = saveFileDialog.FileName;

                await Task.Run(() =>
                {
                    if (File.Exists(zipPath))
                        File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(PatternPath, zipPath, CompressionLevel.Optimal, false);
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }

        /// <summary>
        /// 异步选择zip文件并解压到PatternPath
        /// </summary>
        public async Task ImportPatternZipAsync()
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Zip 文件 (*.zip)|*.zip"
                };
                if (openFileDialog.ShowDialog() != true) return;

                string zipPath = openFileDialog.FileName;

                await Task.Run(() =>
                {
                    if (Directory.Exists(PatternPath))
                        Directory.Delete(PatternPath, true);
                    Directory.CreateDirectory(PatternPath);
                    ZipFile.ExtractToDirectory(zipPath, PatternPath);
                });

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("导入成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    // 重新加载模板文件
                    TemplatePatternFiles.Clear();
                    foreach (var item in Directory.GetFiles(PatternPath))
                    {
                        if (item.EndsWith(".json", StringComparison.CurrentCulture))
                        {
                            TemplatePatternFiles.Add(new TemplatePatternFile(item));
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"导入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
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
            if (Directory.Exists(Config.SaveFilePath))
            {
                var confirmResult = MessageBox.Show("确定要清空内容吗？", "清空确认", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirmResult != MessageBoxResult.Yes)
                {
                    return;
                }
                try
                {
                    Directory.Delete(Config.SaveFilePath, true);
                    if (!Directory.Exists(Config.SaveFilePath))
                        Directory.CreateDirectory(Config.SaveFilePath);
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
