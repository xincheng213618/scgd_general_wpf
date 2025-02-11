using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;

namespace ColorVision.Solution.V
{
    /// <summary>
    /// 解决方案配置
    /// </summary>
    public class CVSolutionConfig : ViewModelBase
    {
        public string FilePath { get; set; }

        public string Vpath { get; set; }
    
        public bool IsSetting { get; set; }
        public bool IsSetting1 { get; set; }

        public ObservableCollection<string> Path { get; set; }

    }
}
