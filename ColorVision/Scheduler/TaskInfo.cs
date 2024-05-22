using ColorVision.Common.MVVM;

namespace ColorVision.Scheduler
{
    public class TaskInfo: ViewModelBase
    {
        public string JobName { get => _JobName; set { _JobName = value; NotifyPropertyChanged(); } }
        private string _JobName;
        public string GroupName { get => _GroupName; set { _GroupName = value; NotifyPropertyChanged(); } }
        private string _GroupName;
        public string NextFireTime { get => _NextFireTime; set { _NextFireTime = value; NotifyPropertyChanged(); } }
        private string _NextFireTime;
        public string PreviousFireTime { get => _PreviousFireTime; set { _PreviousFireTime = value; NotifyPropertyChanged(); } }
        private string _PreviousFireTime;
    }
}
