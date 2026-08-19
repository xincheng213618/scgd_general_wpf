using ColorVision.Common.MVVM;
using ColorVision.Engine.FlowProcessing.Editor;
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
using System.Text;
using Microsoft.Win32;
using ProjectARVRPro.Process.Blank;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Process
{
    public class ProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ProcessManager));
        private const string PersistFileName = "ProcessMetas.json";
        private const string GroupPersistFileName = "ProcessGroups.json";
        private const string ExportConfigFilter = "ARVR流程配置 (*.arvrprocess.json)|*.arvrprocess.json|JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*";
        private const string LegacyRecipeFilter = "旧版 ARVR Recipe (ARVRRecipe.json)|ARVRRecipe.json|JSON文件 (*.json)|*.json|所有文件 (*.*)|*.*";

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
        private bool _suppressPersistence;
        public static ProcessManager GetInstance() { lock (_locker) { _instance ??= new ProcessManager(); return _instance; } }

        public ObservableCollection<IProcess> Processes { get; } = new ObservableCollection<IProcess>();
        public ObservableCollection<ProcessMeta> ResultParserMetas { get; } = new();

        /// <summary>
        /// 所有流程组
        /// </summary>
        public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new ObservableCollection<ProcessGroup>();

        public RecipeConfig RecipeConfig { get; private set; } = new();

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
        public event EventHandler? ActiveProcessMetasChanged;
        public event EventHandler? RecipeConfigImported;

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;

        public RelayCommand EditCommand { get; set; }

        public ProcessMeta? SelectedProcessMeta
        {
            get => _SelectedProcessMeta;
            set
            {
                if (_SelectedProcessMeta == value)
                    return;

                _SelectedProcessMeta = value;
                IsEditingMetaName = false;
                SelectedMetaNameDraft = value?.Name ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedConfigurationMeta));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private ProcessMeta? _SelectedProcessMeta;

        public ProcessMeta? SelectedResultParserMeta
        {
            get => _SelectedResultParserMeta;
            set
            {
                if (_SelectedResultParserMeta == value)
                    return;

                _SelectedResultParserMeta = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedConfigurationMeta));
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private ProcessMeta? _SelectedResultParserMeta;

        [JsonIgnore]
        public ProcessMeta? SelectedConfigurationMeta => SelectedResultParserMeta ?? SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand DuplicateMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand EditMetaNameCommand { get; set; }
        public RelayCommand SaveMetaNameCommand { get; set; }
        public RelayCommand CancelMetaNameCommand { get; set; }
        public RelayCommand EditMetaTemplateCommand { get; set; }
        public RelayCommand OpenFlowTemplateEditorCommand { get; set; }
        public RelayCommand EditMetaProcessCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }
        public RelayCommand AddResultParserCommand { get; set; }
        public RelayCommand RemoveResultParserCommand { get; set; }
        public RelayCommand UpdateResultParserCommand { get; set; }

        // Group management commands
        public RelayCommand AddGroupCommand { get; set; }
        public RelayCommand RemoveGroupCommand { get; set; }
        public RelayCommand RenameGroupCommand { get; set; }
        public RelayCommand DuplicateGroupCommand { get; set; }
        public RelayCommand ImportConfigCommand { get; set; }
        public RelayCommand ImportLegacyRecipeCommand { get; set; }
        public RelayCommand ExportConfigCommand { get; set; }

        /// <summary>
        /// 新组名称（UI绑定）
        /// </summary>
        public string NewGroupName { get => _NewGroupName; set { _NewGroupName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewGroupName;

        public bool IsEditingMetaName
        {
            get => _IsEditingMetaName;
            private set
            {
                if (_IsEditingMetaName == value)
                    return;

                _IsEditingMetaName = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private bool _IsEditingMetaName;

        public string SelectedMetaNameDraft
        {
            get => _SelectedMetaNameDraft;
            set
            {
                if (_SelectedMetaNameDraft == value)
                    return;

                _SelectedMetaNameDraft = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        private string _SelectedMetaNameDraft = string.Empty;

        public ProcessManager()
        {
            LoadProcesses();
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => ActiveGroup != null);
            DuplicateMetaCommand = new RelayCommand(a => DuplicateMeta(), a => SelectedProcessMeta != null);
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            EditMetaNameCommand = new RelayCommand(
                a => BeginEditMetaName(a as ProcessMeta),
                a => a is ProcessMeta);
            SaveMetaNameCommand = new RelayCommand(
                a => SaveMetaName(),
                a => IsEditingMetaName && SelectedProcessMeta != null && !string.IsNullOrWhiteSpace(SelectedMetaNameDraft));
            CancelMetaNameCommand = new RelayCommand(
                a => CancelMetaNameEdit(),
                a => IsEditingMetaName);
            EditMetaTemplateCommand = new RelayCommand(
                a => UpdateMeta(a as ProcessMeta, ProcessMetaEditTarget.Template),
                a => a is ProcessMeta);
            OpenFlowTemplateEditorCommand = new RelayCommand(
                a => OpenFlowTemplateEditor(a as ProcessMeta),
                a => a is ProcessMeta);
            EditMetaProcessCommand = new RelayCommand(
                a => UpdateMeta(a as ProcessMeta, ProcessMetaEditTarget.Process),
                a => a is ProcessMeta);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            AddResultParserCommand = new RelayCommand(a => AddResultParser());
            RemoveResultParserCommand = new RelayCommand(a => RemoveResultParser(), a => SelectedResultParserMeta != null);
            UpdateResultParserCommand = new RelayCommand(a => UpdateResultParser(), a => SelectedResultParserMeta != null);

            AddGroupCommand = new RelayCommand(a => AddGroup(), a => !string.IsNullOrWhiteSpace(NewGroupName));
            RemoveGroupCommand = new RelayCommand(a => RemoveGroup(), a => ProcessGroups.Count > 1);
            RenameGroupCommand = new RelayCommand(a => RenameGroup(), a => ActiveGroup != null && !string.IsNullOrWhiteSpace(NewGroupName));
            DuplicateGroupCommand = new RelayCommand(a => DuplicateGroup(), a => ActiveGroup != null);
            ImportConfigCommand = new RelayCommand(a => ImportConfig());
            ImportLegacyRecipeCommand = new RelayCommand(a => ImportLegacyRecipe());
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
                newGroup.ProcessMetas.Add(CloneProcessMeta(meta, meta.Name));
            }
            ProcessGroups.Add(newGroup);
            ActiveGroupIndex = ProcessGroups.Count - 1;
            SavePersistedGroups();
        }

        private void ImportLegacyRecipe()
        {
            string legacyDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Config");
            string legacyFilePath = Path.Combine(legacyDirectory, "ARVRRecipe.json");
            var dialog = new OpenFileDialog
            {
                Title = "导入旧版 Recipe 清单",
                Filter = LegacyRecipeFilter,
                DefaultExt = "json",
                InitialDirectory = Directory.Exists(legacyDirectory) ? legacyDirectory : string.Empty,
                FileName = File.Exists(legacyFilePath) ? Path.GetFileName(legacyFilePath) : string.Empty
            };

            if (dialog.ShowDialog(Application.Current.GetActiveWindow()) != true)
                return;

            if (!LegacyRecipeImporter.TryReadFile(dialog.FileName, out var importResult, out string errorMessage))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"导入旧版 Recipe 失败:\n{errorMessage}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string confirmation = $"识别到 {importResult.SourceCount} 项旧版 Recipe，其中 {importResult.SharedConfigs.Count} 项将应用到匹配的流程和解析实例";
            if (importResult.LuminanceConfigs.Count > 0)
                confirmation += $"，{importResult.LuminanceConfigs.Count} 项 RGB/W25 Recipe 将按 Key 应用到当前亮色度流程";
            confirmation += "。\n导入只覆盖对应 Recipe，不会修改流程组结构。是否继续？";

            if (MessageBox.Show(Application.Current.GetActiveWindow(), confirmation, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            LegacyRecipeSnapshot snapshot;
            try
            {
                snapshot = CaptureLegacyRecipeSnapshot();
            }
            catch (Exception ex)
            {
                log.Error("准备导入旧版 Recipe 失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"准备导入旧版 Recipe 失败:\n{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            (int updatedProcessRecipes, int updatedLuminanceProcesses, List<string> unmatchedLuminanceKeys) importSummary;
            try
            {
                _suppressPersistence = true;
                try
                {
                    importSummary = ApplyLegacyRecipe(importResult);
                }
                finally
                {
                    _suppressPersistence = false;
                }

                if (!SavePersistedGroups())
                    throw new IOException("保存 ProcessGroups.json 失败。");
            }
            catch (Exception ex)
            {
                log.Error("应用旧版 Recipe 失败", ex);
                string rollbackMessage;
                try
                {
                    RestoreLegacyRecipeSnapshot(snapshot);
                    rollbackMessage = "\n已恢复导入前的配置。";
                }
                catch (Exception rollbackException)
                {
                    log.Error("恢复旧版 Recipe 导入前配置失败", rollbackException);
                    rollbackMessage = $"\n恢复导入前配置也失败，请勿关闭程序并立即导出当前配置：{rollbackException.Message}";
                }

                MessageBox.Show(Application.Current.GetActiveWindow(), $"应用旧版 Recipe 失败:\n{ex.Message}{rollbackMessage}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            OnPropertyChanged(nameof(RecipeConfig));
            RecipeConfigImported?.Invoke(this, EventArgs.Empty);

            string message = $"旧版 Recipe 已导入：更新 {importSummary.updatedProcessRecipes} 个流程/解析 Recipe";
            if (importResult.LuminanceConfigs.Count > 0)
                message += $"、{importSummary.updatedLuminanceProcesses} 个亮色度流程项";
            message += "。";

            if (importSummary.unmatchedLuminanceKeys.Count > 0)
                message += $"\n未找到以下亮色度 Key，对应旧配置未应用：{string.Join(", ", importSummary.unmatchedLuminanceKeys)}。";

            if (importResult.UnsupportedTypeNames.Count > 0)
            {
                string unsupportedTypes = string.Join(", ", importResult.UnsupportedTypeNames
                    .Select(typeName => typeName.Split('.').Last())
                    .Distinct(StringComparer.Ordinal));
                message += $"\n当前版本不支持以下旧类型，已跳过：{unsupportedTypes}。";
            }

            MessageBox.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private sealed record LegacyRecipeSnapshot(
            Dictionary<Type, IRecipeConfig>? SharedConfigs,
            Dictionary<ProcessMeta, string> ProcessConfigs);

        private LegacyRecipeSnapshot CaptureLegacyRecipeSnapshot()
        {
            Dictionary<Type, IRecipeConfig>? sharedConfigs = RecipeConfig.Configs == null
                ? null
                : new Dictionary<Type, IRecipeConfig>(RecipeConfig.Configs);
            Dictionary<ProcessMeta, string> processConfigs = ProcessGroups
                .SelectMany(group => group.ProcessMetas)
                .Concat(ResultParserMetas)
                .Distinct()
                .ToDictionary(meta => meta, GetProcessConfigJson);
            return new LegacyRecipeSnapshot(sharedConfigs, processConfigs);
        }

        private void RestoreLegacyRecipeSnapshot(LegacyRecipeSnapshot snapshot)
        {
            RecipeConfig.Configs = snapshot.SharedConfigs == null
                ? null!
                : new Dictionary<Type, IRecipeConfig>(snapshot.SharedConfigs);

            UnhookProcessMetasEvents();
            UnhookResultParserEvents();
            try
            {
                foreach ((ProcessMeta meta, string configJson) in snapshot.ProcessConfigs)
                {
                    if (meta.Process != null && !string.IsNullOrWhiteSpace(configJson))
                        meta.Process.SetProcessConfig(configJson);
                    meta.ConfigJson = configJson;
                }
            }
            finally
            {
                HookProcessMetasEvents();
                HookResultParserEvents();
            }

            OnPropertyChanged(nameof(RecipeConfig));
            RecipeConfigImported?.Invoke(this, EventArgs.Empty);
        }

        internal (int UpdatedProcessRecipes, int UpdatedLuminanceProcesses, List<string> UnmatchedLuminanceKeys) ApplyLegacyRecipe(LegacyRecipeImportResult importResult)
        {
            RecipeConfig.Configs ??= new Dictionary<Type, IRecipeConfig>();
            foreach (var (type, config) in importResult.SharedConfigs)
                RecipeConfig.Configs[type] = config;

            int updatedProcessRecipes = 0;
            int updatedLuminanceProcesses = 0;
            var appliedLuminanceKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            UnhookProcessMetasEvents();
            UnhookResultParserEvents();
            try
            {
                IEnumerable<ProcessMeta> metas = ProcessGroups
                    .SelectMany(group => group.ProcessMetas)
                    .Concat(ResultParserMetas)
                    .Distinct();

                foreach (ProcessMeta meta in metas)
                {
                    if (ApplyImportedRecipe(meta, importResult.SharedConfigs))
                        updatedProcessRecipes++;

                    if (meta.Process is not LuminanceChromaticityProcess process)
                        continue;

                    string outputKey = process.Config.GetOutputKey();
                    if (!importResult.LuminanceConfigs.TryGetValue(outputKey, out var importedConfig))
                        continue;

                    process.Config.RecipeConfig = LegacyRecipeImporter.CloneLuminanceConfig(importedConfig);
                    meta.ConfigJson = JsonConvert.SerializeObject(process.Config);
                    appliedLuminanceKeys.Add(outputKey);
                    updatedLuminanceProcesses++;
                }
            }
            finally
            {
                HookProcessMetasEvents();
                HookResultParserEvents();
            }

            List<string> unmatchedLuminanceKeys = importResult.LuminanceConfigs.Keys
                .Where(key => !appliedLuminanceKeys.Contains(key))
                .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (updatedProcessRecipes, updatedLuminanceProcesses, unmatchedLuminanceKeys);
        }

        internal static bool ApplyImportedRecipe(ProcessMeta meta, IReadOnlyDictionary<Type, IRecipeConfig> importedConfigs)
        {
            IRecipeConfig? targetRecipe = meta.Process?.GetRecipeConfig();
            if (targetRecipe == null || !importedConfigs.TryGetValue(targetRecipe.GetType(), out IRecipeConfig? importedRecipe))
                return false;

            JsonConvert.PopulateObject(
                JsonConvert.SerializeObject(importedRecipe),
                targetRecipe,
                new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    NullValueHandling = NullValueHandling.Ignore
                });
            meta.ConfigJson = GetProcessConfigJson(meta);
            return true;
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
                    Version = 3,
                    ExportedAt = DateTime.Now,
                    ProcessGroups = CreateProcessGroupsRoot()
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
            int parserCount = importedGroups.ResultParsers?.Count ?? 0;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"导入后将替换当前流程组和解析映射，共 {groupCount} 个组、{parserCount} 条解析映射。是否继续？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                ApplyImportedGroups(importedGroups, importedRecipe);

                string message = $"流程配置已导入，共 {ProcessGroups.Count} 个组、{ResultParserMetas.Count} 条解析映射。";
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

        private void HookResultParserEvents()
        {
            ResultParserMetas.CollectionChanged += ResultParserMetas_CollectionChanged;
            foreach (var meta in ResultParserMetas)
            {
                meta.PropertyChanged += Meta_PropertyChanged;
            }
        }

        private void UnhookResultParserEvents()
        {
            ResultParserMetas.CollectionChanged -= ResultParserMetas_CollectionChanged;
            foreach (var meta in ResultParserMetas)
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
            ProcessMetaCollectionChanged(e);
            ActiveProcessMetasChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ResultParserMetas_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ProcessMetaCollectionChanged(e);
        }

        private void ProcessMetaCollectionChanged(NotifyCollectionChangedEventArgs e)
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

            if (sender is ProcessMeta meta
                && ProcessMetas.Contains(meta)
                && (string.IsNullOrEmpty(e.PropertyName)
                    || e.PropertyName == nameof(ProcessMeta.IsEnabled)
                    || e.PropertyName == nameof(ProcessMeta.Name)))
            {
                ActiveProcessMetasChanged?.Invoke(this, EventArgs.Empty);
            }
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

            var dialog = new ProcessMetaEditWindow(
                templateModels,
                Processes,
                "新增流程项")
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true) return;

            if (dialog.SelectedTemplate == null) return;

            if (HasDuplicateMetaName(dialog.MetaName))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }

            IProcess configuredProcess = CreateConfiguredProcess(dialog.SelectedProcess, out string configJson);
            var newMeta = new ProcessMeta
            {
                Name = dialog.MetaName,
                FlowTemplate = dialog.SelectedTemplate.Key,
                Process = configuredProcess,
                ConfigJson = configJson,
                IsEnabled = dialog.IsMetaEnabled
            };

            ProcessMetas.Add(newMeta);
            SelectedProcessMeta = newMeta;
        }

        private void DuplicateMeta()
        {
            ProcessMeta? source = SelectedProcessMeta;
            if (source == null)
                return;

            string copyName = GetUniqueMetaCopyName(ProcessMetas, source.Name);
            ProcessMeta copy = CloneProcessMeta(source, copyName);
            int sourceIndex = ProcessMetas.IndexOf(source);
            ProcessMetas.Insert(sourceIndex + 1, copy);
            SelectedProcessMeta = copy;
        }

        private static ProcessMeta CloneProcessMeta(ProcessMeta source, string name)
        {
            string configJson = GetProcessConfigJson(source);
            IProcess? process = source.Process?.CreateInstance();
            if (process != null && !string.IsNullOrEmpty(configJson))
                process.SetProcessConfig(configJson);

            return new ProcessMeta
            {
                Name = name,
                FlowTemplate = source.FlowTemplate,
                Process = process,
                IsEnabled = source.IsEnabled,
                ConfigJson = configJson,
                PictureSwitchConfig = source.PictureSwitchConfig.Clone()
            };
        }

        internal static string GetUniqueMetaCopyName(IEnumerable<ProcessMeta> processMetas, string sourceName)
        {
            string normalizedSourceName = string.IsNullOrWhiteSpace(sourceName) ? "Process" : sourceName.Trim();
            string baseName = $"{normalizedSourceName}_Copy";
            string name = baseName;
            int counter = 1;

            while (processMetas.Any(meta => meta.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                name = $"{baseName}_{counter++}";

            return name;
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

        private void BeginEditMetaName(ProcessMeta? meta)
        {
            if (meta == null)
                return;

            SelectedProcessMeta = meta;
            SelectedMetaNameDraft = meta.Name;
            IsEditingMetaName = true;
        }

        private void SaveMetaName()
        {
            if (SelectedProcessMeta == null)
                return;

            string name = SelectedMetaNameDraft.Trim();
            if (string.IsNullOrEmpty(name))
                return;

            if (HasDuplicateMetaName(name, SelectedProcessMeta))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }

            SelectedProcessMeta.Name = name;
            SelectedMetaNameDraft = name;
            IsEditingMetaName = false;
        }

        private void CancelMetaNameEdit()
        {
            SelectedMetaNameDraft = SelectedProcessMeta?.Name ?? string.Empty;
            IsEditingMetaName = false;
        }

        private void UpdateMeta(ProcessMeta? meta, ProcessMetaEditTarget editTarget)
        {
            if (meta == null)
                return;

            SelectedProcessMeta = meta;
            CancelMetaNameEdit();

            var dialog = new ProcessMetaEditWindow(
                templateModels,
                Processes,
                editTarget switch
                {
                    ProcessMetaEditTarget.Template => $"修改流程模板 - {meta.Name}",
                    ProcessMetaEditTarget.Process => $"修改处理类型 - {meta.Name}",
                    _ => $"编辑流程项 - {meta.Name}"
                },
                meta.Name,
                meta.FlowTemplate,
                meta.Process,
                meta.IsEnabled,
                isEdit: true,
                editTarget: editTarget)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true) return;

            if (dialog.SelectedTemplate == null) return;

            if (HasDuplicateMetaName(dialog.MetaName, meta))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }

            IProcess newProcessInstance = CreateConfiguredProcess(dialog.SelectedProcess, out string configJson);
            meta.Name = dialog.MetaName;
            meta.FlowTemplate = dialog.SelectedTemplate.Key;
            meta.IsEnabled = dialog.IsMetaEnabled;
            meta.ConfigJson = configJson;
            meta.Process = newProcessInstance;
        }

        private void OpenFlowTemplateEditor(ProcessMeta? meta)
        {
            if (meta == null)
                return;

            TemplateModel<FlowParam>? template = templateModels.FirstOrDefault(item =>
                string.Equals(item.Key, meta.FlowTemplate, StringComparison.OrdinalIgnoreCase));
            if (template == null)
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    $"未找到流程模板 \"{meta.FlowTemplate}\"。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            new FlowEngineToolWindow(template.Value)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void AddResultParser()
        {
            var dialog = new ProcessMetaEditWindow(
                templateModels,
                Processes,
                "新增解析映射",
                showMetaFields: false)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null)
                return;

            if (dialog.SelectedProcess == null)
                return;

            if (HasDuplicateResultParser(dialog.SelectedTemplate.Key))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "该流程模板已经配置了解析映射", "ColorVision");
                return;
            }

            IProcess configuredProcess = CreateConfiguredProcess(dialog.SelectedProcess, out string configJson);
            var meta = new ProcessMeta
            {
                Name = dialog.SelectedTemplate.Key,
                FlowTemplate = dialog.SelectedTemplate.Key,
                Process = configuredProcess,
                ConfigJson = configJson
            };
            ResultParserMetas.Add(meta);
            SelectedResultParserMeta = meta;
        }

        private void RemoveResultParser()
        {
            if (SelectedResultParserMeta == null)
                return;

            ResultParserMetas.Remove(SelectedResultParserMeta);
            SelectedResultParserMeta = null;
        }

        private void UpdateResultParser()
        {
            if (SelectedResultParserMeta == null)
                return;

            var selectedMeta = SelectedResultParserMeta;
            var dialog = new ProcessMetaEditWindow(
                templateModels,
                Processes,
                $"编辑解析映射 - {selectedMeta.FlowTemplate}",
                flowTemplate: selectedMeta.FlowTemplate,
                process: selectedMeta.Process,
                showMetaFields: false,
                isEdit: true)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (dialog.ShowDialog() != true || dialog.SelectedTemplate == null)
                return;

            if (dialog.SelectedProcess == null)
            {
                ResultParserMetas.Remove(selectedMeta);
                SelectedResultParserMeta = null;
                return;
            }

            if (HasDuplicateResultParser(dialog.SelectedTemplate.Key, selectedMeta))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "该流程模板已经配置了解析映射", "ColorVision");
                return;
            }

            IProcess newProcessInstance = CreateConfiguredProcess(dialog.SelectedProcess, out string configJson);

            selectedMeta.Name = dialog.SelectedTemplate.Key;
            selectedMeta.FlowTemplate = dialog.SelectedTemplate.Key;
            selectedMeta.ConfigJson = configJson;
            selectedMeta.Process = newProcessInstance;
        }

        private bool HasDuplicateResultParser(string flowTemplate, ProcessMeta? ignoredMeta = null)
        {
            return ResultParserMetas.Any(meta =>
                !ReferenceEquals(meta, ignoredMeta)
                && string.Equals(meta.FlowTemplate, flowTemplate, StringComparison.OrdinalIgnoreCase));
        }

        public ProcessMeta? FindProcessMetaForTemplate(string flowTemplate)
        {
            return ResultProcessResolver.FindMapping(flowTemplate, ProcessMetas, ResultParserMetas);
        }

        public IEnumerable<ProcessMeta> GetResultProcessMappings()
        {
            return ProcessMetas.Concat(ResultParserMetas);
        }

        public IProcess CreateBlankProcess()
        {
            IProcess? template = Processes.FirstOrDefault(process => process is BlankProcess);
            return template?.CreateInstance() ?? new BlankProcess();
        }

        private IProcess CreateProcessInstanceOrBlank(IProcess? process)
        {
            return process?.CreateInstance() ?? CreateBlankProcess();
        }

        private IProcess CreateConfiguredProcess(IProcess? source, out string configJson)
        {
            IProcess process = CreateProcessInstanceOrBlank(source);
            object? config = source?.GetProcessConfig();
            configJson = config == null ? string.Empty : JsonConvert.SerializeObject(config);
            if (!string.IsNullOrEmpty(configJson))
            {
                process.SetProcessConfig(configJson);
            }

            return process;
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
            MoveMetaToIndex(selectedMeta, index - 1);
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
            MoveMetaToIndex(selectedMeta, index + 1);
        }

        internal bool MoveMetaToIndex(ProcessMeta meta, int destinationIndex)
        {
            int sourceIndex = ProcessMetas.IndexOf(meta);
            if (sourceIndex < 0 || ProcessMetas.Count == 0)
                return false;

            destinationIndex = Math.Clamp(destinationIndex, 0, ProcessMetas.Count - 1);
            if (sourceIndex == destinationIndex)
                return false;

            ProcessMetas.Move(sourceIndex, destinationIndex);
            SelectedProcessMeta = meta;
            CommandManager.InvalidateRequerySuggested();
            return true;
        }

        #region Persistence

        private void LoadPersistedGroups()
        {
            bool saveAfterLoad = false;
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                // Try new format first
                if (File.Exists(GroupPersistFilePath))
                {
                    saveAfterLoad = LoadFromGroupsFile();
                }
                else if (File.Exists(PersistFilePath))
                {
                    MigrateFromOldFormat();
                    saveAfterLoad = true;
                }
            }
            catch (Exception ex)
            {
                log.Error("加载ProcessGroups失败", ex);
                ProcessGroups.Clear();
                RecipeConfig = new RecipeConfig();
            }

            if (ProcessGroups.Count == 0)
            {
                ProcessGroups.Add(new ProcessGroup { Name = "Default" });
                _ActiveGroupIndex = 0;
            }

            OnPropertyChanged(nameof(ActiveGroupIndex));
            OnPropertyChanged(nameof(ActiveGroup));
            OnPropertyChanged(nameof(ProcessMetas));
            HookProcessMetasEvents();
            HookResultParserEvents();

            if (saveAfterLoad)
                SavePersistedGroups();
        }

        private bool LoadFromGroupsFile()
        {
            string json = File.ReadAllText(GroupPersistFilePath);
            var root = JsonConvert.DeserializeObject<ProcessGroupsRoot>(json, ExportJsonSerializerSettings);
            if (root == null || root.Groups == null || root.Groups.Count == 0)
            {
                ProcessGroups.Add(new ProcessGroup { Name = "Default" });
                _ActiveGroupIndex = 0;
                return false;
            }

            RecipeConfig = root.RecipeConfig ?? new RecipeConfig();

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

            foreach (var item in root.ResultParsers ?? new List<ProcessMetaPersist>())
            {
                ResultParserMetas.Add(DeserializeProcessMeta(item));
            }

            bool migrated = root.Version < 3;
            if (migrated)
                SeedResultParsersFromGroups();

            _ActiveGroupIndex = Math.Max(0, Math.Min(root.ActiveGroupIndex, ProcessGroups.Count - 1));
            return migrated;
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
            SeedResultParsersFromGroups();
        }

        private void SeedResultParsersFromGroups()
        {
            var mappedTemplates = new HashSet<string>(
                ResultParserMetas.Select(meta => meta.FlowTemplate),
                StringComparer.OrdinalIgnoreCase);

            foreach (ProcessMeta source in ProcessGroups.SelectMany(group => group.ProcessMetas))
            {
                if (string.IsNullOrWhiteSpace(source.FlowTemplate)
                    || ProcessTypeCatalog.IsBlankProcess(source.Process)
                    || !mappedTemplates.Add(source.FlowTemplate))
                    continue;

                IProcess? process = source.Process?.CreateInstance();
                string configJson = GetProcessConfigJson(source);
                if (process != null && !string.IsNullOrWhiteSpace(configJson))
                    process.SetProcessConfig(configJson);

                ResultParserMetas.Add(new ProcessMeta
                {
                    Name = source.FlowTemplate,
                    FlowTemplate = source.FlowTemplate,
                    Process = process,
                    ConfigJson = configJson
                });
            }
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

            if (templateProc == null)
            {
                templateProc = FindUniqueProcessTemplateByClassName(item.ProcessTypeFullName);
                if (templateProc != null)
                    log.Info($"流程类型命名空间已更新: {item.ProcessTypeFullName} -> {templateProc.GetType().FullName}");
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

        private IProcess? FindUniqueProcessTemplateByClassName(string processTypeFullName)
        {
            if (string.IsNullOrWhiteSpace(processTypeFullName))
                return null;

            int separatorIndex = processTypeFullName.LastIndexOf('.');
            string className = processTypeFullName[(separatorIndex + 1)..];
            IProcess[] matches = Processes
                .Where(process => string.Equals(process.GetType().Name, className, StringComparison.Ordinal))
                .Take(2)
                .ToArray();

            return matches.Length == 1 ? matches[0] : null;
        }

        private bool SavePersistedGroups()
        {
            if (_suppressPersistence)
                return true;

            return SavePersistedGroups(CreateProcessGroupsRoot());
        }

        private static bool SavePersistedGroups(ProcessGroupsRoot root)
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                string json = JsonConvert.SerializeObject(root, ExportJsonSerializerSettings);
                WriteTextAtomically(GroupPersistFilePath, json);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("保存ProcessGroups失败", ex);
                return false;
            }
        }

        public void SaveProcessGroups()
        {
            SavePersistedGroups();
        }

        internal bool TrySaveProcessGroups()
        {
            return SavePersistedGroups();
        }

        internal static void WriteTextAtomically(string filePath, string contents)
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("保存路径必须包含目录。", nameof(filePath));

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(filePath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                using (var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 4096, leaveOpen: true))
                {
                    writer.Write(contents);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }

                if (File.Exists(filePath))
                    File.Replace(temporaryPath, filePath, filePath + ".bak", ignoreMetadataErrors: true);
                else
                    File.Move(temporaryPath, filePath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private ProcessGroupsRoot CreateProcessGroupsRoot()
        {
            return CreateProcessGroupsRoot(RecipeConfig, ProcessGroups, ResultParserMetas, _ActiveGroupIndex);
        }

        private static ProcessGroupsRoot CreateProcessGroupsRoot(
            RecipeConfig recipeConfig,
            IEnumerable<ProcessGroup> processGroups,
            IEnumerable<ProcessMeta> resultParsers,
            int activeGroupIndex)
        {
            return new ProcessGroupsRoot
            {
                Version = 3,
                ActiveGroupIndex = activeGroupIndex,
                RecipeConfig = recipeConfig,
                Groups = processGroups.Select(g => new ProcessGroupPersist
                {
                    Name = g.Name,
                    Metas = g.ProcessMetas.Select(CreateProcessMetaPersist).ToList()
                }).ToList(),
                ResultParsers = resultParsers.Select(CreateProcessMetaPersist).ToList()
            };
        }

        private static ProcessMetaPersist CreateProcessMetaPersist(ProcessMeta meta)
        {
            return new ProcessMetaPersist
            {
                Name = meta.Name,
                FlowTemplate = meta.FlowTemplate,
                ProcessTypeFullName = meta.Process?.GetType().FullName ?? string.Empty,
                IsEnabled = meta.IsEnabled,
                ConfigJson = GetProcessConfigJson(meta),
                PictureSwitchConfig = meta.PictureSwitchConfig.Clone()
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
                    recipeConfig = groupsRoot.RecipeConfig;

                    return ValidateImportedGroups(groupsRoot, out errorMessage);
                }

                if (token is JObject groupsObject && groupsObject[nameof(ProcessGroupsRoot.Groups)] != null)
                {
                    groupsRoot = groupsObject.ToObject<ProcessGroupsRoot>(JsonSerializer.Create(ExportJsonSerializerSettings)) ?? new ProcessGroupsRoot();
                    recipeConfig = groupsRoot.RecipeConfig;
                    return ValidateImportedGroups(groupsRoot, out errorMessage);
                }

                if (token is JArray legacyMetas)
                {
                    var metas = legacyMetas.ToObject<List<ProcessMetaPersist>>() ?? new List<ProcessMetaPersist>();
                    groupsRoot = new ProcessGroupsRoot
                    {
                        Version = 2,
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

        internal void ApplyImportedGroups(ProcessGroupsRoot importedGroups, RecipeConfig? importedRecipe)
        {
            var importedProcessGroups = new List<ProcessGroup>();
            var importedResultParsers = new List<ProcessMeta>();
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

            foreach (var metaPersist in importedGroups.ResultParsers ?? new List<ProcessMetaPersist>())
            {
                if (metaPersist != null)
                    importedResultParsers.Add(DeserializeProcessMeta(metaPersist));
            }

            if (importedGroups.Version < 3)
            {
                var mappedTemplates = new HashSet<string>(
                    importedResultParsers.Select(meta => meta.FlowTemplate),
                    StringComparer.OrdinalIgnoreCase);

                foreach (ProcessMeta source in importedProcessGroups.SelectMany(group => group.ProcessMetas))
                {
                    if (string.IsNullOrWhiteSpace(source.FlowTemplate)
                        || ProcessTypeCatalog.IsBlankProcess(source.Process)
                        || !mappedTemplates.Add(source.FlowTemplate))
                        continue;

                    string configJson = GetProcessConfigJson(source);
                    IProcess? process = source.Process?.CreateInstance();
                    if (process != null && !string.IsNullOrWhiteSpace(configJson))
                        process.SetProcessConfig(configJson);

                    importedResultParsers.Add(new ProcessMeta
                    {
                        Name = source.FlowTemplate,
                        FlowTemplate = source.FlowTemplate,
                        Process = process,
                        ConfigJson = configJson
                    });
                }
            }

            if (importedProcessGroups.Count == 0)
            {
                importedProcessGroups.Add(new ProcessGroup { Name = "Default" });
            }

            int importedActiveGroupIndex = importedProcessGroups.Count == 0
                ? 0
                : Math.Max(0, Math.Min(importedGroups.ActiveGroupIndex, importedProcessGroups.Count - 1));
            RecipeConfig candidateRecipeConfig = importedRecipe ?? RecipeConfig;
            ProcessGroupsRoot candidateRoot = CreateProcessGroupsRoot(
                candidateRecipeConfig,
                importedProcessGroups,
                importedResultParsers,
                importedActiveGroupIndex);
            if (!SavePersistedGroups(candidateRoot))
                throw new IOException("保存导入的 ProcessGroups.json 失败，当前配置未被替换。");

            UnhookProcessMetasEvents();
            UnhookResultParserEvents();
            SelectedProcessMeta = null;
            SelectedResultParserMeta = null;
            ProcessGroups.Clear();
            ResultParserMetas.Clear();

            foreach (var group in importedProcessGroups)
            {
                ProcessGroups.Add(group);
            }

            foreach (ProcessMeta meta in importedResultParsers)
            {
                ResultParserMetas.Add(meta);
            }

            _ActiveGroupIndex = importedActiveGroupIndex;
            RecipeConfig = candidateRecipeConfig;

            OnPropertyChanged(nameof(RecipeConfig));
            OnPropertyChanged(nameof(ProcessGroups));
            OnPropertyChanged(nameof(ActiveGroupIndex));
            OnPropertyChanged(nameof(ActiveGroup));
            OnPropertyChanged(nameof(ProcessMetas));
            HookProcessMetasEvents();
            HookResultParserEvents();
            ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
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
            foreach (var item in ProcessMetas.Where(meta => meta.IsEnabled))
            {
                HandyControl.Controls.StepBarItem stepBarItem = new HandyControl.Controls.StepBarItem() { Content = item.Name };
                stepBar.Items.Add(stepBarItem);
            }
        }

        internal static int GetEnabledStepIndex(IReadOnlyList<ProcessMeta> processMetas, int processIndex)
        {
            if (processIndex < 0 || processIndex >= processMetas.Count || !processMetas[processIndex].IsEnabled)
                return -1;

            int enabledStepIndex = 0;
            for (int i = 0; i < processIndex; i++)
            {
                if (processMetas[i].IsEnabled)
                    enabledStepIndex++;
            }

            return enabledStepIndex;
        }

    }
}
