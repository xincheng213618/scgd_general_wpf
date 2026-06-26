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
using Newtonsoft.Json.Linq;
using System.IO;
using Microsoft.Win32;

namespace ProjectARVRPro.Process
{
    public class ProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ProcessManager));
        private const string PersistFileName = "ProcessMetas.json";
        private const string GroupPersistFileName = "ProcessGroups.json";
        private const string ExportConfigFilter = "ARVR流程配置 (*.arvrprocess.json)|*.arvrprocess.json|JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*";
        private static string PersistDirectory => ViewResultManager.DirectoryPath;
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);
        private static string GroupPersistFilePath => Path.Combine(PersistDirectory, GroupPersistFileName);
        private static JsonSerializerSettings ExportJsonSerializerSettings => new()
        {
            TypeNameHandling = TypeNameHandling.All,
            Formatting = Formatting.Indented
        };

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
                    // Unhook old group events
                    UnhookProcessMetasEvents();
                    _ActiveGroupIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ActiveGroup));
                    OnPropertyChanged(nameof(ProcessMetas));
                    // Hook new group events
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
        public ProcessGroup? ActiveGroup => (ProcessGroups.Count > 0 && _ActiveGroupIndex >= 0 && _ActiveGroupIndex < ProcessGroups.Count)
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

        public ProcessMeta? SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private ProcessMeta? _SelectedProcessMeta;

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
        public RelayCommand ImportConfigCommand { get; set; }
        public RelayCommand ExportConfigCommand { get; set; }

        /// <summary>
        /// 新组名称（UI绑定）
        /// </summary>
        public string NewGroupName { get => _NewGroupName; set { _NewGroupName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewGroupName;

        public ProcessManager()
        {
            LoadProcesses();
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => ActiveGroup != null);
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            UpdateMetaCommand = new RelayCommand(a => UpdateMeta(), a => SelectedProcessMeta != null);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());

            AddGroupCommand = new RelayCommand(a => AddGroup(), a => !string.IsNullOrWhiteSpace(NewGroupName));
            RemoveGroupCommand = new RelayCommand(a => RemoveGroup(), a => ProcessGroups.Count > 1);
            RenameGroupCommand = new RelayCommand(a => RenameGroup(), a => ActiveGroup != null && !string.IsNullOrWhiteSpace(NewGroupName));
            DuplicateGroupCommand = new RelayCommand(a => DuplicateGroup(), a => ActiveGroup != null);
            ImportConfigCommand = new RelayCommand(a => ImportConfig());
            ExportConfigCommand = new RelayCommand(a => ExportConfig(), a => ProcessGroups.Count > 0);

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
                    PictureSwitchConfig = meta.PictureSwitchConfig.Clone()
                };
                newGroup.ProcessMetas.Add(newMeta);
            }
            ProcessGroups.Add(newGroup);
            ActiveGroupIndex = ProcessGroups.Count - 1;
            SavePersistedGroups();
        }

        private void ExportConfig()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Title = "导出流程配置",
                    Filter = ExportConfigFilter,
                    DefaultExt = "arvrprocess.json",
                    FileName = $"ARVRProcessConfig_{DateTime.Now:yyyyMMdd_HHmmss}.arvrprocess.json"
                };

                if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                    return;

                var exportRoot = new ProcessManagerConfigPersist
                {
                    Version = 1,
                    ExportedAt = DateTime.Now,
                    ProcessGroups = CreateProcessGroupsRoot(),
                    RecipeConfig = RecipeManager.GetInstance().RecipeConfig
                };

                string json = JsonConvert.SerializeObject(exportRoot, ExportJsonSerializerSettings);
                File.WriteAllText(dialog.FileName, json);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"流程配置已导出到:\n{dialog.FileName}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error("导出流程配置失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"导出流程配置失败:\n{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportConfig()
        {
            var dialog = new OpenFileDialog
            {
                Title = "导入流程配置",
                Filter = ExportConfigFilter,
                DefaultExt = "arvrprocess.json"
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            if (!TryReadConfigFile(dialog.FileName, out var importedGroups, out var importedRecipe, out var warningMessage, out var errorMessage))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"导入流程配置失败:\n{errorMessage}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            int groupCount = importedGroups.Groups?.Count ?? 0;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"导入后将替换当前流程组配置，共 {groupCount} 个组。是否继续？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                ApplyImportedGroups(importedGroups);

                if (importedRecipe != null)
                {
                    RecipeManager.GetInstance().RecipeConfig = importedRecipe;
                    RecipeManager.GetInstance().Save();
                }

                string message = $"流程配置已导入，共 {ProcessGroups.Count} 个组。";
                if (!string.IsNullOrWhiteSpace(warningMessage))
                    message += $"\n{warningMessage}";
                MessageBox.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error("应用导入流程配置失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"应用导入流程配置失败:\n{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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

        private void AddMeta()
        {
            if (ActiveGroup == null) return;

            var dialog = new ProcessMetaEditWindow(templateModels, Processes, "新增流程项")
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true) return;

            if (dialog.SelectedTemplate == null || dialog.SelectedProcess == null) return;

            if (HasDuplicateMetaName(dialog.MetaName))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }

            var newMeta = new ProcessMeta
            {
                Name = dialog.MetaName,
                FlowTemplate = dialog.SelectedTemplate.Key,
                Process = dialog.SelectedProcess.CreateInstance(),
                IsEnabled = dialog.IsMetaEnabled
            };

            ProcessMetas.Add(newMeta);
            SelectedProcessMeta = newMeta;
        }

        private void RemoveMeta()
        {
            if (SelectedProcessMeta != null)
            {
                ProcessMetas.Remove(SelectedProcessMeta);
                SelectedProcessMeta = null;
            }
        }

        private bool HasDuplicateMetaName(string name, ProcessMeta? ignoredMeta = null)
        {
            return ProcessMetas.Any(m => !ReferenceEquals(m, ignoredMeta) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private void UpdateMeta()
        {
            if (SelectedProcessMeta == null) return;

            var dialog = new ProcessMetaEditWindow(
                templateModels,
                Processes,
                $"编辑流程项 - {SelectedProcessMeta.Name}",
                SelectedProcessMeta.Name,
                SelectedProcessMeta.FlowTemplate,
                SelectedProcessMeta.Process,
                SelectedProcessMeta.IsEnabled)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true) return;

            if (dialog.SelectedTemplate == null || dialog.SelectedProcess == null) return;

            if (HasDuplicateMetaName(dialog.MetaName, SelectedProcessMeta))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }

            var oldProcessType = SelectedProcessMeta.Process?.GetType().FullName;
            var newProcessInstance = dialog.SelectedProcess.CreateInstance();
            var newProcessType = newProcessInstance.GetType().FullName;
            var retainedConfigJson = string.Equals(oldProcessType, newProcessType, StringComparison.Ordinal)
                ? SelectedProcessMeta.ConfigJson
                : string.Empty;

            if (!string.IsNullOrEmpty(retainedConfigJson))
            {
                newProcessInstance.SetProcessConfig(retainedConfigJson);
            }

            SelectedProcessMeta.Name = dialog.MetaName;
            SelectedProcessMeta.FlowTemplate = dialog.SelectedTemplate.Key;
            SelectedProcessMeta.IsEnabled = dialog.IsMetaEnabled;
            SelectedProcessMeta.ConfigJson = retainedConfigJson;
            SelectedProcessMeta.Process = newProcessInstance;
        }

        private bool CanMoveUp()
        {
            var selectedMeta = SelectedProcessMeta;
            return selectedMeta != null && ProcessMetas.IndexOf(selectedMeta) > 0;
        }

        private void MoveUp()
        {
            if (!CanMoveUp()) return;
            var selectedMeta = SelectedProcessMeta;
            if (selectedMeta == null) return;
            int index = ProcessMetas.IndexOf(selectedMeta);
            ProcessMetas.Move(index, index - 1);
        }

        private bool CanMoveDown()
        {
            var selectedMeta = SelectedProcessMeta;
            return selectedMeta != null && ProcessMetas.IndexOf(selectedMeta) < ProcessMetas.Count - 1;
        }

        private void MoveDown()
        {
            if (!CanMoveDown()) return;
            var selectedMeta = SelectedProcessMeta;
            if (selectedMeta == null) return;
            int index = ProcessMetas.IndexOf(selectedMeta);
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
                PictureSwitchConfig = item.PictureSwitchConfig ?? new PictureSwitchConfig()
            };

            meta.ApplyConfig();
            return meta;
        }

        private void SavePersistedGroups()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                var root = CreateProcessGroupsRoot();

                string json = JsonConvert.SerializeObject(root, Formatting.Indented);
                File.WriteAllText(GroupPersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存ProcessGroups失败", ex);
            }
        }

        public void SaveProcessGroups()
        {
            SavePersistedGroups();
        }

        private ProcessGroupsRoot CreateProcessGroupsRoot()
        {
            return new ProcessGroupsRoot
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
                        ProcessTypeFullName = m.Process?.GetType().FullName ?? string.Empty,
                        IsEnabled = m.IsEnabled,
                        ConfigJson = GetProcessConfigJson(m),
                        PictureSwitchConfig = m.PictureSwitchConfig.Clone()
                    }).ToList()
                }).ToList()
            };
        }

        private static string GetProcessConfigJson(ProcessMeta meta)
        {
            var config = meta.Process?.GetProcessConfig();
            return config == null ? meta.ConfigJson : JsonConvert.SerializeObject(config);
        }

        private static bool TryReadConfigFile(string filePath, out ProcessGroupsRoot groupsRoot, out RecipeConfig? recipeConfig, out string warningMessage, out string errorMessage)
        {
            groupsRoot = new ProcessGroupsRoot();
            recipeConfig = null;
            warningMessage = string.Empty;
            errorMessage = string.Empty;

            try
            {
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    errorMessage = "配置文件为空。";
                    return false;
                }

                var token = JToken.Parse(json);

                if (token is JObject packageObject && packageObject[nameof(ProcessManagerConfigPersist.ProcessGroups)] != null)
                {
                    groupsRoot = packageObject[nameof(ProcessManagerConfigPersist.ProcessGroups)]?.ToObject<ProcessGroupsRoot>(JsonSerializer.Create(ExportJsonSerializerSettings)) ?? new ProcessGroupsRoot();

                    if (packageObject[nameof(ProcessManagerConfigPersist.RecipeConfig)] != null && packageObject[nameof(ProcessManagerConfigPersist.RecipeConfig)]?.Type != JTokenType.Null)
                    {
                        try
                        {
                            recipeConfig = packageObject[nameof(ProcessManagerConfigPersist.RecipeConfig)]?.ToObject<RecipeConfig>(JsonSerializer.Create(ExportJsonSerializerSettings));
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"导入Recipe配置失败，仅导入流程组配置: {ex.Message}");
                            warningMessage = "Recipe配置导入失败，已保留当前Recipe配置。";
                        }
                    }

                    return ValidateImportedGroups(groupsRoot, out errorMessage);
                }

                if (token is JObject groupsObject && groupsObject[nameof(ProcessGroupsRoot.Groups)] != null)
                {
                    groupsRoot = groupsObject.ToObject<ProcessGroupsRoot>() ?? new ProcessGroupsRoot();
                    return ValidateImportedGroups(groupsRoot, out errorMessage);
                }

                if (token is JArray legacyMetas)
                {
                    var metas = legacyMetas.ToObject<List<ProcessMetaPersist>>() ?? new List<ProcessMetaPersist>();
                    groupsRoot = new ProcessGroupsRoot
                    {
                        Version = 1,
                        ActiveGroupIndex = 0,
                        Groups = new List<ProcessGroupPersist>
                        {
                            new()
                            {
                                Name = "Default",
                                Metas = metas
                            }
                        }
                    };
                    return ValidateImportedGroups(groupsRoot, out errorMessage);
                }

                errorMessage = "不支持的配置文件格式。";
                return false;
            }
            catch (Exception ex)
            {
                log.Error("读取流程配置文件失败", ex);
                errorMessage = ex.Message;
                return false;
            }
        }

        private static bool ValidateImportedGroups(ProcessGroupsRoot groupsRoot, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (groupsRoot.Groups == null || groupsRoot.Groups.Count == 0)
            {
                errorMessage = "配置文件中没有流程组。";
                return false;
            }

            return true;
        }

        private void ApplyImportedGroups(ProcessGroupsRoot importedGroups)
        {
            var importedProcessGroups = new List<ProcessGroup>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var groupPersist in importedGroups.Groups)
            {
                if (groupPersist == null)
                    continue;

                var group = new ProcessGroup
                {
                    Name = GetUniqueGroupName(groupPersist.Name, usedNames)
                };

                foreach (var metaPersist in groupPersist.Metas ?? new List<ProcessMetaPersist>())
                {
                    if (metaPersist == null)
                        continue;

                    group.ProcessMetas.Add(DeserializeProcessMeta(metaPersist));
                }

                importedProcessGroups.Add(group);
            }

            if (importedProcessGroups.Count == 0)
            {
                importedProcessGroups.Add(new ProcessGroup { Name = "Default" });
            }

            UnhookProcessMetasEvents();
            SelectedProcessMeta = null;
            ProcessGroups.Clear();

            foreach (var group in importedProcessGroups)
            {
                ProcessGroups.Add(group);
            }

            if (ProcessGroups.Count == 0)
                _ActiveGroupIndex = 0;
            else
                _ActiveGroupIndex = Math.Max(0, Math.Min(importedGroups.ActiveGroupIndex, ProcessGroups.Count - 1));

            OnPropertyChanged(nameof(ProcessGroups));
            OnPropertyChanged(nameof(ActiveGroupIndex));
            OnPropertyChanged(nameof(ActiveGroup));
            OnPropertyChanged(nameof(ProcessMetas));
            HookProcessMetasEvents();
            ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
            SavePersistedGroups();
            CommandManager.InvalidateRequerySuggested();
        }

        private static string GetUniqueGroupName(string name, HashSet<string> usedNames)
        {
            string baseName = string.IsNullOrWhiteSpace(name) ? "Default" : name.Trim();
            string uniqueName = baseName;
            int counter = 1;

            while (!usedNames.Add(uniqueName))
            {
                uniqueName = $"{baseName}_{counter++}";
            }

            return uniqueName;
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
