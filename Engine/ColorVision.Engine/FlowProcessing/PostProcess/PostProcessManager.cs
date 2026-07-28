#pragma warning disable CS8601,CS8625
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.FlowProcessing.PostProcess
{
    public class PostProcessConfig : ViewModelBase, IConfig
    {
        public static PostProcessConfig Instance => ConfigService.Instance.GetRequiredService<PostProcessConfig>();
        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public PostProcessConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this).ShowDialog());
        }

        [LocalizedDisplayName(nameof(Resources.DefaultSavePath)), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string SavePath { get => _SavePath; set { _SavePath = value; OnPropertyChanged(); } }
        private string _SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Batch"); 
    }


    public class PostProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(PostProcessManager));

        private const string PersistFileName = "PostProcessConfig.json";
        private static string PersistDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static PostProcessManager _instance;
        private static readonly object _locker = new();
        public static PostProcessManager GetInstance() { lock (_locker) { _instance ??= new PostProcessManager(); return _instance; } }

        public PostProcessConfig PostProcessConfig { get; set; }

        public ObservableCollection<IPostProcessor> Processes { get; } = new ObservableCollection<IPostProcessor>();

        public ObservableCollection<PostProcessMeta> ProcessMetas { get; } = new ObservableCollection<PostProcessMeta>();

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;
        public RelayCommand EditCommand { get; set; }

        public PostProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PostProcessMeta _SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand UpdateMetaCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PostProcessManager()
        {
            PostProcessConfig = PostProcessConfig.Instance;

            LoadProcesses();
            ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => Processes.Count > 0 && templateModels.Count > 0);
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            UpdateMetaCommand = new RelayCommand(a => UpdateMeta(), a => SelectedProcessMeta != null);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            LoadPersistedMetas();
        }

        private void LoadProcesses()
        {
            var processList = new System.Collections.Generic.List<IPostProcessor>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IPostProcessor).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IPostProcessor process)
                        {
                            processList.Add(process);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
            
            // Sort by metadata order, then by display name
            var sortedProcesses = processList
                .Select(p => new { Process = p, Metadata = PostProcessMetadata.FromProcess(p) })
                .OrderBy(x => x.Metadata.Order)
                .ThenBy(x => x.Metadata.DisplayName)
                .Select(x => x.Process);
            
            foreach (var process in sortedProcesses)
            {
                Processes.Add(process);
            }
        }

        private void ProcessMetas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PostProcessMeta meta in e.NewItems)
                {
                    meta.PropertyChanged += Meta_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (PostProcessMeta meta in e.OldItems)
                {
                    meta.PropertyChanged -= Meta_PropertyChanged;
                }
            }
            SavePersistedMetas();
            NotifyAllExecutionOrders();
        }

        /// <summary>
        /// Notifies all PostProcessMeta items to update their ExecutionOrder property.
        /// </summary>
        private void NotifyAllExecutionOrders()
        {
            foreach (var meta in ProcessMetas)
            {
                meta.NotifyExecutionOrderChanged();
            }
        }

        private void Meta_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Skip persistence for UI-only display properties like ExecutionOrder
            if (e.PropertyName == nameof(PostProcessMeta.ExecutionOrder))
                return;
            
            // 任意属性变更即持久化，避免频繁：可加节流，这里简单实现
            SavePersistedMetas();
        }

        public void Edit()
        {
            PostProcessManagerWindow processManagerWindow = new PostProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            processManagerWindow.DataContext = this;
            processManagerWindow.ShowDialog();
        }

        private void AddMeta()
        {
            var dialog = new PostProcessMetaEditWindow(
                templateModels,
                Processes,
                "新增后处理项")
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null || dialog.SelectedProcess == null)
                return;

            if (ProcessMetas.Any(meta => meta.Name.Equals(dialog.MetaName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DuplicateName, "ColorVision");
                return;
            }

            object? config = dialog.SelectedProcess.GetConfig();
            var meta = new PostProcessMeta
            {
                Name = dialog.MetaName,
                TemplateName = dialog.SelectedTemplate.Key,
                PostProcessor = dialog.SelectedProcess,
                ConfigJson = config == null ? string.Empty : JsonConvert.SerializeObject(config),
                Tag = dialog.MetaTag
            };
            ProcessMetas.Add(meta);
            SelectedProcessMeta = meta;
        }

        private void RemoveMeta()
        {
            if (SelectedProcessMeta != null)
            {
                ProcessMetas.Remove(SelectedProcessMeta);
                SelectedProcessMeta = null;
            }
        }

        private void UpdateMeta()
        {
            PostProcessMeta? selectedMeta = SelectedProcessMeta;
            if (selectedMeta == null)
                return;

            var dialog = new PostProcessMetaEditWindow(
                templateModels,
                Processes,
                "编辑后处理项",
                selectedMeta.Name,
                selectedMeta.TemplateName,
                selectedMeta.PostProcessor,
                selectedMeta.Tag)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null || dialog.SelectedProcess == null)
                return;

            if (ProcessMetas.Any(meta =>
                    !ReferenceEquals(meta, selectedMeta)
                    && meta.Name.Equals(dialog.MetaName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DuplicateName, "ColorVision");
                return;
            }

            object? config = dialog.SelectedProcess.GetConfig();
            selectedMeta.Name = dialog.MetaName;
            selectedMeta.TemplateName = dialog.SelectedTemplate.Key;
            selectedMeta.PostProcessor = dialog.SelectedProcess;
            selectedMeta.ConfigJson = config == null ? string.Empty : JsonConvert.SerializeObject(config);
            selectedMeta.Tag = dialog.MetaTag;
        }

        private bool CanMoveUp()
        {
            return SelectedProcessMeta != null && ProcessMetas.IndexOf(SelectedProcessMeta) > 0;
        }

        private void MoveUp()
        {
            if (!CanMoveUp()) return;
            int index = ProcessMetas.IndexOf(SelectedProcessMeta);
            ProcessMetas.Move(index, index - 1);
        }

        private bool CanMoveDown()
        {
            return SelectedProcessMeta != null && ProcessMetas.IndexOf(SelectedProcessMeta) < ProcessMetas.Count - 1;
        }

        private void MoveDown()
        {
            if (!CanMoveDown()) return;
            int index = ProcessMetas.IndexOf(SelectedProcessMeta);
            ProcessMetas.Move(index, index + 1);
        }

        private void LoadPersistedMetas()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                if (!File.Exists(PersistFilePath)) return;

                string json = File.ReadAllText(PersistFilePath);
                var list = JsonConvert.DeserializeObject<List<PostProcessPersist>>(json) ?? new List<PostProcessPersist>();
                ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // 暂停事件
                foreach (var item in list)
                {
                    IPostProcessor proc = null;
                    string processTypeName = item.ProcessTypeFullName;
                    var templateProc = Processes.FirstOrDefault(p => p.GetType().FullName == processTypeName);
                    
                    if (templateProc != null)
                    {
                        // Create a new instance for each meta to have its own config
                        proc = templateProc.CreateInstance();
                    }
                    else
                    {
                        // 尝试反射创建
                        try
                        {
                            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(x => x.FullName == processTypeName && typeof(IPostProcessor).IsAssignableFrom(x));
                            if (t != null)
                            {
                                proc = Activator.CreateInstance(t) as IPostProcessor;
                                if (proc != null && !Processes.Any(p => p.GetType().FullName == proc.GetType().FullName))
                                    Processes.Add(proc);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"Unable to instantiate process type {processTypeName}: {ex.Message}");
                        }
                    }
                    
                    PostProcessMeta meta = new PostProcessMeta() 
                    { 
                        Name = item.Name, 
                        TemplateName = item.TemplateName, 
                        PostProcessor = proc,
                        ConfigJson = item.ConfigJson,
                        Tag = item.Tag
                    };
                    
                    // Apply the stored config to the batch process
                    meta.ApplyConfig();
                    
                    meta.PropertyChanged += Meta_PropertyChanged;
                    ProcessMetas.Add(meta);
                }
                ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged; // 恢复事件
            }
            catch (Exception ex)
            {
                log.Error("加载ProcessMetas失败", ex);
            }
        }

        private void SavePersistedMetas()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                var list = ProcessMetas.Select(m => new PostProcessPersist
                {
                    Name = m.Name,
                    TemplateName = m.TemplateName,
                    ProcessTypeFullName = m.PostProcessor?.GetType().FullName,
                    ConfigJson = m.ConfigJson,
                    Tag = m.Tag
                }).ToList();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存ProcessMetas失败", ex);
            }
        }

        public void GenStepBar(HandyControl.Controls.StepBar stepBar)
        {
            stepBar.Items.Clear();
            foreach (var item in ProcessMetas)
            {
                HandyControl.Controls.StepBarItem stepBarItem = new HandyControl.Controls.StepBarItem() { Content = item.Name };
                stepBar.Items.Add(stepBarItem);
            }
        }

    }
}
