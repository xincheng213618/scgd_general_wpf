using ColorVision.Common.MVVM;
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

        public RelayCommand EditCommand { get; set; }

        public IPreProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IPreProcess _SelectedProcess;

        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public PreProcessManager()
        {
            LoadProcesses();
            Processes.CollectionChanged += Processes_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            MoveUpCommand = new RelayCommand(a => MoveUp(), a => CanMoveUp());
            MoveDownCommand = new RelayCommand(a => MoveDown(), a => CanMoveDown());
            LoadPersisted();
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
                    var config = process.GetConfig();
                    if (config is System.ComponentModel.INotifyPropertyChanged notifyConfig)
                    {
                        notifyConfig.PropertyChanged += Process_PropertyChanged;
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (IPreProcess process in e.OldItems)
                {
                    var config = process.GetConfig();
                    if (config is System.ComponentModel.INotifyPropertyChanged notifyConfig)
                    {
                        notifyConfig.PropertyChanged -= Process_PropertyChanged;
                    }
                }
            }
            SavePersisted();
        }

        private void Process_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            SavePersisted();
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
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                if (!File.Exists(PersistFilePath)) return;
                
                string json = File.ReadAllText(PersistFilePath);
                var list = JsonConvert.DeserializeObject<List<PreProcessPersist>>(json) ?? new List<PreProcessPersist>();
                
                Processes.CollectionChanged -= Processes_CollectionChanged; // Pause events
                
                foreach (var item in list)
                {
                    var process = Processes.FirstOrDefault(p => p.GetType().FullName == item.ProcessTypeFullName);
                    if (process != null)
                    {
                        // Apply the stored configuration
                        process.SetConfig(item.ConfigJson);
                    }
                }
                
                Processes.CollectionChanged += Processes_CollectionChanged; // Resume events
            }
            catch (Exception ex)
            {
                log.Error("加载预处理器配置失败", ex);
            }
        }

        private void SavePersisted()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                
                var list = Processes
                    .Where(p => p.GetConfig() != null)
                    .Select(p => new PreProcessPersist
                    {
                        ProcessTypeFullName = p.GetType().FullName,
                        ConfigJson = JsonConvert.SerializeObject(p.GetConfig())
                    })
                    .ToList();
                
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
            }
            catch (Exception ex)
            {
                log.Error("保存预处理器配置失败", ex);
            }
        }
    }
}
