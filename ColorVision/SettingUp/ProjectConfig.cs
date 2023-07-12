using ColorVision.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    /// <summary>
    /// 工程配置
    /// </summary>
    public class ProjectConfig: ViewModelBase
    {
        public string ProjectName { get => _ProjectName; set { _ProjectName = value; NotifyPropertyChanged(); } }
        private string _ProjectName;

        public string ProjectFullName { get => _ProjectFullName; set { _ProjectFullName = value; NotifyPropertyChanged(); } }
        private string _ProjectFullName;

        public string CachePath { get => _CachePath; set { _CachePath = value; NotifyPropertyChanged(); } }
        private string _CachePath;

    }
}
