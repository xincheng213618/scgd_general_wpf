using ColorVision.Common.MVVM;
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ColorVision.Engine.Messages
{
    public class MsgConfig : ViewModelBase,IConfig
    {
        public static MsgConfig Instance => ConfigService.Instance.GetRequiredService<MsgConfig>();

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

        public int CacheLength { get => _CacheLength; set { _CacheLength = value; OnPropertyChanged(); } }
        private int _CacheLength = 1000;

        [JsonIgnore]
        public ObservableCollection<MsgRecord> MsgRecords { get; set; } = new ObservableCollection<MsgRecord>();
    }
}
