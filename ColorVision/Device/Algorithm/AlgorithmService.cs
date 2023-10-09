#pragma warning disable CS8602  

using ColorVision.MQTT;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Windows;

namespace ColorVision.Device.Algorithm
{
    public class AlgorithmService : BaseService<AlgorithmConfig>
    {
        public event DeviceStatusChangedHandler DeviceStatusChanged;

        public DeviceStatus DeviceStatus { get => _DeviceStatus; set { if (value == _DeviceStatus) return;  _DeviceStatus = value; Application.Current.Dispatcher.Invoke(() => DeviceStatusChanged?.Invoke(value)); NotifyPropertyChanged(); } }
        private DeviceStatus _DeviceStatus;

        public static Dictionary<string, ObservableCollection<string>> ServicesDevices { get; set; } = new Dictionary<string, ObservableCollection<string>>();

        public string DeviceID { get => Config.ID; }

        public AlgorithmService(AlgorithmConfig Config) : base(Config)
        {
            MsgReturnReceived += MQTTCamera_MsgReturnChanged;
            DeviceStatus = DeviceStatus.UnInit;
        }

        private void MQTTCamera_MsgReturnChanged(MsgReturn msg)
        {
            IsRun = false;
            switch (msg.EventName)
            {
                case "CM_GetAllSnID":
                    try
                    {
                        SnID = msg.Data.SnID[0];
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled)
                            log.Error(ex);
                    }
                    return;
            }


            //信息在这里添加一次过滤，让信息只能在对应的相机上显示,同时如果ID为空的话，就默认是服务端的信息，不进行过滤，这里后续在进行优化
            if (Config.ID != null && msg.SnID != Config.ID)
            {
                return;
            }

            if (msg.Code == 0)
            {

                switch (msg.EventName)
                {
                    case "Init":
                        ServiceID = msg.ServiceID;
                        DeviceStatus = DeviceStatus.Init;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "SetParam":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.Closed;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "GetData":
                        DeviceStatus = DeviceStatus.Opened;
                        break;
                    case "SaveLicense":
                        break;
                    default:
                        MessageBox.Show($"未定义{msg.EventName}");
                        break;
                }
            }
            else
            {
                MessageBox.Show("返回失败" + Environment.NewLine + msg);
                switch (msg.EventName)
                {
                    case "GetData":
                        break;
                    case "Close":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Open":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Init":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "UnInit":
                        DeviceStatus = DeviceStatus.UnInit;
                        break;
                    case "Calibrations":
                        break;
                    default:
                        break;
                }
            }
        }

        public bool IsRun { get; set; }
        public MsgRecord Init()
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Init",
                Params = new Dictionary<string, object>() {{ "SnID", SnID } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord UnInit()
        {
            MsgSend msg = new MsgSend  {  EventName = "UnInit"};
            return PublishAsyncClient(msg);
        }


        public MsgRecord GetData(int pid,int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "GetData",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            //Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid },{ "eCalibType", (int)eCalibType }, { "szFileNameX", "X.tif " }, { "szFileNameY", "Y.tif " }, { "szFileNameZ", "Z.tif " } }
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetAllSnID() => PublishAsyncClient(new MsgSend { EventName = "CM_GetAllSnID" });


        public MsgRecord SetLicense(string md5, string FileData)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SaveLicense",
                Params = new Dictionary<string, object>() { { "FileName", md5 }, { "FileData", FileData }, { "eType", 0 }}
            };
            return PublishAsyncClient(msg); 
        }

        public MsgRecord FOV(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend 
            { 
                EventName = "FOV",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }
        public MsgRecord MTF(int pid, int Batchid, CalibrationType eCalibType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "MTF",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid }  }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SFR(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord SFR(int pid, int Batchid,IRECT ROI )
        {
            MsgSend msg = new MsgSend
            {
                EventName = "SFR",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } ,{"x",ROI.x }, { "x", ROI.x }, { "y", ROI.y },{ "cx", ROI.cx }, { "cy", ROI.cy } }
            };
            return PublishAsyncClient(msg);
        }



        public MsgRecord Ghost(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Ghost",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }



        public MsgRecord Distortion(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "Distortion",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord FocusPoints(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "FocusPoints",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord LedCheck(int pid, int Batchid)
        {
            MsgSend msg = new MsgSend
            {
                EventName = "LedCheck",
                Params = new Dictionary<string, object>() { { "SnID", SnID }, { "nPid", pid }, { "nBatch", Batchid } }
            };
            return PublishAsyncClient(msg);
        }


        public MsgRecord Close()
        {
            MsgSend msg = new MsgSend {  EventName = "Close" };
            return PublishAsyncClient(msg);
        }

    }



}
