#pragma warning disable CA1852
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProjectARVRPro.Process
{
    public partial class ProcessMetaEditWindow : Window
    {
        private readonly ProcessMetaEditViewModel _viewModel;

        public string MetaName => _viewModel.MetaName.Trim();
        public TemplateModel<FlowParam>? SelectedTemplate => _viewModel.SelectedTemplate;
        public IProcess? SelectedProcess => _viewModel.SelectedProcess;
        public bool IsMetaEnabled => _viewModel.IsEnabled;

        public ProcessMetaEditWindow(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IProcess> processes,
            string title,
            string metaName = "",
            string flowTemplate = "",
            IProcess? process = null,
            bool isEnabled = true)
        {
            InitializeComponent();
            Title = title;
            _viewModel = new ProcessMetaEditViewModel(templates, processes, metaName, flowTemplate, process, isEnabled);
            DataContext = _viewModel;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MetaName))
            {
                MessageBox.Show(this, "名称不能为空", "ColorVision");
                return;
            }

            if (SelectedTemplate == null)
            {
                MessageBox.Show(this, "请选择流程模板", "ColorVision");
                return;
            }

            if (SelectedProcess == null)
            {
                MessageBox.Show(this, "请选择处理类", "ColorVision");
                return;
            }

            DialogResult = true;
        }
    }

    internal class ProcessMetaEditViewModel
    {
        public ObservableCollection<TemplateModel<FlowParam>> Templates { get; }
        public ObservableCollection<IProcess> Processes { get; }

        public string MetaName { get; set; }
        public TemplateModel<FlowParam>? SelectedTemplate { get; set; }
        public IProcess? SelectedProcess { get; set; }
        public bool IsEnabled { get; set; }

        public ProcessMetaEditViewModel(
            IEnumerable<TemplateModel<FlowParam>> templates,
            IEnumerable<IProcess> processes,
            string metaName,
            string flowTemplate,
            IProcess? process,
            bool isEnabled)
        {
            Templates = new ObservableCollection<TemplateModel<FlowParam>>(templates);
            Processes = new ObservableCollection<IProcess>(processes);
            MetaName = metaName;
            SelectedTemplate = Templates.FirstOrDefault(t => t.Key == flowTemplate) ?? Templates.FirstOrDefault();
            SelectedProcess = process == null
                ? Processes.FirstOrDefault()
                : Processes.FirstOrDefault(p => p.GetType().FullName == process.GetType().FullName) ?? Processes.FirstOrDefault();
            IsEnabled = isEnabled;
        }
    }
}