using ColorVision.Common.MVVM;
using ColorVision.Services.Interfaces;
using Newtonsoft.Json;
using System;
using System.Windows;

namespace ColorVision.Services.Msg
{
    public delegate void MsgRecordStateChangedHandler(MsgRecordState msgRecordState);
    public delegate void MsgRecordSucessChangedHandler(MsgReturn msgReturn);

    public class MsgRecord : ViewModelBase, IServiceConfig
    {
        public event MsgRecordStateChangedHandler MsgRecordStateChanged;
        public event MsgRecordSucessChangedHandler? MsgSucessed;
        public void ClearMsgRecordSucessChangedHandler() => MsgSucessed = null;
        public string SubscribeTopic { get; set; }
        public string SendTopic { get; set; }

        public string MsgID { get; set; }
        public DateTime SendTime { get => _SendTime; set { _SendTime = value; NotifyPropertyChanged(); } }
        private DateTime _SendTime;
        public DateTime ReciveTime { get => _ReciveTime; set { _ReciveTime = value; NotifyPropertyChanged(); } }
        private DateTime _ReciveTime;
        public MsgSend MsgSend { get; set; }
        public MsgReturn MsgReturn { get; set; }

        public string ErrorMsg { get; set; }

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
                    if (value == MsgRecordState.Success)
                    {
                        Application.Current.Dispatcher.Invoke(() => MsgSucessed?.Invoke(MsgReturn));
                    }
                    else
                    {
                        MsgSucessed = null;
                    }
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
        public bool IsSend { get => MsgRecordState == MsgRecordState.Sended; }
        [JsonIgnore]
        public bool IsRecive { get => MsgRecordState == MsgRecordState.Success || MsgRecordState == MsgRecordState.Fail; }
        [JsonIgnore]
        public bool IsTimeout { get => MsgRecordState == MsgRecordState.Timeout; }

    }



}
