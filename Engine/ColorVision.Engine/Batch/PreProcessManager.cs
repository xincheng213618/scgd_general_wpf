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
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Batch
{
    public class PreProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(PreProcessManager));

        private const string PersistFileName = "PreProcessConfig.json";
        private static string PersistDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static PreProcessManager _instance;
        private static readonly object _locker = new();
        public static PreProcessManager GetInstance() { lock (_locker) { _instance ??= new PreProcessManager(); return _instance; } }

        public ObservableCollection<IPreProcess> Processes { get; } = new ObservableCollection<IPreProcess>();

        public ObservableCollection<PreProcessMeta> ProcessMetas { get; } = new ObservableCollection<PreProcessMeta>();

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;
        public RelayCommand EditCommand { get; set; }

        // New properties for creation
        public string NewMetaName { get => _NewMetaName; set { _NewMetaName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewMetaName;

        public TemplateModel<FlowParam> SelectedTemplate { get => _SelectedTemplate; set { _SelectedTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _SelectedTemplate;

        public IPreProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IPreProcess _SelectedProcess;

        public TemplateModel<FlowParam> UpdateTemplate { get => _UpdateTemplate; set { _UpdateTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _UpdateTemplate;

        public IPreProcess UpdateProcess { get => _UpdateProcess; set { _UpdateProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IPreProcess _UpdateProcess;

        public PreProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); OnSelectedProcessMetaChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessMeta _SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand UpdateMetaCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PreProcessManager()
        {
            LoadProcesses();
            ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => CanAddMeta());
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            UpdateMetaCommand = new RelayCommand(a => UpdateMeta(), a => CanUpdateMeta());
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            LoadPersistedMetas();
        }

        private void LoadProcesses()
        {
            var processList = new System.Collections.Generic.List<IPreProcess>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IPreProcess).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IPreProcess process)
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
                .Select(p => new { Process = p, Metadata = PreProcessMetadata.FromProcess(p) })
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
                foreach (PreProcessMeta meta in e.NewItems)
                {
                    meta.PropertyChanged += Meta_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (PreProcessMeta meta in e.OldItems)
                {
                    meta.PropertyChanged -= Meta_PropertyChanged;
                }
            }
            SavePersistedMetas();
            NotifyAllExecutionOrders();
        }

        /// <summary>
        /// Notifies all PreProcessMeta items to update their ExecutionOrder property.
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
            if (e.PropertyName == nameof(PreProcessMeta.ExecutionOrder))
                return;
            
            SavePersistedMetas();
        }

        public void Edit()
        {
            PreProcessManagerWindow processManagerWindow = new PreProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            processManagerWindow.DataContext = this;
            processManagerWindow.ShowDialog();
        }

        private bool CanAddMeta()
        {
            return !string.IsNullOrWhiteSpace(NewMetaName) && SelectedTemplate != null && SelectedProcess != null;
        }

        private void AddMeta()
        {
            if (!CanAddMeta()) return;
            if (ProcessMetas.Any(m => m.Name.Equals(NewMetaName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.DuplicateName, "ColorVision");
                return;
            }
            
            // Create a new instance of the pre-processor for this meta to have its own config
            var newProcess = SelectedProcess.CreateInstance();
            
            ProcessMetas.Add(new PreProcessMeta
            {
                Name = NewMetaName,
                TemplateName = SelectedTemplate.Key,
                PreProcess = newProcess
            });
            NewMetaName = string.Empty;
        }

        private void RemoveMeta()
        {
            if (SelectedProcessMeta != null)
            {
                ProcessMetas.Remove(SelectedProcessMeta);
                SelectedProcessMeta = null;
            }
        }

        private void OnSelectedProcessMetaChanged()
        {
            if (SelectedProcessMeta != null)
            {
                // Populate update fields when a PreProcessMeta is selected
                UpdateTemplate = templateModels.FirstOrDefault(t => t.Key == SelectedProcessMeta.TemplateName);
                UpdateProcess = Processes.FirstOrDefault(p => p.GetType().FullName == SelectedProcessMeta.PreProcess?.GetType().FullName);
            }
        }

        private bool CanUpdateMeta()
        {
            return SelectedProcessMeta != null && UpdateTemplate != null && UpdateProcess != null;
        }

        private void UpdateMeta()
        {
            if (!CanUpdateMeta()) return;
            
            // Update the selected PreProcessMeta with new values
            SelectedProcessMeta.TemplateName = UpdateTemplate.Key;
            // Create a new instance to avoid sharing between multiple metas
            SelectedProcessMeta.PreProcess = UpdateProcess.CreateInstance();
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
                var list = JsonConvert.DeserializeObject<List<PreProcessMetaPersist>>(json) ?? new List<PreProcessMetaPersist>();
                ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // 暂停事件
                foreach (var item in list)
                {
                    IPreProcess proc = null;
                    var templateProc = Processes.FirstOrDefault(p => p.GetType().FullName == item.ProcessTypeFullName);
                    
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
                            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(x => x.FullName == item.ProcessTypeFullName && typeof(IPreProcess).IsAssignableFrom(x));
                            if (t != null)
                            {
                                proc = Activator.CreateInstance(t) as IPreProcess;
                                if (proc != null && !Processes.Any(p => p.GetType().FullName == proc.GetType().FullName))
                                    Processes.Add(proc);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn(ColorVision.Engine.Properties.Resources.UnableToInstantiateProcessType+$" {item.ProcessTypeFullName}: {ex.Message}");
                        }
                    }
                    
                    PreProcessMeta meta = new PreProcessMeta() 
                    { 
                        Name = item.Name, 
                        TemplateName = item.TemplateName, 
                        PreProcess = proc,
                        ConfigJson = item.ConfigJson,
                        Tag = item.Tag
                    };
                    
                    // Apply the stored config to the pre-processor
                    meta.ApplyConfig();
                    
                    meta.PropertyChanged += Meta_PropertyChanged;
                    ProcessMetas.Add(meta);
                }
                ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged; // 恢复事件
            }
            catch (Exception ex)
            {
                log.Error("加载PreProcessMetas失败", ex);
            }
        }

        private void SavePersistedMetas()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                var list = ProcessMetas.Select(m => new PreProcessMetaPersist
                {
                    Name = m.Name,
                    TemplateName = m.TemplateName,
                    ProcessTypeFullName = m.PreProcess?.GetType().FullName,
                    ConfigJson = m.ConfigJson,
                    Tag = m.Tag
                }).ToList();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存PreProcessMetas失败", ex);
            }
        }
    }
}
