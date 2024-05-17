using ColorVision.Common.MVVM;
using ColorVision.UI;

namespace ColorVision.Solution
{
    public class SolutionSetting: ViewModelBase,IConfig
    {
        public static SolutionSetting Instance => ConfigHandler.GetInstance().GetRequiredService<SolutionSetting>();

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

    }
}