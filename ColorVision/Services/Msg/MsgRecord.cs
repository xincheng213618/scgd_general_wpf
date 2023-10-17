using ColorVision.MQTT;
using ColorVision.MVVM;
using Newtonsoft.Json;
using System;
using System.Windows;

namespace ColorVision.Services.Msg
{
    public delegate void MsgRecordStateChangedHandler(MsgRecordState msgRecordState);

    public class MsgRecord : ViewModelBase, IServiceConfig
    {
        public event MsgRecordStateChangedHandler MsgRecordStateChanged;

        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public string MsgID { get; set; }
        public DateTime SendTime { get => _SendTime; set { _SendTime = value; NotifyPropertyChanged(); } }
        private DateTime _SendTime;
        public DateTime ReciveTime { get => _ReciveTime; set { _ReciveTime = value; NotifyPropertyChanged(); } }
        private DateTime _ReciveTime;
        public MsgSend MsgSend { get; set; }
        public MsgReturn MsgReturn { get; set; }

        public MsgRecordState MsgRecordState
        {
            get => _MsgRecordState; set
            {
                _MsgRecordState = value;
                NotifyPropertyChanged();
                Application.Current.Dispatcher.Invoke(() => MsgRecordStateChanged?.Invoke(MsgRecordState));
                if (value == MsgRecordState.Success || value == MsgRecordState.Fail)
                {
                    NotifyPropertyChanged(nameof(IsRecive));
                    NotifyPropertyChanged(nameof(MsgReturn));
                }
                else if (value == MsgRecordState.Timeout)
                {
                    NotifyPropertyChanged(nameof(IsTimeout));
                }
                else
                {
                    NotifyPropertyChanged(nameof(MsgReturn));
                }

                NotifyPropertyChanged(nameof(IsSend));

            }
        }
        private MsgRecordState _MsgRecordState { get; set; }

        [JsonIgnore]
        public bool IsSend { get => MsgRecordState == MsgRecordState.Send; }
        [JsonIgnore]
        public bool IsRecive { get => MsgRecordState == MsgRecordState.Success || MsgRecordState == MsgRecordState.Fail; }
        [JsonIgnore]
        public bool IsTimeout { get => MsgRecordState == MsgRecordState.Timeout; }

    }



}
