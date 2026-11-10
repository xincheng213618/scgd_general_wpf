#pragma warning disable CA1822,CA1852,CS8601,CS8603,CS8604,CS8621,CS8625,CS8714
using ColorVision.Common.MVVM;
using ColorVision.UI;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
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

namespace ColorVision.Engine.FlowProcessing.PreProcess
{
    internal class PreProcessPersist
    {
        public string? ProcessTypeFullName { get; set; }
        public string? ConfigJson { get; set; }
        public string? ActionName { get; set; }
        public bool? IsEnabled { get; set; }
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
        public IPreProcessor Process { get; }
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

        public string DisplayName => string.IsNullOrWhiteSpace(ActionName) ? Metadata.DisplayName : ActionName.Trim();
        internal string? UnreadableConfigJson { get; init; }
        internal bool HasInvalidConfiguration { get; init; }
        public string EnabledText => HasInvalidConfiguration
            ? EngineLocalization.Get("Flow_PreProcess_InvalidActionConfig")
            : IsEnabled ? Properties.Resources.Flow_PreProcess_EnabledStatus : Properties.Resources.Flow_PreProcess_DisabledStatus;

        public PreProcessAction(IPreProcessor process)
        {
            Process = process;
            Metadata = PreProcessMetadata.FromProcess(process);
            _ActionName = Metadata.DisplayName;
        }
    }

    public class PreProcessManager : ViewModelBase
    {
        private enum PersistedLoadOutcome { Loaded, Missing, Failed }

        private static readonly ILog log = LogManager.GetLogger(nameof(PreProcessManager));

        private const string PersistFileName = "PreProcessConfig.json";
        private static string PersistDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        internal static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static PreProcessManager _instance;
        private static readonly object _locker = new();
        private readonly Dictionary<string, Type> _processTypesByFullName = new(StringComparer.Ordinal);
        private readonly string _persistFilePath;
        private bool _isLoading;
        private bool _isConfigurationAvailable = true;

        public static PreProcessManager GetInstance() { lock (_locker) { _instance ??= new PreProcessManager(); return _instance; } }
        internal static PreProcessManager? Current { get { lock (_locker) { return _instance; } } }

        public ObservableCollection<PreProcessAction> Processes { get; } = new ObservableCollection<PreProcessAction>();
        public ObservableCollection<PreProcessTypeOption> AvailableProcessTypes { get; } = new ObservableCollection<PreProcessTypeOption>();

        public RelayCommand EditCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }
        public RelayCommand ReloadCommand { get; }
        public RelayCommand AddProcessCommand { get; set; }
        public RelayCommand RemoveProcessCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PreProcessTypeOption SelectedProcessType { get => _SelectedProcessType; set { _SelectedProcessType = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessTypeOption _SelectedProcessType;

        public PreProcessAction SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private PreProcessAction _SelectedProcess;

        public string PersistPath => _persistFilePath;
        public int ProcessCount => Processes.Count;
        public int EnabledCount => Processes.Count(p => p.IsEnabled);
        public bool HasConfigurationLoadFailure => !IsConfigurationAvailable;

        public bool IsConfigurationAvailable
        {
            get => _isConfigurationAvailable;
            private set
            {
                if (!SetProperty(ref _isConfigurationAvailable, value)) return;
                OnPropertyChanged(nameof(HasConfigurationLoadFailure));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public string LastSaveStatus { get => _LastSaveStatus; private set { _LastSaveStatus = value; OnPropertyChanged(); } }
        private string _LastSaveStatus = Properties.Resources.Flow_PreProcess_NotSaved;

        public PreProcessManager() : this(PersistFilePath)
        {
        }

        internal PreProcessManager(string persistFilePath)
        {
            _persistFilePath = Path.GetFullPath(persistFilePath);
            LoadAvailableProcessTypes();
            Processes.CollectionChanged += Processes_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            SaveCommand = new RelayCommand(a => SavePersisted(), _ => IsConfigurationAvailable);
            ReloadCommand = new RelayCommand(_ =>
            {
                if (HasConfigurationLoadFailure) LoadPersisted(allowMissingForFirstRun: false);
            }, _ => HasConfigurationLoadFailure);
            AddProcessCommand = new RelayCommand(a => AddProcess(a), a => IsConfigurationAvailable && (a is PreProcessTypeOption || SelectedProcessType != null));
            RemoveProcessCommand = new RelayCommand(a => RemoveSelectedProcess(), a => SelectedProcess != null);
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());

            SelectedProcessType = AvailableProcessTypes.FirstOrDefault();
            if (LoadPersisted(allowMissingForFirstRun: true) == PersistedLoadOutcome.Missing)
            {
                InitializeDefaultProcesses();
            }

            if (IsConfigurationAvailable) SubscribeAllActions();
            NotifyProcessSummaryChanged();
        }

        private void LoadAvailableProcessTypes()
        {
            var options = new List<PreProcessTypeOption>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in AssemblyHandler.GetInstance().GetTypes(assembly).Where(IsSelectablePreProcessType))
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
            return typeof(IPreProcessor).IsAssignableFrom(type)
                && !type.IsAbstract
                && type.GetConstructor(Type.EmptyTypes) != null
                && type.GetCustomAttributes(typeof(PreProcessAttribute), false).OfType<PreProcessAttribute>().Any();
        }

        private void InitializeDefaultProcesses()
        {
            if (!_processTypesByFullName.TryGetValue(typeof(FolderSizePreProcessor).FullName, out Type cacheCleanupType))
            {
                return;
            }

            var process = CreateProcess(cacheCleanupType);
            if (process == null)
            {
                return;
            }

            var action = new PreProcessAction(process) { IsEnabled = true };
            Processes.Add(action);
            SelectedProcess = action;
        }

        private IPreProcessor? CreateProcess(Type processType)
        {
            try
            {
                return Activator.CreateInstance(processType) as IPreProcessor;
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

            if (!action.HasInvalidConfiguration && action.Process.GetConfig() is INotifyPropertyChanged notifyConfig)
            {
                notifyConfig.PropertyChanged -= ProcessConfig_PropertyChanged;
                notifyConfig.PropertyChanged += ProcessConfig_PropertyChanged;
            }
        }

        private void UnsubscribeAction(PreProcessAction action)
        {
            action.PropertyChanged -= Action_PropertyChanged;
            if (!action.HasInvalidConfiguration && action.Process.GetConfig() is INotifyPropertyChanged notifyConfig)
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
            if (!IsConfigurationAvailable) return;
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

        private PersistedLoadOutcome LoadPersisted(bool allowMissingForFirstRun)
        {
            _isLoading = true;
            try
            {
                string json;
                try { json = File.ReadAllText(PersistPath); }
                catch (Exception error) when (allowMissingForFirstRun && (error is FileNotFoundException or DirectoryNotFoundException))
                {
                    LastSaveStatus = Properties.Resources.Flow_PreProcess_ConfigNotFound;
                    return PersistedLoadOutcome.Missing;
                }

                var list = JsonConvert.DeserializeObject<List<PreProcessPersist?>>(json)
                    ?? throw new InvalidDataException("The preprocessing configuration must contain an array of actions.");
                var loadedActions = new List<PreProcessAction>(list.Count);
                int skippedCount = 0;

                foreach (var item in list)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.ProcessTypeFullName))
                        throw new InvalidDataException("A preprocessing action must identify its processor type.");

                    if (!_processTypesByFullName.TryGetValue(item.ProcessTypeFullName, out Type processType))
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

                    loadedActions.Add(CreateAction(process, item));
                }

                foreach (var action in Processes) UnsubscribeAction(action);
                Processes.Clear();
                foreach (var action in loadedActions) Processes.Add(action);
                SelectedProcess = Processes.FirstOrDefault();
                IsConfigurationAvailable = true;
                if (Processes.Any(action => action.HasInvalidConfiguration))
                    LastSaveStatus = Properties.Resources.Flow_PreProcess_ConfigLoadFailed;
                else
                    LastSaveStatus = skippedCount > 0
                        ? string.Format(Properties.Resources.Flow_PreProcess_ConfigLoadedWithSkipped, DateTime.Now.ToString("HH:mm:ss"), skippedCount)
                        : string.Format(Properties.Resources.Flow_PreProcess_ConfigLoaded, DateTime.Now.ToString("HH:mm:ss"));
                return PersistedLoadOutcome.Loaded;
            }
            catch (Exception ex)
            {
                log.Error("加载预处理配置失败", ex);
                IsConfigurationAvailable = false;
                LastSaveStatus = EngineLocalization.Get("Flow_PreProcess_ConfigUnavailable");
                return PersistedLoadOutcome.Failed;
            }
            finally
            {
                _isLoading = false;
                NotifyProcessSummaryChanged();
                RefreshProcessView();
            }
        }

        private static PreProcessAction CreateAction(IPreProcessor process, PreProcessPersist item)
        {
            bool invalidConfiguration = false;
            try { process.SetConfig(item.ConfigJson ?? string.Empty); }
            catch (Exception error)
            {
                invalidConfiguration = true;
                log.Warn($"加载预处理动作 {item.ProcessTypeFullName} 配置失败。", error);
            }

            var action = new PreProcessAction(process)
            {
                HasInvalidConfiguration = invalidConfiguration,
                UnreadableConfigJson = invalidConfiguration ? item.ConfigJson : null
            };

            if (!string.IsNullOrWhiteSpace(item.ActionName))
            {
                action.ActionName = item.ActionName;
            }

            action.IsEnabled = item.IsEnabled ?? false;
            return action;
        }

        public void SavePersisted()
        {
            if (!IsConfigurationAvailable) return;
            try
            {
                var list = Processes.Select(action => new PreProcessPersist
                {
                    ProcessTypeFullName = action.Process.GetType().FullName,
                    ConfigJson = action.HasInvalidConfiguration
                        ? action.UnreadableConfigJson
                        : JsonConvert.SerializeObject(action.Process.GetConfig()),
                    ActionName = action.ActionName,
                    IsEnabled = action.IsEnabled
                }).ToList();

                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                WriteConfigAtomically(json);
                LastSaveStatus = string.Format(Properties.Resources.Flow_PreProcess_AutoSaved, DateTime.Now.ToString("HH:mm:ss"));
            }
            catch (Exception ex)
            {
                log.Error("保存预处理器配置失败", ex);
                LastSaveStatus = Properties.Resources.Flow_PreProcess_SaveFailedCheckLog;
            }
        }

        private void WriteConfigAtomically(string json)
        {
            string directory = Path.GetDirectoryName(PersistPath)!;
            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $".preprocess-config-{Guid.NewGuid():N}.tmp");
            bool created = false;
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    using var writer = new StreamWriter(stream);
                    writer.Write(json);
                }
                File.Move(temporaryPath, PersistPath, overwrite: true);
            }
            catch
            {
                if (created)
                {
                    try { File.Delete(temporaryPath); }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        log.Warn($"Failed to remove preprocessing configuration temporary file {temporaryPath}.", error);
                    }
                }
                throw;
            }
        }

        public List<IPreProcessor> GetEnabledProcesses(string flowName)
        {
            // Retain the flowName parameter for existing callers; preprocessing is shared by all flows.
            return GetEnabledActions().Select(a => a.Process).ToList();
        }

        private List<PreProcessAction> GetEnabledActions()
        {
            if (!IsConfigurationAvailable)
                throw new InvalidOperationException(EngineLocalization.Get("Flow_PreProcess_ConfigUnavailable"));
            List<PreProcessAction> enabled = Processes.Where(action => action.IsEnabled).ToList();
            if (enabled.Find(action => action.HasInvalidConfiguration) is { } invalid)
                throw new InvalidOperationException(string.Format(EngineLocalization.Get("Flow_PreProcess_InvalidEnabledAction"), invalid.DisplayName));
            return enabled;
        }

        public async Task<bool> ExecuteAsync(string flowName, string serialNumber, ObservableCollection<CVBaseServerNode>? serverNodes = null)
        {
            try
            {
                var matchingActions = GetEnabledActions();
                if (matchingActions.Count == 0)
                {
                    return true;
                }

                log.Debug($"匹配到 {matchingActions.Count} 个已启用的预处理动作 {flowName}");
                var ctx = new PreProcessContext
                {
                    FlowName = flowName,
                    SerialNumber = serialNumber,
                    CVBaseServerNodes = serverNodes,
                };

                foreach (var action in matchingActions)
                {
                    log.Debug($"执行预处理动作 {action.DisplayName}");
                    var actionTiming = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        bool success = await action.Process.PreProcess(ctx);
                        if (!success)
                        {
                            log.Warn($"预处理动作 {action.DisplayName} 执行返回失败，Flow={flowName}，SN={serialNumber}，耗时 {actionTiming.Elapsed.TotalMilliseconds:F3}ms");
                            return false;
                        }
                        if (actionTiming.ElapsedMilliseconds >= 100 || log.IsDebugEnabled)
                        {
                            string timingJson = JsonConvert.SerializeObject(new
                            {
                                Event = "PreProcessTiming", FlowName = flowName, SerialNumber = serialNumber,
                                Action = action.DisplayName, ProcessType = action.Process.GetType().FullName,
                                ElapsedMs = Math.Round(actionTiming.Elapsed.TotalMilliseconds, 3), Status = "Completed",
                            });
                            if (actionTiming.ElapsedMilliseconds >= 100) log.Info(timingJson);
                            else log.Debug(timingJson);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"预处理动作 {action.DisplayName} 执行异常，Flow={flowName}，SN={serialNumber}，耗时 {actionTiming.Elapsed.TotalMilliseconds:F3}ms", ex);
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

        public static string GetProcessDisplayName(IPreProcessor processor)
        {
            return PreProcessMetadata.FromProcess(processor).DisplayName;
        }
    }
}
