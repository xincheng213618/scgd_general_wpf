using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace ColorVision.Engine.Pattern
{

    public class PatternManagerConfig:ViewModelBase,IConfig
    {
        [DisplayName("图卡生成路径"), PropertyEditorType(PropertyEditorType.TextSelectFolder)]
        public string SaveFilePath { get => _SaveFilePath; set { _SaveFilePath = value; NotifyPropertyChanged(); } }
        private string _SaveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Pattern");
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

        }

        public void OpenSaveFilePath()
        {
            PlatformHelper.OpenFolder(Config.SaveFilePath);
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
