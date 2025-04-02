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


        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreatName = "新建工程";
         
        public string DefaultSaveName { get => _DefaultSaveName; set { _DefaultSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultSaveName = "yyyy/dd/MM HH:mm:ss";

        public string DefaultImageSaveName { get => _DefaultImageSaveName; set { _DefaultImageSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultImageSaveName = "yyyyddMMHHmmss";

        public bool IsMemoryLackWarning{ get => _IsMemoryLackWarning; set { _IsMemoryLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsMemoryLackWarning = true;

        public bool IsLackWarning { get => _IsLackWarning; set { _IsLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsLackWarning = true;

        public bool IsShowLackWarning { get => _IsShowLackWarning; set { _IsShowLackWarning = value; NotifyPropertyChanged(); } }
        private bool _IsShowLackWarning = true;

    }
}