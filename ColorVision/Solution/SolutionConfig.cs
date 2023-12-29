using ColorVision.MVVM;

namespace ColorVision.Solution
{

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