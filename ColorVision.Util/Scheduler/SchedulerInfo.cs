using ColorVision.Common.MVVM;
using System;
using System.Threading.Tasks;

namespace ColorVision.Scheduler
{
    public enum SchedulerStatus
    {
        Ready,
        Running,
        Paused,
    }

    public class SchedulerInfo: ViewModelBase
    {
        public RelayCommand PausedCommand { get; set; }
        public RelayCommand DeleteCommand { get; set; }
        public RelayCommand RunCommand { get; set; }
        public RelayCommand ResumeJobCommand { get; set; }

        public SchedulerInfo()
        {
            PausedCommand = new RelayCommand(ExecuteStopCommand,a => Status != SchedulerStatus.Paused);
            ResumeJobCommand = new RelayCommand(ExecuteResumeJobCommand, a => Status == SchedulerStatus.Paused);
            DeleteCommand = new RelayCommand(ExecuteDeleteCommand, a => true);
        }

        private async void ExecuteResumeJobCommand(object obj)
        {
            var schedulerManager = QuartzSchedulerManager.GetInstance();
            await schedulerManager.ResumeJob(JobName, GroupName);
            Status = SchedulerStatus.Ready;
        }

        private async void ExecuteStopCommand(object parameter)
        {
            var schedulerManager = QuartzSchedulerManager.GetInstance();
            await schedulerManager.StopJob(JobName, GroupName);
            Status = SchedulerStatus.Paused;
        }
        private async void ExecuteDeleteCommand(object parameter)
        {
            var schedulerManager = QuartzSchedulerManager.GetInstance();
            await schedulerManager.RemoveJob(JobName, GroupName);
            schedulerManager.TaskInfos.Remove(this);
        }

        public SchedulerStatus Status { get => _Status; set { _Status = value; NotifyPropertyChanged(); } }
        private SchedulerStatus _Status;

        public int MaxCount { get => _MaxCount; set { _MaxCount = value; NotifyPropertyChanged(); } }
        private int _MaxCount = 1;

        public int RunCount { get => _RunCount; set { _RunCount = value; NotifyPropertyChanged(); } }
        private int _RunCount = 0;


        public string JobName { get => _JobName; set { _JobName = value; NotifyPropertyChanged(); } }
        private string _JobName;
        public string GroupName { get => _GroupName; set { _GroupName = value; NotifyPropertyChanged(); } }
        private string _GroupName;

        public string CronExpression { get => _CronExpression;set { _CronExpression = value; NotifyPropertyChanged(); } }
        private string _CronExpression;
        public Type JobType { get => _JobType; set { _JobType = value; NotifyPropertyChanged(); } }
        private Type _JobType;


        public string NextFireTime { get => _NextFireTime; set { _NextFireTime = value; NotifyPropertyChanged(); } }
        private string _NextFireTime;
        public string PreviousFireTime { get => _PreviousFireTime; set { _PreviousFireTime = value; NotifyPropertyChanged(); } }
        private string _PreviousFireTime;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; NotifyPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;
    }
}
