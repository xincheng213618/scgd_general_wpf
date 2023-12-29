using ColorVision.MVVM;

namespace ColorVision.Solution
{
    public class SolutionSetting: ViewModelBase
    {
        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreatName = "新建工程";
         
        public string DefaultSaveName { get => _DefaultSaveName; set { _DefaultSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultSaveName = "yyyy/dd/MM HH:mm:ss";

        public string DefaultImageSaveName { get => _DefaultImageSaveName; set { _DefaultImageSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultImageSaveName = "yyyyddMMHHmmss";

    }
}