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

        public PreProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessMeta _SelectedProcessMeta;

        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PreProcessManager()
        {
            LoadProcesses();
            ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            LoadPersistedMetas();
            // Auto-populate all discovered preprocessors if not already present
            InitializeAllPreProcessors();
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

        /// <summary>
        /// Initializes PreProcessMeta entries for all discovered preprocessors across all templates.
        /// Creates entries for each combination of template and preprocessor.
        /// </summary>
        private void InitializeAllPreProcessors()
        {
            ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // Pause events
            
            try
            {
                foreach (var template in templateModels)
                {
                    foreach (var process in Processes)
                    {
                        // Check if this combination already exists
                        var existingMeta = ProcessMetas.FirstOrDefault(m => 
                            string.Equals(m.TemplateName, template.Key, StringComparison.OrdinalIgnoreCase) &&
                            m.PreProcess?.GetType().FullName == process.GetType().FullName);
                        
                        if (existingMeta == null)
                        {
                            // Create a new instance for this meta
                            var newProcess = process.CreateInstance();
                            var metadata = PreProcessMetadata.FromProcess(newProcess);
                            
                            var meta = new PreProcessMeta
                            {
                                Name = $"{template.Key}_{metadata.DisplayName}",
                                TemplateName = template.Key,
                                PreProcess = newProcess,
                                IsEnabled = false // Default to disabled for new entries
                            };
                            meta.PropertyChanged += Meta_PropertyChanged;
                            ProcessMetas.Add(meta);
                        }
                    }
                }
            }
            finally
            {
                ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged; // Resume events
                SavePersistedMetas();
                NotifyAllExecutionOrders();
            }
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
                        Tag = item.Tag,
                        IsEnabled = item.IsEnabled
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
                    Tag = m.Tag,
                    IsEnabled = m.IsEnabled
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
