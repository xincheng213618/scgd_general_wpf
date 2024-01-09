using ColorVision.MVVM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.SettingUp
{
    public class ViewConfig : ViewModelBase
    {
        private static ViewConfig _instance;
        private static readonly object _locker = new();
        public static ViewConfig GetInstance() { lock (_locker) { return _instance ??= new ViewConfig(); } }

        public bool IsAutoSelect { get => _IsAutoSelect; set { _IsAutoSelect = value; NotifyPropertyChanged(); } }
        private bool _IsAutoSelect =true;
    }
}
