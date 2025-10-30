﻿using ColorVision.Common.MVVM;
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

namespace ColorVision.Engine.Batch
{
    public class BatchConfig : ViewModelBase,IConfig
    {
        public static BatchConfig Instance => ConfigService.Instance.GetRequiredService<BatchConfig>();
        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public BatchConfig()
        {
            EditCommand = new RelayCommand(a => BatchManager.GetInstance().Edit());
        }

        [DisplayName("默认保存路径"), PropertyEditorType(typeof(TextSelectFolderPropertiesEditor))]
        public string SavePath { get => _SavePath; set { _SavePath = value; OnPropertyChanged(); } }
        private string _SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Batch"); 
    }


    public class BatchManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(BatchManager));

        private const string PersistFileName = "ProcessMetas.json";
        private static string PersistDirectory => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + $"\\ColorVision\\Config\\";
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static BatchManager _instance;
        private static readonly object _locker = new();
        public static BatchManager GetInstance() { lock (_locker) { _instance ??= new BatchManager(); return _instance; } }

        public BatchConfig BatchConfig { get; set; }

        public ObservableCollection<IBatchProcess> Processes { get; } = new ObservableCollection<IBatchProcess>();

        public ObservableCollection<BatchProcessMeta> ProcessMetas { get; } = new ObservableCollection<BatchProcessMeta>();

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;
        public RelayCommand EditCommand { get; set; }

        // New properties for creation
        public string NewMetaName { get => _NewMetaName; set { _NewMetaName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewMetaName;

        public TemplateModel<FlowParam> SelectedTemplate { get => _SelectedTemplate; set { _SelectedTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _SelectedTemplate;

        public IBatchProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IBatchProcess _SelectedProcess;

        public TemplateModel<FlowParam> UpdateTemplate { get => _UpdateTemplate; set { _UpdateTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _UpdateTemplate;

        public IBatchProcess UpdateProcess { get => _UpdateProcess; set { _UpdateProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IBatchProcess _UpdateProcess;

        public BatchProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); OnSelectedProcessMetaChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private BatchProcessMeta _SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }
        public RelayCommand UpdateMetaCommand { get; set; }
        public RelayCommand MoveUpCommand { get; set; }
        public RelayCommand MoveDownCommand { get; set; }

        public BatchManager()
        {
            BatchConfig = ConfigService.Instance.GetRequiredService<BatchConfig>();

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
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                try
                {
                    foreach (Type type in assembly.GetTypes().Where(t => typeof(IBatchProcess).IsAssignableFrom(t) && !t.IsAbstract))
                    {
                        if (Activator.CreateInstance(type) is IBatchProcess process)
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
                foreach (BatchProcessMeta meta in e.NewItems)
                {
                    meta.PropertyChanged += Meta_PropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (BatchProcessMeta meta in e.OldItems)
                {
                    meta.PropertyChanged -= Meta_PropertyChanged;
                }
            }
            SavePersistedMetas();
        }

        private void Meta_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 任意属性变更即持久化，避免频繁：可加节流，这里简单实现
            SavePersistedMetas();
        }

        public void Edit()
        {
            BatchProcessManagerWindow processManagerWindow = new BatchProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
                MessageBox.Show(Application.Current.GetActiveWindow(), "名称重复", "ColorVision");
                return;
            }
            ProcessMetas.Add(new BatchProcessMeta
            {
                Name = NewMetaName,
                TemplateName = SelectedTemplate.Key,
                BatchProcess = SelectedProcess
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
                // Populate update fields when a BatchProcessMeta is selected
                UpdateTemplate = templateModels.FirstOrDefault(t => t.Key == SelectedProcessMeta.TemplateName);
                UpdateProcess = Processes.FirstOrDefault(p => p.GetType().FullName == SelectedProcessMeta.BatchProcess?.GetType().FullName);
            }
        }

        private bool CanUpdateMeta()
        {
            return SelectedProcessMeta != null && UpdateTemplate != null && UpdateProcess != null;
        }

        private void UpdateMeta()
        {
            if (!CanUpdateMeta()) return;
            
            // Update the selected BatchProcessMeta with new values
            SelectedProcessMeta.TemplateName = UpdateTemplate.Key;
            SelectedProcessMeta.BatchProcess = UpdateProcess;
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
                var list = JsonConvert.DeserializeObject<List<BatchProcessMetaPersist>>(json) ?? new List<BatchProcessMetaPersist>();
                ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // 暂停事件
                foreach (var item in list)
                {
                    IBatchProcess proc = Processes.FirstOrDefault(p => p.GetType().FullName == item.ProcessTypeFullName);
                    if (proc == null)
                    {
                        // 尝试反射创建
                        try
                        {
                            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(x => x.FullName == item.ProcessTypeFullName && typeof(IBatchProcess).IsAssignableFrom(x));
                            if (t != null)
                            {
                                proc = Activator.CreateInstance(t) as IBatchProcess;
                                if (proc != null && !Processes.Any(p => p.GetType().FullName == proc.GetType().FullName))
                                    Processes.Add(proc);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"无法实例化进程类型 {item.ProcessTypeFullName}: {ex.Message}");
                        }
                    }
                    BatchProcessMeta meta = new BatchProcessMeta() { Name = item.Name, TemplateName = item.TemplateName, BatchProcess = proc };
                    meta.PropertyChanged += Meta_PropertyChanged;
                    ProcessMetas.Add(meta);
                }
                ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged; // 恢复事件
            }
            catch (Exception ex)
            {
                log.Error("加载ProcessMetas失败", ex);
            }
        }

        private void SavePersistedMetas()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                var list = ProcessMetas.Select(m => new BatchProcessMetaPersist
                {
                    Name = m.Name,
                    TemplateName = m.TemplateName,
                    ProcessTypeFullName = m.BatchProcess?.GetType().FullName
                }).ToList();
                string json = JsonConvert.SerializeObject(list, Formatting.Indented);
                File.WriteAllText(PersistFilePath, json);
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
