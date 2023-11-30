using ColorVision.MVVM;

namespace ColorVision.Solution
{
    public class SolutionSetting: ViewModelBase
    {
        public string DefaultCreatName { get => _DefaultCreatName; set { _DefaultCreatName = value; NotifyPropertyChanged(); } }
        private string _DefaultCreatName = "新建工程";
         
        public string DefaultSaveName { get => _DefaultSaveName; set { _DefaultSaveName = value; NotifyPropertyChanged(); } }
        private string _DefaultSaveName = "yyyy/dd/MM HH:mm:ss";
    }

    /// <summary>
    /// 解决方案配置
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {
        public string FullName 
        { 
            get =>  _SolutionFullName;
            set
            {
                _SolutionFullName = value;
                NotifyPropertyChanged();
            }
        }
        private string _SolutionFullName;
    }
}