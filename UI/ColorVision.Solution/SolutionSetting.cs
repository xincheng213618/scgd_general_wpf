using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Solution
{

    [DisplayName("解决方案配置")]
    public class SolutionSetting: ViewModelBase,IConfig
    {
        public static SolutionSetting Instance => ConfigService.Instance.GetRequiredService<SolutionSetting>();

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }

        public SolutionSetting()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }


        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; OnPropertyChanged(); } }
        private string _DefaultCreatName = "新建工程";

    }
}