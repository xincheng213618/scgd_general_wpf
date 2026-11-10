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
        [JsonIgnore, Browsable(false)]
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
        internal static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static PostProcessManager _instance;
        private static readonly object _locker = new();
        private readonly string _persistFilePath;
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

        public PostProcessManager() : this(PersistFilePath)
        {
        }

        internal PostProcessManager(string persistFilePath)
        {
            _persistFilePath = Path.GetFullPath(persistFilePath);
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
            foreach (Type type in AssemblyHandler.GetInstance().GetImplementationTypes(typeof(IPostProcessor)))
            {
                try
                {
                    if (Activator.CreateInstance(type) is IPostProcessor process)
                    {
                        processList.Add(process);
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
                EngineLocalization.Get("新增后处理项"))
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
                Tag = dialog.MetaTag,
                FailurePolicy = dialog.FailurePolicy
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
                EngineLocalization.Get("编辑后处理项"),
                selectedMeta.Name,
                selectedMeta.TemplateName,
                selectedMeta.PostProcessor ?? Processes.FirstOrDefault(process => process.GetType().FullName == selectedMeta.ProcessTypeFullName),
                selectedMeta.Tag,
                selectedMeta.FailurePolicy)
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
            string configJson = config == null ? string.Empty : JsonConvert.SerializeObject(config);
            selectedMeta.PropertyChanged -= Meta_PropertyChanged;
            try
            {
                selectedMeta.Name = dialog.MetaName;
                selectedMeta.TemplateName = dialog.SelectedTemplate.Key;
                selectedMeta.ConfigJson = configJson;
                selectedMeta.Tag = dialog.MetaTag;
                selectedMeta.FailurePolicy = dialog.FailurePolicy;
                // Keep a failed processor unavailable until the complete replacement is ready.
                selectedMeta.PostProcessor = dialog.SelectedProcess;
            }
            finally { selectedMeta.PropertyChanged += Meta_PropertyChanged; }
            SavePersistedMetas();
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
                Directory.CreateDirectory(Path.GetDirectoryName(_persistFilePath)!);
                if (!File.Exists(_persistFilePath)) return;

                string json = File.ReadAllText(_persistFilePath);
                var list = JsonConvert.DeserializeObject<List<PostProcessPersist?>>(json) ?? new List<PostProcessPersist?>();
                ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // 暂停事件
                try
                {
                    foreach (var item in list)
                    {
                        if (item == null)
                        {
                            log.Warn("Ignored an empty postprocessor configuration entry.");
                            continue;
                        }
                        PostProcessMeta meta = LoadMeta(item);
                        meta.PropertyChanged += Meta_PropertyChanged;
                        ProcessMetas.Add(meta);
                    }
                }
                finally { ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged; }
            }
            catch (Exception ex)
            {
                log.Error("加载ProcessMetas失败", ex);
            }
        }

        private PostProcessMeta LoadMeta(PostProcessPersist item)
        {
            var meta = new PostProcessMeta
            {
                Name = item.Name,
                TemplateName = item.TemplateName,
                PersistedProcessorTypeFullName = item.ProcessTypeFullName,
                ConfigJson = item.ConfigJson,
                Tag = item.Tag,
                FailurePolicy = item.FailurePolicy
            };

            IPostProcessor? prototype;
            try
            {
                prototype = FindProcessorPrototype(item.ProcessTypeFullName);
                if (prototype == null) return meta;
                meta.PostProcessor = prototype.CreateInstance();
            }
            catch (Exception error)
            {
                log.Warn($"Unable to create postprocessor {item.ProcessTypeFullName} for {item.Name}.", error);
                return meta;
            }

            try
            {
                if (ReferenceEquals(meta.PostProcessor, prototype) && !string.IsNullOrEmpty(meta.ConfigJson) && prototype.GetConfig() != null)
                    throw new InvalidOperationException("The configured postprocessor did not create an independent instance.");
                meta.ApplyConfig();
            }
            catch (Exception error)
            {
                meta.MarkUnreadableConfiguration();
                log.Warn($"Unable to apply postprocessor configuration for {item.Name}.", error);
            }
            return meta;
        }

        private IPostProcessor? FindProcessorPrototype(string? typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;
            IPostProcessor? prototype = Processes.FirstOrDefault(process => process.GetType().FullName == typeName);
            if (prototype != null) return prototype;

            Type? type = AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(candidate => candidate.FullName == typeName && typeof(IPostProcessor).IsAssignableFrom(candidate));
            if (type == null || Activator.CreateInstance(type) is not IPostProcessor discovered) return null;
            Processes.Add(discovered);
            return discovered;
        }

        private void SavePersistedMetas()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_persistFilePath)!);
                var list = ProcessMetas.Select(m => new PostProcessPersist
                {
                    Name = m.Name,
                    TemplateName = m.TemplateName,
                    ProcessTypeFullName = m.PostProcessor?.GetType().FullName ?? m.PersistedProcessorTypeFullName,
                    ConfigJson = m.ConfigJson,
                    Tag = m.Tag,
                    FailurePolicy = m.FailurePolicy
                }).ToList();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(_persistFilePath, json);
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
