#pragma warning disable CA1822,CA1852,CS8601,CS8603,CS8604,CS8621,CS8625,CS8714
using ColorVision.Common.MVVM;
using ColorVision.Engine.Batch.PreProcess;
using ColorVision.UI;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Properties = ColorVision.Engine.Properties;

namespace ColorVision.Engine.Batch
{
    internal class PreProcessPersist
    {
        public string? ProcessTypeFullName { get; set; }
        public string? ConfigJson { get; set; }
        public string? ActionName { get; set; }
        public bool? IsEnabled { get; set; }
        public string? TemplateNames { get; set; }
    }

    internal class LegacyActionFields
    {
        public string? ActionName { get; set; }
        public bool? IsEnabled { get; set; }
        public string? TemplateNames { get; set; }
    }

    public class PreProcessTypeOption
    {
        public Type ProcessType { get; }
        public PreProcessMetadata Metadata { get; }
        public string DisplayName => Metadata.DisplayName;
        public string Description => Metadata.Description;

        public PreProcessTypeOption(Type processType)
        {
            ProcessType = processType;
            Metadata = PreProcessMetadata.FromType(processType);
        }

        public override string ToString() => DisplayName;
    }

    public class PreProcessAction : ViewModelBase
    {
        private static readonly char[] TemplateSeparators = { ',', ';', '，', '；' };

        public IPreProcess Process { get; }
        public PreProcessMetadata Metadata { get; }

        public string ActionName
        {
            get => _ActionName;
            set
            {
                _ActionName = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }
        private string _ActionName;

        public bool IsEnabled
        {
            get => _IsEnabled;
            set
            {
                _IsEnabled = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EnabledText));
            }
        }
        private bool _IsEnabled;

        public string TemplateNames
        {
            get => _TemplateNames;
            set
            {
                _TemplateNames = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TemplateSummary));
            }
        }
        private string _TemplateNames = string.Empty;

        public string DisplayName => string.IsNullOrWhiteSpace(ActionName) ? Metadata.DisplayName : ActionName.Trim();
        public string TemplateSummary => string.IsNullOrWhiteSpace(TemplateNames) ? Properties.Resources.Flow_PreProcess_AllTemplates : TemplateNames;
        public string EnabledText => IsEnabled ? Properties.Resources.Flow_PreProcess_EnabledStatus : Properties.Resources.Flow_PreProcess_DisabledStatus;

        public PreProcessAction(IPreProcess process)
        {
            Process = process;
            Metadata = PreProcessMetadata.FromProcess(process);
            _ActionName = Metadata.DisplayName;
        }

        public bool AppliesToTemplate(string templateName)
        {
            if (string.IsNullOrWhiteSpace(TemplateNames))
            {
                return true;
            }

            return TemplateNames.Split(TemplateSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .Any(t => string.Equals(t, templateName, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class PreProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(PreProcessManager));

        private const string PersistFileName = "PreProcessConfig.json";
        private static string PersistDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static PreProcessManager _instance;
        private static readonly object _locker = new();
        private readonly Dictionary<string, Type> _processTypesByFullName = new(StringComparer.Ordinal);
        private bool _isLoading;

        public static PreProcessManager GetInstance() { lock (_locker) { _instance ??= new PreProcessManager(); return _instance; } }

        public ObservableCollection<PreProcessAction> Processes { get; } = new ObservableCollection<PreProcessAction>();
        public ObservableCollection<PreProcessTypeOption> AvailableProcessTypes { get; } = new ObservableCollection<PreProcessTypeOption>();

        public RelayCommand EditCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand AddProcessCommand { get; set; }
        public RelayCommand RemoveProcessCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PreProcessTypeOption SelectedProcessType { get => _SelectedProcessType; set { _SelectedProcessType = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessTypeOption _SelectedProcessType;

        public PreProcessAction SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessAction _SelectedProcess;

        public string PersistPath => PersistFilePath;
        public int ProcessCount => Processes.Count;
        public int EnabledCount => Processes.Count(p => p.IsEnabled);

        public string LastSaveStatus { get => _LastSaveStatus; private set { _LastSaveStatus = value; OnPropertyChanged(); } }
        private string _LastSaveStatus = Properties.Resources.Flow_PreProcess_NotSaved;

        public PreProcessManager()
        {
            LoadAvailableProcessTypes();
            Processes.CollectionChanged += Processes_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            SaveCommand = new RelayCommand(a => SavePersisted());
            AddProcessCommand = new RelayCommand(a => AddProcess(a), a => a is PreProcessTypeOption || SelectedProcessType != null);
            RemoveProcessCommand = new RelayCommand(a => RemoveSelectedProcess(), a => SelectedProcess != null);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());

            SelectedProcessType = AvailableProcessTypes.FirstOrDefault();
            bool hasPersistedConfig = LoadPersisted();
            if (!hasPersistedConfig)
            {
                InitializeDefaultProcesses();
            }

            SubscribeAllActions();
            NotifyProcessSummaryChanged();
        }

        private void LoadAvailableProcessTypes()
        {
            var options = new List<PreProcessTypeOption>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(IsSelectablePreProcessType))
                    {
                        string? fullName = type.FullName;
                        if (string.IsNullOrWhiteSpace(fullName) || _processTypesByFullName.ContainsKey(fullName))
                        {
                            continue;
                        }

                        _processTypesByFullName[fullName] = type;
                        options.Add(new PreProcessTypeOption(type));
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
            }

            foreach (var option in options.OrderBy(x => x.Metadata.Order).ThenBy(x => x.Metadata.DisplayName))
            {
                AvailableProcessTypes.Add(option);
            }
        }

        private static bool IsSelectablePreProcessType(Type type)
        {
            return typeof(IPreProcess).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) != null
                && type.GetCustomAttributes(typeof(PreProcessAttribute), false).OfType<PreProcessAttribute>().Any();
        }

        private void InitializeDefaultProcesses()
        {
            if (!_processTypesByFullName.TryGetValue(typeof(FolderSizePreProcess).FullName, out Type cacheCleanupType))
            {
                return;
            }

            var process = CreateProcess(cacheCleanupType);
            if (process == null)
            {
                return;
            }

            var action = new PreProcessAction(process) { IsEnabled = false };
            Processes.Add(action);
            SelectedProcess = action;
        }

        private IPreProcess? CreateProcess(Type processType)
        {
            try
            {
                return Activator.CreateInstance(processType) as IPreProcess;
            }
            catch (Exception ex)
            {
                log.Warn($"创建预处理动作失败: {processType.FullName}", ex);
                return null;
            }
        }

        private void Processes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (PreProcessAction action in e.NewItems)
                {
                    SubscribeAction(action);
                }
            }
            if (e.OldItems != null)
            {
                foreach (PreProcessAction action in e.OldItems)
                {
                    UnsubscribeAction(action);
                }
            }

            NotifyProcessSummaryChanged();
            RefreshProcessView();
            if (!_isLoading)
            {
                SavePersisted();
            }
        }

        private void Action_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyProcessSummaryChanged();
            RefreshProcessView();
            if (_isLoading) return;
            SavePersisted();
        }

        private void ProcessConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isLoading) return;
            SavePersisted();
        }

        private void SubscribeAllActions()
        {
            foreach (var action in Processes)
            {
                SubscribeAction(action);
            }
        }

        private void SubscribeAction(PreProcessAction action)
        {
            action.PropertyChanged -= Action_PropertyChanged;
            action.PropertyChanged += Action_PropertyChanged;

            if (action.Process.GetConfig() is INotifyPropertyChanged notifyConfig)
            {
                notifyConfig.PropertyChanged -= ProcessConfig_PropertyChanged;
                notifyConfig.PropertyChanged += ProcessConfig_PropertyChanged;
            }
        }

        private void UnsubscribeAction(PreProcessAction action)
        {
            action.PropertyChanged -= Action_PropertyChanged;
            if (action.Process.GetConfig() is INotifyPropertyChanged notifyConfig)
            {
                notifyConfig.PropertyChanged -= ProcessConfig_PropertyChanged;
            }
        }

        private void NotifyProcessSummaryChanged()
        {
            OnPropertyChanged(nameof(ProcessCount));
            OnPropertyChanged(nameof(EnabledCount));
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshProcessView()
        {
            try
            {
                CollectionViewSource.GetDefaultView(Processes)?.Refresh();
            }
            catch
            {
                // The view may not exist yet while the manager is being constructed.
            }
        }

        public void Edit()
        {
            PreProcessManagerWindow processManagerWindow = new PreProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            processManagerWindow.DataContext = this;
            processManagerWindow.ShowDialog();
        }

        private void AddProcess(object? parameter)
        {
            PreProcessTypeOption? option = parameter as PreProcessTypeOption ?? SelectedProcessType;
            if (option == null)
            {
                return;
            }

            var process = CreateProcess(option.ProcessType);
            if (process == null)
            {
                return;
            }

            var action = new PreProcessAction(process) { IsEnabled = true };
            Processes.Add(action);
            SelectedProcessType = option;
            SelectedProcess = action;
        }

        private void RemoveSelectedProcess()
        {
            if (SelectedProcess == null)
            {
                return;
            }

            int index = Processes.IndexOf(SelectedProcess);
            Processes.Remove(SelectedProcess);
            if (Processes.Count == 0)
            {
                SelectedProcess = null;
                return;
            }

            SelectedProcess = Processes[Math.Min(index, Processes.Count - 1)];
        }

        private bool CanMoveUp()
        {
            return SelectedProcess != null && Processes.IndexOf(SelectedProcess) > 0;
        }

        private void MoveUp()
        {
            if (!CanMoveUp()) return;
            int index = Processes.IndexOf(SelectedProcess);
            Processes.Move(index, index - 1);
        }

        private bool CanMoveDown()
        {
            return SelectedProcess != null && Processes.IndexOf(SelectedProcess) < Processes.Count - 1;
        }

        private void MoveDown()
        {
            if (!CanMoveDown()) return;
            int index = Processes.IndexOf(SelectedProcess);
            Processes.Move(index, index + 1);
        }

        private bool LoadPersisted()
        {
            _isLoading = true;
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                if (!File.Exists(PersistFilePath))
                {
                    LastSaveStatus = Properties.Resources.Flow_PreProcess_ConfigNotFound;
                    return false;
                }

                string json = File.ReadAllText(PersistFilePath);
                var list = JsonConvert.DeserializeObject<List<PreProcessPersist>>(json) ?? new List<PreProcessPersist>();

                Processes.Clear();
                int skippedCount = 0;

                foreach (var item in list)
                {
                    if (string.IsNullOrWhiteSpace(item.ProcessTypeFullName)
                        || !_processTypesByFullName.TryGetValue(item.ProcessTypeFullName, out Type processType))
                    {
                        skippedCount++;
                        continue;
                    }

                    var process = CreateProcess(processType);
                    if (process == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    try
                    {
                        process.SetConfig(item.ConfigJson ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"加载预处理动作 {item.ProcessTypeFullName} 配置失败: {ex.Message}");
                    }

                    var action = CreateAction(process, item);
                    Processes.Add(action);
                }

                SelectedProcess = Processes.FirstOrDefault();
                LastSaveStatus = skippedCount > 0
                    ? string.Format(Properties.Resources.Flow_PreProcess_ConfigLoadedWithSkipped, DateTime.Now.ToString("HH:mm:ss"), skippedCount)
                    : string.Format(Properties.Resources.Flow_PreProcess_ConfigLoaded, DateTime.Now.ToString("HH:mm:ss"));
                return true;
            }
            catch (Exception ex)
            {
                log.Error("加载预处理配置失败", ex);
                LastSaveStatus = Properties.Resources.Flow_PreProcess_ConfigLoadFailed;
                return true;
            }
            finally
            {
                _isLoading = false;
                NotifyProcessSummaryChanged();
                RefreshProcessView();
            }
        }

        private static PreProcessAction CreateAction(IPreProcess process, PreProcessPersist item)
        {
            var legacy = ReadLegacyActionFields(item.ConfigJson);
            var action = new PreProcessAction(process);

            string? actionName = FirstNonWhiteSpace(item.ActionName, legacy.ActionName);
            if (!string.IsNullOrWhiteSpace(actionName))
            {
                action.ActionName = actionName;
            }

            action.IsEnabled = item.IsEnabled ?? legacy.IsEnabled ?? false;
            action.TemplateNames = item.TemplateNames ?? legacy.TemplateNames ?? string.Empty;
            return action;
        }

        private static LegacyActionFields ReadLegacyActionFields(string? configJson)
        {
            var fields = new LegacyActionFields();
            if (string.IsNullOrWhiteSpace(configJson))
            {
                return fields;
            }

            try
            {
                var json = JObject.Parse(configJson);
                fields.ActionName = json.GetValue("ActionName", StringComparison.OrdinalIgnoreCase)?.Value<string>();
                fields.TemplateNames = json.GetValue("TemplateNames", StringComparison.OrdinalIgnoreCase)?.Value<string>();

                var enabledToken = json.GetValue("IsEnabled", StringComparison.OrdinalIgnoreCase);
                if (enabledToken != null && bool.TryParse(enabledToken.ToString(), out bool enabled))
                {
                    fields.IsEnabled = enabled;
                }
            }
            catch
            {
                // Old config migration is best-effort only.
            }

            return fields;
        }

        private static string? FirstNonWhiteSpace(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return null;
        }

        public void SavePersisted()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);

                var list = new List<PreProcessPersist>();
                foreach (var action in Processes)
                {
                    try
                    {
                        var persist = new PreProcessPersist
                        {
                            ProcessTypeFullName = action.Process.GetType().FullName,
                            ConfigJson = JsonConvert.SerializeObject(action.Process.GetConfig()),
                            ActionName = action.ActionName,
                            IsEnabled = action.IsEnabled,
                            TemplateNames = action.TemplateNames
                        };
                        list.Add(persist);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"序列化预处理动作 {action.Process.GetType().Name} 配置失败: {ex.Message}");
                    }
                }

                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
                LastSaveStatus = string.Format(Properties.Resources.Flow_PreProcess_AutoSaved, DateTime.Now.ToString("HH:mm:ss"));
            }
            catch (Exception ex)
            {
                log.Error("保存预处理器配置失败", ex);
                LastSaveStatus = Properties.Resources.Flow_PreProcess_SaveFailedCheckLog;
            }
        }

        public List<IPreProcess> GetEnabledProcesses(string flowName)
        {
            return GetEnabledActions(flowName).Select(a => a.Process).ToList();
        }

        private List<PreProcessAction> GetEnabledActions(string flowName)
        {
            return Processes.Where(a => a.IsEnabled && a.AppliesToTemplate(flowName)).ToList();
        }

        public async Task<bool> ExecuteAsync(string flowName, string serialNumber, ObservableCollection<CVBaseServerNode>? serverNodes = null)
        {
            try
            {
                var matchingActions = GetEnabledActions(flowName);
                if (matchingActions.Count == 0)
                {
                    return true;
                }

                log.Info($"匹配到 {matchingActions.Count} 个已启用的预处理动作 {flowName}");
                var ctx = new IPreProcessContext
                {
                    FlowName = flowName,
                    SerialNumber = serialNumber,
                    CVBaseServerNodes = serverNodes,
                };

                foreach (var action in matchingActions)
                {
                    log.Info($"执行预处理动作 {action.DisplayName}");
                    try
                    {
                        bool success = await action.Process.PreProcess(ctx);
                        if (!success)
                        {
                            log.Warn($"预处理动作 {action.DisplayName} 执行返回失败");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"预处理动作 {action.DisplayName} 执行异常", ex);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行预处理出错", ex);
                return false;
            }
        }

        public static string GetProcessDisplayName(IPreProcess processor)
        {
            return PreProcessMetadata.FromProcess(processor).DisplayName;
        }
    }
}
