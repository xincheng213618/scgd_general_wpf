using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;

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
        public string Name { get; set; }

        public string FlowTemplate { get; set; }

        public IProcess Process { get; set; }
    }

    public class ProcessManager
    {
        private static readonly ILog log = LogManager.GetLogger(nameof(ProcessManager));

        private static ProcessManager _instance;
        private static readonly object _locker = new();
        public static ProcessManager GetInstance() { lock (_locker) { _instance ??= new ProcessManager(); return _instance; } }

        public ObservableCollection<IProcess> Processes { get; } = new ObservableCollection<IProcess>();

        public ObservableCollection<ProcessMeta> ProcessMetas { get; } = new ObservableCollection<ProcessMeta>();

        public ObservableCollection<TemplateModel<FlowParam>> templateModels = TemplateFlow.Params;

        public ProcessManager()
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

            foreach (var process in Processes)
            {
                ProcessMetas.Add(new ProcessMeta()
                {
                    Name = process.GetType().Name,
                    FlowTemplate = process.GetType().DeclaringType?.Namespace ?? "Unknown",
                    Process = process
                });
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
    }
}
