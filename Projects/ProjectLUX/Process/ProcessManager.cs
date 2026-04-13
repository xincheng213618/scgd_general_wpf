using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.IO;

namespace ProjectLUX.Process
{

    public class ProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ProcessManager));
        private const string PersistFileName = "ProcessMetas.json";
        private const string GroupPersistFileName = "ProcessGroups.json";
        private static string PersistDirectory => ViewResultManager.DirectoryPath; // 复用配置目录
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);
        private static string GroupPersistFilePath => Path.Combine(PersistDirectory, GroupPersistFileName);

        private static ProcessManager _instance;
        private static readonly object _locker = new();
        public static ProcessManager GetInstance() { lock (_locker) { _instance ??= new ProcessManager(); return _instance; } }

        public ObservableCollection<IProcess> Processes { get; } = new ObservableCollection<IProcess>();

        /// <summary>
        /// 所有流程组
        /// </summary>
        public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new ObservableCollection<ProcessGroup>();

        /// <summary>
        /// 当前激活的组索引
        /// </summary>
        public int ActiveGroupIndex
        {
            get => _ActiveGroupIndex;
            set
            {
                if (value < 0 || (ProcessGroups.Count > 0 && value >= ProcessGroups.Count))
                    return;
                if (_ActiveGroupIndex != value)
                {
                    UnhookProcessMetasEvents();
                    _ActiveGroupIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveGroup));
                    OnPropertyChanged(nameof(ProcessMetas));
                    HookProcessMetasEvents();
                    ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
                    SavePersistedGroups();
                }
            }
        }
        private int _ActiveGroupIndex;

        /// <summary>
        /// 当前激活组
        /// </summary>
        [JsonIgnore]
        public ProcessGroup ActiveGroup => (ProcessGroups.Count > 0 && _ActiveGroupIndex >= 0 && _ActiveGroupIndex < ProcessGroups.Count)
            ? ProcessGroups[_ActiveGroupIndex] : null;

        /// <summary>
        /// 当前组的 ProcessMetas（兼容属性，与 ActiveGroup.ProcessMetas 同步）
        /// </summary>
        public ObservableCollection<ProcessMeta> ProcessMetas => ActiveGroup?.ProcessMetas ?? _emptyMetas;
        private static readonly ObservableCollection<ProcessMeta> _emptyMetas = new();

        /// <summary>
        /// 组切换事件
        /// </summary>
        public event EventHandler ActiveGroupChanged;

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;

        public RelayCommand EditCommand { get; set; }

        // New properties for creation
        public string NewMetaName { get => _NewMetaName; set { _NewMetaName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewMetaName;

        public TemplateModel<FlowParam> SelectedTemplate { get => _SelectedTemplate; set { _SelectedTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _SelectedTemplate;

        public IProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IProcess _SelectedProcess;

        public TemplateModel<FlowParam> UpdateTemplate { get => _UpdateTemplate; set { _UpdateTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _UpdateTemplate;

        public IProcess UpdateProcess { get => _UpdateProcess; set { _UpdateProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IProcess _UpdateProcess;

        public ProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); OnSelectedProcessMetaChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private ProcessMeta _SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand UpdateMetaCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        // Group management commands
        public RelayCommand AddGroupCommand { get; set; }
        public RelayCommand RemoveGroupCommand { get; set; }
        public RelayCommand RenameGroupCommand { get; set; }
        public RelayCommand DuplicateGroupCommand { get; set; }

        /// <summary>
        /// 新组名称（UI绑定）
        /// </summary>
        public string NewGroupName { get => _NewGroupName; set { _NewGroupName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewGroupName;

        public ProcessManager()
        {
            LoadProcesses();
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => CanAddMeta());
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            UpdateMetaCommand = new RelayCommand(a => UpdateMeta(), a => CanUpdateMeta());
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());

            AddGroupCommand = new RelayCommand(a => AddGroup(), a => !string.IsNullOrWhiteSpace(NewGroupName));
            RemoveGroupCommand = new RelayCommand(a => RemoveGroup(), a => ProcessGroups.Count > 1);
            RenameGroupCommand = new RelayCommand(a => RenameGroup(), a => ActiveGroup != null && !string.IsNullOrWhiteSpace(NewGroupName));
            DuplicateGroupCommand = new RelayCommand(a => DuplicateGroup(), a => ActiveGroup != null);

            LoadPersistedGroups();
        }

        #region Group Management

        private void AddGroup()
        {
            if (string.IsNullOrWhiteSpace(NewGroupName)) return;
            if (ProcessGroups.Any(g => g.Name.Equals(NewGroupName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "组名重复", "ColorVision");
                return;
            }
            var group = new ProcessGroup { Name = NewGroupName };
            ProcessGroups.Add(group);
            ActiveGroupIndex = ProcessGroups.Count - 1;
            NewGroupName = string.Empty;
            SavePersistedGroups();
        }

        private void RemoveGroup()
        {
            if (ProcessGroups.Count <= 1)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "至少保留一个组", "ColorVision");
                return;
            }
            if (ActiveGroup == null) return;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"确定要删除组 \"{ActiveGroup.Name}\" 吗？", "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            UnhookProcessMetasEvents();
            int idx = _ActiveGroupIndex;
            ProcessGroups.RemoveAt(idx);
            _ActiveGroupIndex = Math.Min(idx, ProcessGroups.Count - 1);
            OnPropertyChanged(nameof(ActiveGroupIndex));
            OnPropertyChanged(nameof(ActiveGroup));
            OnPropertyChanged(nameof(ProcessMetas));
            HookProcessMetasEvents();
            ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
            SavePersistedGroups();
        }

        private void RenameGroup()
        {
            if (ActiveGroup == null || string.IsNullOrWhiteSpace(NewGroupName)) return;
            if (ProcessGroups.Any(g => g != ActiveGroup && g.Name.Equals(NewGroupName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "组名重复", "ColorVision");
                return;
            }
            ActiveGroup.Name = NewGroupName;
            NewGroupName = string.Empty;
            SavePersistedGroups();
        }

        private void DuplicateGroup()
        {
            if (ActiveGroup == null) return;
            string baseName = ActiveGroup.Name + "_Copy";
            string newName = baseName;
            int counter = 1;
            while (ProcessGroups.Any(g => g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            {
                newName = $"{baseName}_{counter++}";
            }

            var newGroup = new ProcessGroup { Name = newName };
            foreach (var meta in ActiveGroup.ProcessMetas)
            {
                var newProc = meta.Process?.CreateInstance();
                if (newProc != null && !string.IsNullOrEmpty(meta.ConfigJson))
                {
                    newProc.SetProcessConfig(meta.ConfigJson);
                }
                var newMeta = new ProcessMeta
                {
                    Name = meta.Name,
                    FlowTemplate = meta.FlowTemplate,
                    Process = newProc,
                    IsEnabled = meta.IsEnabled,
                    ConfigJson = meta.ConfigJson,
                    SocketCode = meta.SocketCode
                };
                newGroup.ProcessMetas.Add(newMeta);
            }
            ProcessGroups.Add(newGroup);
            ActiveGroupIndex = ProcessGroups.Count - 1;
            SavePersistedGroups();
        }

        #endregion

        #region ProcessMeta Events

        private void HookProcessMetasEvents()
        {
            var metas = ActiveGroup?.ProcessMetas;
            if (metas == null) return;
            metas.CollectionChanged += ProcessMetas_CollectionChanged;
            foreach (var meta in metas)
            {
                meta.PropertyChanged += Meta_PropertyChanged;
            }
        }

        private void UnhookProcessMetasEvents()
        {
            var metas = ActiveGroup?.ProcessMetas;
            if (metas == null) return;
            metas.CollectionChanged -= ProcessMetas_CollectionChanged;
            foreach (var meta in metas)
            {
                meta.PropertyChanged -= Meta_PropertyChanged;
            }
        }

        #endregion

        private void LoadProcesses()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IProcess).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IProcess process)
                        {
                            Processes.Add(process);
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }
        }

        private void ProcessMetas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ProcessMeta meta in e.NewItems)
                {
                    meta.PropertyChanged += Meta_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (ProcessMeta meta in e.OldItems)
                {
                    meta.PropertyChanged -= Meta_PropertyChanged;
                }
            }
            SavePersistedGroups();
        }

        private void Meta_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SavePersistedGroups();
        }

        public void Edit()
        {
            ProcessManagerWindow processManagerWindow = new ProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            processManagerWindow.DataContext = this;
            processManagerWindow.ShowDialog();
        }

        private bool CanAddMeta()
        {
            return !string.IsNullOrWhiteSpace(NewMetaName) && SelectedTemplate != null && SelectedProcess != null && ActiveGroup != null;
        }

        private void AddMeta()
        {
            if (!CanAddMeta()) return;
            if (ProcessMetas.Any(m => m.Name.Equals(NewMetaName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }
            // Create a new instance of the process to ensure independent config
            var newProcessInstance = SelectedProcess.CreateInstance();
            ProcessMetas.Add(new ProcessMeta
            {
                Name = NewMetaName,
                FlowTemplate = SelectedTemplate.Key,
                Process = newProcessInstance
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
                // Populate update fields when a ProcessMeta is selected
                UpdateTemplate = templateModels.FirstOrDefault(t => t.Key == SelectedProcessMeta.FlowTemplate);
                UpdateProcess = Processes.FirstOrDefault(p => p.GetType().FullName == SelectedProcessMeta.Process?.GetType().FullName);
            }
        }

        private bool CanUpdateMeta()
        {
            return SelectedProcessMeta != null && UpdateTemplate != null && UpdateProcess != null;
        }

        private void UpdateMeta()
        {
            if (!CanUpdateMeta()) return;
            
            // Update the selected ProcessMeta with new values
            SelectedProcessMeta.FlowTemplate = UpdateTemplate.Key;
            
            // Create a new instance of the process to ensure independent config
            var newProcessInstance = UpdateProcess.CreateInstance();
            
            // If the same process type, preserve the existing config
            if (SelectedProcessMeta.Process?.GetType().FullName == newProcessInstance.GetType().FullName 
                && !string.IsNullOrEmpty(SelectedProcessMeta.ConfigJson))
            {
                newProcessInstance.SetProcessConfig(SelectedProcessMeta.ConfigJson);
            }
            
            SelectedProcessMeta.Process = newProcessInstance;
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

        #region Persistence

        private void LoadPersistedGroups()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                // Try new format first
                if (File.Exists(GroupPersistFilePath))
                {
                    LoadFromGroupsFile();
                }
                // Fall back to old format (auto-migrate)
                else if (File.Exists(PersistFilePath))
                {
                    MigrateFromOldFormat();
                }

                // Ensure we have at least one group
                if (ProcessGroups.Count == 0)
                {
                    ProcessGroups.Add(new ProcessGroup { Name = "Default" });
                    _ActiveGroupIndex = 0;
                }

                OnPropertyChanged(nameof(ActiveGroupIndex));
                OnPropertyChanged(nameof(ActiveGroup));
                OnPropertyChanged(nameof(ProcessMetas));
                HookProcessMetasEvents();
            }
            catch (Exception ex)
            {
                log.Error("加载ProcessGroups失败", ex);
            }
        }

        private void LoadFromGroupsFile()
        {
            string json = File.ReadAllText(GroupPersistFilePath);
            var root = JsonConvert.DeserializeObject<ProcessGroupsRoot>(json);
            if (root == null || root.Groups == null || root.Groups.Count == 0)
            {
                ProcessGroups.Add(new ProcessGroup { Name = "Default" });
                _ActiveGroupIndex = 0;
                return;
            }

            foreach (var gp in root.Groups)
            {
                var group = new ProcessGroup { Name = gp.Name };
                foreach (var item in gp.Metas)
                {
                    var meta = DeserializeProcessMeta(item);
                    group.ProcessMetas.Add(meta);
                }
                ProcessGroups.Add(group);
            }

            _ActiveGroupIndex = Math.Max(0, Math.Min(root.ActiveGroupIndex, ProcessGroups.Count - 1));
        }

        private void MigrateFromOldFormat()
        {
            log.Info("检测到旧格式 ProcessMetas.json，自动迁移到 ProcessGroups.json");
            string json = File.ReadAllText(PersistFilePath);
            var list = JsonConvert.DeserializeObject<List<ProcessMetaPersist>>(json) ?? new List<ProcessMetaPersist>();

            var defaultGroup = new ProcessGroup { Name = "Default" };
            foreach (var item in list)
            {
                var meta = DeserializeProcessMeta(item);
                defaultGroup.ProcessMetas.Add(meta);
            }
            ProcessGroups.Add(defaultGroup);
            _ActiveGroupIndex = 0;

            // Save in new format
            SavePersistedGroups();
        }

        private ProcessMeta DeserializeProcessMeta(ProcessMetaPersist item)
        {
            IProcess templateProc = Processes.FirstOrDefault(p => p.GetType().FullName == item.ProcessTypeFullName);
            if (templateProc == null)
            {
                try
                {
                    var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(x => x.FullName == item.ProcessTypeFullName && typeof(IProcess).IsAssignableFrom(x));
                    if (t != null)
                    {
                        templateProc = Activator.CreateInstance(t) as IProcess;
                        if (templateProc != null && !Processes.Any(p => p.GetType().FullName == templateProc.GetType().FullName))
                            Processes.Add(templateProc);
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"无法实例化进程类型 {item.ProcessTypeFullName}: {ex.Message}");
                }
            }

            IProcess proc = templateProc?.CreateInstance();

            ProcessMeta meta = new ProcessMeta()
            {
                Name = item.Name,
                FlowTemplate = item.FlowTemplate,
                Process = proc,
                IsEnabled = item.IsEnabled,
                ConfigJson = item.ConfigJson,
                SocketCode = item.SocketCode
            };

            meta.ApplyConfig();
            return meta;
        }

        private void SavePersistedGroups()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                var root = new ProcessGroupsRoot
                {
                    Version = 1,
                    ActiveGroupIndex = _ActiveGroupIndex,
                    Groups = ProcessGroups.Select(g => new ProcessGroupPersist
                    {
                        Name = g.Name,
                        Metas = g.ProcessMetas.Select(m => new ProcessMetaPersist
                        {
                            Name = m.Name,
                            FlowTemplate = m.FlowTemplate,
                            ProcessTypeFullName = m.Process?.GetType().FullName,
                            IsEnabled = m.IsEnabled,
                            SocketCode = m.SocketCode,
                            ConfigJson = m.ConfigJson
                        }).ToList()
                    }).ToList()
                };

                string json = JsonConvert.SerializeObject(root, Formatting.Indented);
                File.WriteAllText(GroupPersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存ProcessGroups失败", ex);
            }
        }

        #endregion

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
