#pragma warning disable CA1822,CA1852,CS8601,CS8604,CS8621,CS8714
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
using System.Windows.Input;

namespace ColorVision.Engine.Batch
{
    /// <summary>
    /// Simplified persistence model for pre-processors
    /// </summary>
    internal class PreProcessPersist
    {
        public string ProcessTypeFullName { get; set; }
        public string ConfigJson { get; set; }
    }

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
        private bool _isLoading;

        public RelayCommand EditCommand { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public IPreProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IPreProcess _SelectedProcess;

        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public string PersistPath => PersistFilePath;

        public int ProcessCount => Processes.Count;

        public int EnabledCount => Processes.Count(IsEnabledPreProcessor);

        public string LastSaveStatus { get => _LastSaveStatus; private set { _LastSaveStatus = value; OnPropertyChanged(); } }
        private string _LastSaveStatus = "尚未保存";

        public PreProcessManager()
        {
            LoadProcesses();
            Processes.CollectionChanged += Processes_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            SaveCommand = new RelayCommand(a => SavePersisted());
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            LoadPersisted();
            SubscribeAllConfigs();
            NotifyProcessSummaryChanged();
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

        private void Processes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (IPreProcess process in e.NewItems)
                {
                    SubscribeConfig(process);
                }
            }
            if (e.OldItems != null)
            {
                foreach (IPreProcess process in e.OldItems)
                {
                    UnsubscribeConfig(process);
                }
            }
            NotifyProcessSummaryChanged();
            if (!_isLoading)
            {
                SavePersisted();
            }
        }

        private void Process_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            NotifyProcessSummaryChanged();
            if (_isLoading) return;
            SavePersisted();
        }

        private void SubscribeAllConfigs()
        {
            foreach (var process in Processes)
            {
                SubscribeConfig(process);
            }
        }

        private void SubscribeConfig(IPreProcess process)
        {
            if (process.GetConfig() is INotifyPropertyChanged notifyConfig)
            {
                notifyConfig.PropertyChanged -= Process_PropertyChanged;
                notifyConfig.PropertyChanged += Process_PropertyChanged;
            }
        }

        private void UnsubscribeConfig(IPreProcess process)
        {
            if (process.GetConfig() is INotifyPropertyChanged notifyConfig)
            {
                notifyConfig.PropertyChanged -= Process_PropertyChanged;
            }
        }

        private void NotifyProcessSummaryChanged()
        {
            OnPropertyChanged(nameof(ProcessCount));
            OnPropertyChanged(nameof(EnabledCount));
            CommandManager.InvalidateRequerySuggested();
        }

        public void Edit()
        {
            PreProcessManagerWindow processManagerWindow = new PreProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            processManagerWindow.DataContext = this;
            processManagerWindow.ShowDialog();
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

        private void LoadPersisted()
        {
            _isLoading = true;
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                if (!File.Exists(PersistFilePath))
                {
                    LastSaveStatus = "未找到配置文件";
                    return;
                }
                
                string json = File.ReadAllText(PersistFilePath);
                var list = JsonConvert.DeserializeObject<List<PreProcessPersist>>(json) ?? new List<PreProcessPersist>();

                var processMap = Processes
                    .Where(p => !string.IsNullOrWhiteSpace(p.GetType().FullName))
                    .GroupBy(p => p.GetType().FullName)
                    .ToDictionary(g => g.Key, g => g.First());
                
                foreach (var item in list)
                {
                    if (item.ProcessTypeFullName == null || !processMap.TryGetValue(item.ProcessTypeFullName, out var process))
                    {
                        continue;
                    }

                    try
                    {
                        process.SetConfig(item.ConfigJson);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"加载预处理器 {item.ProcessTypeFullName} 配置失败: {ex.Message}");
                    }
                }

                var orderedProcesses = new List<IPreProcess>();
                var addedTypes = new HashSet<string>();

                foreach (var item in list)
                {
                    if (item.ProcessTypeFullName == null || addedTypes.Contains(item.ProcessTypeFullName))
                    {
                        continue;
                    }

                    if (processMap.TryGetValue(item.ProcessTypeFullName, out var process))
                    {
                        orderedProcesses.Add(process);
                        addedTypes.Add(item.ProcessTypeFullName);
                    }
                }

                orderedProcesses.AddRange(Processes.Where(p => p.GetType().FullName == null || !addedTypes.Contains(p.GetType().FullName)));

                Processes.Clear();
                foreach (var process in orderedProcesses)
                {
                    Processes.Add(process);
                }

                LastSaveStatus = $"已加载配置 {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                log.Error("加载预处理器配置失败", ex);
                LastSaveStatus = "加载配置失败";
            }
            finally
            {
                _isLoading = false;
                NotifyProcessSummaryChanged();
            }
        }

        public void SavePersisted()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                
                var list = new List<PreProcessPersist>();
                foreach (var p in Processes)
                {
                    if (p.GetConfig() == null) continue;
                    
                    try
                    {
                        var persist = new PreProcessPersist
                        {
                            ProcessTypeFullName = p.GetType().FullName,
                            ConfigJson = JsonConvert.SerializeObject(p.GetConfig())
                        };
                        list.Add(persist);
                    }
                    catch (Exception ex)
                    {
                        log.Warn($"序列化预处理器 {p.GetType().Name} 配置失败: {ex.Message}");
                    }
                }
                
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
                LastSaveStatus = $"已自动保存 {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                log.Error("保存预处理器配置失败", ex);
                LastSaveStatus = "保存失败，请查看日志";
            }
        }

        public List<IPreProcess> GetEnabledProcesses(string flowName)
        {
            return Processes.Where(p => IsValidEnabledPreProcessor(p, flowName)).ToList();
        }

        public async Task<bool> ExecuteAsync(string flowName, string serialNumber, ObservableCollection<CVBaseServerNode>? serverNodes = null)
        {
            try
            {
                var matchingProcessors = GetEnabledProcesses(flowName);
                if (matchingProcessors.Count == 0)
                {
                    return true;
                }

                log.Info($"匹配到 {matchingProcessors.Count} 个已启用的预处理 {flowName}");
                var ctx = new IPreProcessContext
                {
                    FlowName = flowName,
                    SerialNumber = serialNumber,
                    CVBaseServerNodes = serverNodes,
                };

                foreach (var processor in matchingProcessors)
                {
                    var metadata = PreProcessMetadata.FromProcess(processor);
                    log.Info($"执行预处理 {metadata.DisplayName}");
                    try
                    {
                        bool success = await processor.PreProcess(ctx);
                        if (!success)
                        {
                            log.Warn($"预处理 {metadata.DisplayName} 执行返回失败");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"预处理 {metadata.DisplayName} 执行异常", ex);
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

        private static bool IsEnabledPreProcessor(IPreProcess processor)
        {
            return processor.GetConfig() is PreProcessConfigBase { IsEnabled: true };
        }

        private static bool IsValidEnabledPreProcessor(IPreProcess processor, string flowName)
        {
            if (processor.GetConfig() is PreProcessConfigBase baseConfig)
            {
                return baseConfig.IsEnabled && baseConfig.AppliesToTemplate(flowName);
            }

            return false;
        }
    }
}
