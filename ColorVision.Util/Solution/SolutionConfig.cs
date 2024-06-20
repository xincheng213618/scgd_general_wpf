using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Solution
{


    /// <summary>
    /// 解决方案配置
    /// </summary>
    public class SolutionConfig : ViewModelBase
    {

        public string FullPath 
        { 
            get =>  _FullPath;
            set
            {
                _FullPath = value;
                NotifyPropertyChanged();
            }
        }
        private string _FullPath;


        public ObservableCollection<string> Path { get; set; }
    }
}