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
        Stopped
    }

    public class SchedulerInfo: ViewModelBase
    {
        public RelayCommand StopCommand { get; set; }

        public RelayCommand RemoveCommand { get; set; }
        public RelayCommand RunCommand { get; set; }

        public SchedulerInfo()
        {
            StopCommand = new RelayCommand(ExecuteStopCommand,a => Status == SchedulerStatus.Running);
            RemoveCommand = new RelayCommand(ExecuteRemoveCommand ,a => Status != SchedulerStatus.Stopped);
        }
        private async void ExecuteStopCommand(object parameter)
        {
            var schedulerManager = QuartzSchedulerManager.GetInstance();
            await schedulerManager.StopJob(JobName, GroupName);
            Status = SchedulerStatus.Stopped;
        }
        private async void ExecuteRemoveCommand(object parameter)
        {
            var schedulerManager = QuartzSchedulerManager.GetInstance();
            await schedulerManager.RemoveJob(JobName, GroupName);
            Status = SchedulerStatus.Ready;
        }

        public SchedulerStatus Status { get; set; } = SchedulerStatus.Running;

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
