using ColorVision.Common.MVVM;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Services.Msg
{
    public class MsgConfig : ViewModelBase
    {
        public static MsgConfig Instance => new MsgConfig();

        private static readonly object _locker = new();

        public MsgConfig()
        {
            var timer = new System.Timers.Timer
            {
                Interval = TimeSpan.FromSeconds(1).TotalMilliseconds,
                AutoReset = true,
            };
            timer.Elapsed += (s, e) =>
            {
                lock (_locker)
                {
                    int itemsToRemoveCount = MsgRecords.Count - CacheLength;
                    if (itemsToRemoveCount > 0)
                        for (int i = 0; i < itemsToRemoveCount; i++)
                            if (MsgRecords.Count > 1)
                                Application.Current.Dispatcher.Invoke(() => MsgRecords.RemoveAt(MsgRecords.Count - 1));
                }
            };
            timer.Start();
        }

        public int CacheLength { get => _CacheLength; set { _CacheLength = value; NotifyPropertyChanged(); } }
        private int _CacheLength = 1000;

        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();
    }
}
