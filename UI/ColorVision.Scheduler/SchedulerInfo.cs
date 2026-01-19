using ColorVision.Common.MVVM;
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

    // 纯数据对象，不再包含命令
    public class SchedulerInfo: ViewModelBase
    {
        public SchedulerStatus Status { get => _Status; set { _Status = value; OnPropertyChanged(); } }
        private SchedulerStatus _Status;

        public JobStartMode JobStartMode
        {
            get => _JobStartMode; set
            {
                _JobStartMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsDelayed));
            }
        }
        private JobStartMode _JobStartMode = JobStartMode.Immediate;
        public bool IsDelayed { get => _JobStartMode == JobStartMode.Delayed; set { JobStartMode = value ? JobStartMode.Delayed : JobStartMode.Immediate; } }

        // 仅在延迟启动模式下使用
        public TimeSpan Delay { get => _Delay; set { _Delay = value; OnPropertyChanged(); } }
        private TimeSpan _Delay;

        public JobExecutionMode Mode { get => _Mode; set { _Mode = value; 
                OnPropertyChanged(); 
                OnPropertyChanged(nameof(IsSimple));
                OnPropertyChanged(nameof(IsCalendar));
                OnPropertyChanged(nameof(IsJobInterval));
                OnPropertyChanged(nameof(IsCron));
            }
        }
        private JobExecutionMode _Mode = JobExecutionMode.Simple;
        public bool IsSimple => _Mode == JobExecutionMode.Simple;
        public bool IsJobInterval => _Mode == JobExecutionMode.Interval;
        public TimeSpan Interval { get => _Interval; set { _Interval = value; OnPropertyChanged(); } }
        private TimeSpan _Interval;

        public bool IsCron => _Mode == JobExecutionMode.Cron;

        public JobRepeatMode RepeatMode { get => _RepeatMode; set { _RepeatMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsRepeatMultiple)); OnPropertyChanged(nameof(IsRepeatOnce)); } }
        private JobRepeatMode _RepeatMode = JobRepeatMode.Once;

        public bool IsRepeatMultiple => _RepeatMode == JobRepeatMode.Multiple;
        public bool IsRepeatOnce => _RepeatMode == JobRepeatMode.Once;

        public int RepeatCount { get => _RepeatCount; set { _RepeatCount = value; OnPropertyChanged(); } }
        private int _RepeatCount = 1;

        public bool IsCalendar => _Mode == JobExecutionMode.Calendar;

        public int RunCount { get => _RunCount; set { _RunCount = value; OnPropertyChanged(); } }
        private int _RunCount;

        public string JobName { get => _JobName; set { _JobName = value; OnPropertyChanged(); } }
        private string _JobName;
        public string GroupName { get => _GroupName; set { _GroupName = value; OnPropertyChanged(); } }
        private string _GroupName;

        public string CronExpression { get => _CronExpression;set { _CronExpression = value; OnPropertyChanged(); } }
        private string _CronExpression;

        public Type JobType { get => _JobType; set { _JobType = value; OnPropertyChanged(); } }
        private Type _JobType;

        public IJobConfig Config { get => _Config; set { _Config = value; OnPropertyChanged(); } }
        private IJobConfig _Config;

        public string NextFireTime { get => _NextFireTime; set { _NextFireTime = value; OnPropertyChanged(); } }
        private string _NextFireTime;
        public string PreviousFireTime { get => _PreviousFireTime; set { _PreviousFireTime = value; OnPropertyChanged(); } }
        private string _PreviousFireTime;

        public DateTime CreateTime { get => _CreateTime; set { _CreateTime = value; OnPropertyChanged(); } }
        private DateTime _CreateTime = DateTime.Now;

        // Phase 3: 新增功能字段
        
        /// <summary>
        /// 任务优先级 (1-10, 默认5)
        /// </summary>
        public int Priority { get => _Priority; set { _Priority = value; OnPropertyChanged(); } }
        private int _Priority = 5;

        /// <summary>
        /// 超时时间（秒），0表示无超时限制
        /// </summary>
        public int TimeoutSeconds { get => _TimeoutSeconds; set { _TimeoutSeconds = value; OnPropertyChanged(); } }
        private int _TimeoutSeconds = 0;

        /// <summary>
        /// 成功执行次数
        /// </summary>
        public int SuccessCount { get => _SuccessCount; set { _SuccessCount = value; OnPropertyChanged(); } }
        private int _SuccessCount;

        /// <summary>
        /// 失败执行次数
        /// </summary>
        public int FailureCount { get => _FailureCount; set { _FailureCount = value; OnPropertyChanged(); } }
        private int _FailureCount;

        /// <summary>
        /// 最后执行时间（毫秒）
        /// </summary>
        public long LastExecutionTimeMs { get => _LastExecutionTimeMs; set { _LastExecutionTimeMs = value; OnPropertyChanged(); } }
        private long _LastExecutionTimeMs;

        /// <summary>
        /// 平均执行时间（毫秒）
        /// </summary>
        public long AverageExecutionTimeMs { get => _AverageExecutionTimeMs; set { _AverageExecutionTimeMs = value; OnPropertyChanged(); } }
        private long _AverageExecutionTimeMs;

        /// <summary>
        /// 最长执行时间（毫秒）
        /// </summary>
        public long MaxExecutionTimeMs { get => _MaxExecutionTimeMs; set { _MaxExecutionTimeMs = value; OnPropertyChanged(); } }
        private long _MaxExecutionTimeMs;

        /// <summary>
        /// 最短执行时间（毫秒）
        /// </summary>
        public long MinExecutionTimeMs { get => _MinExecutionTimeMs; set { _MinExecutionTimeMs = value; OnPropertyChanged(); } }
        private long _MinExecutionTimeMs;
    }
}
