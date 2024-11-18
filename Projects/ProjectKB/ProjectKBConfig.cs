using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.Windows;

namespace ProjectKB
{
    public class ProjectKBConfig: ViewModelBase, IConfig
    {
        public static ProjectKBConfig Instance => ConfigService.Instance.GetRequiredService<ProjectKBConfig>();
        public RelayCommand OpenTemplateCommand { get; set; }
        public RelayCommand OpenFlowEngineToolCommand { get; set; }
        public RelayCommand OpenLogCommand { get; set; }

        public ProjectKBConfig()
        {
            OpenTemplateCommand = new RelayCommand(a => OpenTemplate());
            OpenFlowEngineToolCommand = new RelayCommand(a => OpenFlowEngineTool());
            TemplateItemSource = FlowParam.Params;
            OpenLogCommand = new RelayCommand(a => OpenLog());
        }
        public static void OpenLog()
        {
            WindowLog windowLog = new WindowLog() { Owner = Application.Current.GetActiveWindow() };
            windowLog.Show();
        }

        [JsonIgnore]
        public ObservableCollection<TemplateModel<FlowParam>> TemplateItemSource { get => _TemplateItemSource; set { _TemplateItemSource = value; NotifyPropertyChanged(); } }
        private ObservableCollection<TemplateModel<FlowParam>> _TemplateItemSource;

        public int TemplateSelectedIndex { get => _TemplateSelectedIndex; set { _TemplateSelectedIndex = value; NotifyPropertyChanged(); } }
        private int _TemplateSelectedIndex;
        public void OpenTemplate()
        {
            new TemplateEditorWindow(new TemplateFlow(), TemplateSelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public void OpenFlowEngineTool()
        {
            new FlowEngineToolWindow(FlowParam.Params[TemplateSelectedIndex].Value) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }

        public string SN { get => _SN; set { _SN = value; NotifyPropertyChanged(); } }
        private string _SN;

        public bool IsAutoUploadSn { get => _IsAutoUploadSn; set { _IsAutoUploadSn = value; NotifyPropertyChanged(); } }
        private bool _IsAutoUploadSn;
    }
}
