﻿using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System.ComponentModel;

namespace ColorVision.Scheduler
{
    public enum SchedulerStatus
    {
        Ready,
        Running,
        Paused,
    }

    public enum JobStartMode
    {
        [Description("立即")]
        Immediate,
        [Description("延时")]
        Delayed
    }

    public enum JobExecutionMode
    {
        [Description("简单")]
        Simple,
        [Description("日历")]
        Calendar,
        [Description("Cron")]
        Cron,
        [Description("间隔")]
        Interval
    }

    public enum JobRepeatMode
    {
        [Description("一次")]
        Once,
        [Description("多次")]
        Multiple,
        [Description("永远")]
        Forever
    }

    public class SchedulerInfo: ViewModelBase
    {
        [JsonIgnore]
        public RelayCommand PausedCommand { get; set; }
        [JsonIgnore]
        public RelayCommand DeleteCommand { get; set; }
        [JsonIgnore]
        public RelayCommand RunCommand { get; set; }
        [JsonIgnore]
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

        public JobStartMode JobStartMode
        {
            get => _JobStartMode; set
            {
                _JobStartMode = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(IsDelayed));
            }
        }
        private JobStartMode _JobStartMode = JobStartMode.Immediate;
        public bool IsDelayed { get => _JobStartMode == JobStartMode.Delayed; set { JobStartMode = value ? JobStartMode.Delayed : JobStartMode.Immediate; } }

        // 仅在延迟启动模式下使用
        public TimeSpan Delay { get => _Delay; set { _Delay = value; NotifyPropertyChanged(); } }
        private TimeSpan _Delay;


        public JobExecutionMode Mode { get => _Mode; set { _Mode = value; 
                NotifyPropertyChanged(); 
                NotifyPropertyChanged(nameof(IsSimple));
                NotifyPropertyChanged(nameof(IsCalendar));
                NotifyPropertyChanged(nameof(IsJobInterval));
                NotifyPropertyChanged(nameof(IsCron));
            }
        }
        private JobExecutionMode _Mode = JobExecutionMode.Simple;
        public bool IsSimple => _Mode == JobExecutionMode.Simple;
        public bool IsJobInterval => _Mode == JobExecutionMode.Interval;
        public TimeSpan Interval { get => _Interval; set { _Interval = value; NotifyPropertyChanged(); } }
        private TimeSpan _Interval;

        public bool IsCron => _Mode == JobExecutionMode.Cron;

        public JobRepeatMode RepeatMode { get => _RepeatMode; set { _RepeatMode = value; NotifyPropertyChanged(); NotifyPropertyChanged(nameof(IsRepeatMultiple)); NotifyPropertyChanged(nameof(IsRepeatOnce)); } }
        private JobRepeatMode _RepeatMode = JobRepeatMode.Once;

        public bool IsRepeatMultiple => _RepeatMode == JobRepeatMode.Multiple;
        public bool IsRepeatOnce => _RepeatMode == JobRepeatMode.Once;

        public int RepeatCount { get => _RepeatCount; set { _RepeatCount = value; NotifyPropertyChanged(); } }
        private int _RepeatCount = 1;

        public bool IsCalendar => _Mode == JobExecutionMode.Calendar;



        public int RunCount { get => _RunCount; set { _RunCount = value; NotifyPropertyChanged(); } }
        private int _RunCount;


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
