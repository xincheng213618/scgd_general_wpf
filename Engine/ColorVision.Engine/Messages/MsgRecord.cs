#pragma warning disable
using ColorVision.Database;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Windows;

namespace ColorVision.Engine.Messages
{
    public delegate void MsgRecordStateChangedHandler(MsgRecordState msgRecordState);
    public delegate void MsgRecordSucessChangedHandler(MsgReturn msgReturn);

    [SqlSugar.SugarTable("MsgRecord")]
    public class MsgRecord : VPKModel
    {
        public event MsgRecordStateChangedHandler MsgRecordStateChanged;

        public event MsgRecordSucessChangedHandler? MsgSucessed;
        public void ClearMsgRecordSucessChangedHandler() => MsgSucessed = null;

        [SugarColumn(ColumnName = "SubscribeTopic",IsNullable =true)]
        public string SubscribeTopic { get; set; }
        [SugarColumn(ColumnName = "SendTopic", IsNullable = true)]
        public string SendTopic { get; set; }

        [SugarColumn(ColumnName = "MsgID", IsNullable = true)]
        public string MsgID { get; set; }

        [SugarColumn(ColumnName = "SendTime", IsNullable = true)]
        public DateTime SendTime { get => _SendTime; set { _SendTime = value; OnPropertyChanged(); } }
        private DateTime _SendTime;

        [SugarColumn(ColumnName = "ReciveTime", IsNullable = true)]
        public DateTime ReciveTime { get => _ReciveTime; set { _ReciveTime = value; OnPropertyChanged(); } }
        private DateTime _ReciveTime;

        [SugarColumn(IsIgnore = true)]
        public MsgSend MsgSend { get; set; }

        [SugarColumn(ColumnName = "MsgSendJson", IsNullable = true)]
        public string MsgSendJson { get => JsonConvert.SerializeObject(MsgSend); set { if (!string.IsNullOrEmpty(value)) MsgSend = JsonConvert.DeserializeObject<MsgSend>(value); } }

       
        [SugarColumn(IsIgnore = true)]
        public MsgReturn MsgReturn { get; set; }

        [SugarColumn(ColumnName = "MsgReturnJson", IsNullable = true)]
        public string MsgReturnJson { get => JsonConvert.SerializeObject(MsgReturn); set { if (!string.IsNullOrEmpty(value)) MsgReturn = JsonConvert.DeserializeObject<MsgReturn>(value); } }

        [SugarColumn(ColumnName = "ErrorMsg", IsNullable = true)]
        public string ErrorMsg { get; set; }

        [SugarColumn(ColumnName = "MsgRecordState", IsNullable = true)]
        public MsgRecordState MsgRecordState
        {
            get => _MsgRecordState; set
            {
                _MsgRecordState = value;
                OnPropertyChanged();

                Application.Current.Dispatcher.Invoke(() => MsgRecordStateChanged?.Invoke(MsgRecordState));
                if (value == MsgRecordState.Success || value == MsgRecordState.Fail)
                {
                    OnPropertyChanged(nameof(IsRecive));
                    OnPropertyChanged(nameof(MsgReturn));
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
                    OnPropertyChanged(nameof(IsTimeout));
                }
                else
                {
                    OnPropertyChanged(nameof(MsgReturn));
                }

                OnPropertyChanged(nameof(IsSend));

                //UpdateMsgRecordToDbAsync();
            }
        }

        private MsgRecordState _MsgRecordState = MsgRecordState.Initial;

        [SugarColumn(IsIgnore = true)]
        //[JsonIgnore]
        public bool IsSend { get => MsgRecordState == MsgRecordState.Sended; }

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        public bool IsRecive { get => MsgRecordState == MsgRecordState.Success || MsgRecordState == MsgRecordState.Fail; }

        [SugarColumn(IsIgnore = true)]
        [JsonIgnore]
        public bool IsTimeout { get => MsgRecordState == MsgRecordState.Timeout; }

        public DateTime UpdateTime { get; set; } = DateTime.Now;

        public DateTime CreateTime { get; set; } = DateTime.Now;

    }



}
