using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;
using System;
using System.Linq;
using System.Windows.Input;
using System.Collections.Specialized;
using Newtonsoft.Json;
using System.IO;

namespace ProjectARVRPro
{
    public class IProcessExecutionContext
    {
        public MeasureBatchModel Batch { get; set; }
        public ProjectARVRReuslt Result { get; set; }
        public ObjectiveTestResult ObjectiveTestResult { get; set; }
        public ObjectiveTestResultFix ObjectiveTestResultFix { get; set; }
        public ARVRRecipeConfig RecipeConfig { get; set; }
        public ILog Logger { get; set; }
    }
    public interface IProcess
    {
        public bool Execute(IProcessExecutionContext processExecutionContext);
    }

    public class ProcessMeta:ViewModelBase
    {
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public string FlowTemplate { get => _FlowTemplate; set { _FlowTemplate = value; OnPropertyChanged(); } }
        private string _FlowTemplate;

        public IProcess Process { get => _Process; set { _Process = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProcessTypeName)); } }
        private IProcess _Process;

        public string ProcessTypeName => Process?.GetType().Name ?? string.Empty;
        public string ProcessTypeFullName => Process?.GetType().FullName ?? string.Empty;
    }

    internal class ProcessMetaPersist
    {
        public string Name { get; set; }
        public string FlowTemplate { get; set; }
        public string ProcessTypeFullName { get; set; }
    }

    public class ProcessManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ProcessManager));
        private const string PersistFileName = "ProcessMetas.json";
        private static string PersistDirectory => ViewResultManager.DirectoryPath; // 复用配置目录
        private static string PersistFilePath => Path.Combine(PersistDirectory, PersistFileName);

        private static ProcessManager _instance;
        private static readonly object _locker = new();
        public static ProcessManager GetInstance() { lock (_locker) { _instance ??= new ProcessManager(); return _instance; } }

        public ObservableCollection<IProcess> Processes { get; } = new ObservableCollection<IProcess>();

        public ObservableCollection<ProcessMeta> ProcessMetas { get; } = new ObservableCollection<ProcessMeta>();

        public ObservableCollection<TemplateModel<FlowParam>> templateModels { get; set; } = TemplateFlow.Params;
        public RelayCommand EditCommand { get; set; }

        // New properties for creation
        public string NewMetaName { get => _NewMetaName; set { _NewMetaName = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private string _NewMetaName;

        public TemplateModel<FlowParam> SelectedTemplate { get => _SelectedTemplate; set { _SelectedTemplate = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private TemplateModel<FlowParam> _SelectedTemplate;

        public IProcess SelectedProcess { get => _SelectedProcess; set { _SelectedProcess = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private IProcess _SelectedProcess;

        public ProcessMeta SelectedProcessMeta { get => _SelectedProcessMeta; set { _SelectedProcessMeta = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private ProcessMeta _SelectedProcessMeta;

        public RelayCommand AddMetaCommand { get; set; }
        public RelayCommand RemoveMetaCommand { get; set; }

        public ProcessManager()
        {
            LoadProcesses();
            ProcessMetas.CollectionChanged += ProcessMetas_CollectionChanged;
            EditCommand = new RelayCommand(a => Edit());
            AddMetaCommand = new RelayCommand(a => AddMeta(), a => CanAddMeta());
            RemoveMetaCommand = new RelayCommand(a => RemoveMeta(), a => SelectedProcessMeta != null);
            LoadPersistedMetas();
        }

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

        private void ProcessMetas_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
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
            SavePersistedMetas();
        }

        private void Meta_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 任意属性变更即持久化，避免频繁：可加节流，这里简单实现
            SavePersistedMetas();
        }

        public void Edit()
        {
            ProcessManagerWindow processManagerWindow = new ProcessManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
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
            ProcessMetas.Add(new ProcessMeta
            {
                Name = NewMetaName,
                FlowTemplate = SelectedTemplate.Key,
                Process = SelectedProcess
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

        private void LoadPersistedMetas()
        {
            try
            {
                if (!Directory.Exists(PersistDirectory)) Directory.CreateDirectory(PersistDirectory);
                if (!File.Exists(PersistFilePath)) return;
                string json = File.ReadAllText(PersistFilePath);
                var list = JsonConvert.DeserializeObject<List<ProcessMetaPersist>>(json) ?? new List<ProcessMetaPersist>();
                ProcessMetas.CollectionChanged -= ProcessMetas_CollectionChanged; // 暂停事件
                foreach (var item in list)
                {
                    IProcess proc = Processes.FirstOrDefault(p => p.GetType().FullName == item.ProcessTypeFullName);
                    if (proc == null)
                    {
                        // 尝试反射创建
                        try
                        {
                            var t = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()).FirstOrDefault(x => x.FullName == item.ProcessTypeFullName && typeof(IProcess).IsAssignableFrom(x));
                            if (t != null)
                            {
                                proc = Activator.CreateInstance(t) as IProcess;
                                if (proc != null && !Processes.Any(p => p.GetType().FullName == proc.GetType().FullName))
                                    Processes.Add(proc);
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"无法实例化进程类型 {item.ProcessTypeFullName}: {ex.Message}");
                        }
                    }
                    ProcessMeta meta = new ProcessMeta() { Name = item.Name, FlowTemplate = item.FlowTemplate, Process = proc };
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
                var list = ProcessMetas.Select(m => new ProcessMetaPersist
                {
                    Name = m.Name,
                    FlowTemplate = m.FlowTemplate,
                    ProcessTypeFullName = m.Process?.GetType().FullName
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

    /// <summary>
    /// ProcessManagerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ProcessManagerWindow : Window
    {
        public ProcessManagerWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {

        }

        private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}
