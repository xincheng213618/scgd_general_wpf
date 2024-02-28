using System.Collections.Generic;
using ColorVision.Services.Msg;
using System.Windows;
using MQTTMessageLib;
using ColorVision.Services.Devices.Camera.Calibrations;
using MQTTMessageLib.Calibration;
using MQTTMessageLib.FileServer;

namespace ColorVision.Services.Devices.Calibration
{
    public class MQTTCalibration : MQTTDeviceService<ConfigCalibration>
    {
        public event MessageRecvHandler OnMessageRecved;
        public MQTTCalibration(ConfigCalibration config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Unknown;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Calibration":

                        object obj = msg.Data;
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, obj.ToString()));
                        break;
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "Calibration":
                        Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, "校准失败"));
                        break;
                }
            }


        }


        public MsgRecord Calibration(CalibrationParam item,string FilePath,double R,double G,double B)
        {
            Dictionary<string, object> Params = new Dictionary<string, object>();
            MsgSend msg = new MsgSend
            {
                EventName = "Calibration",
                Params = Params
            };

            //if (item.Color.Luminance.IsSelected)
            //{
            //    Params.Add("CalibType", "Luminance");
            //    Params.Add("CalibTypeFileName", item.Color.Luminance.FilePath);
            //}

            //if (item.Color.LumOneColor.IsSelected)
            //{
            //    Params.Add("CalibType", "LumOneColor");
            //    Params.Add("CalibTypeFileName", item.Color.LumOneColor.FilePath);
            //}
            //if (item.Color.LumFourColor.IsSelected)
            //{
            //    Params.Add("CalibType", "LumFourColor");
            //    Params.Add("CalibTypeFileName", item.Color.LumFourColor.FilePath);
            //}
            //if (item.Color.LumMultiColor.IsSelected)
            //{
            //    Params.Add("CalibType", "LumMultiColor");
            //    Params.Add("CalibTypeFileName", item.Color.LumMultiColor.FilePath);
            //}

            //List<Dictionary<string, object>> List = new List<Dictionary<string, object>>
            //{
            //    item.Normal.ToDictionary(),
            //};

            //List[0].Add("EXPOSURE", R);
            //List[1].Add("EXPOSURE", G);
            //List[2].Add("EXPOSURE", B);

            //Params.Add("List", List);

            //Params.Add("SrcFileName", FilePath);
            //Params.Add("fname",DateTime.Now.ToString("yyyyMMddHHmmss") );
            return PublishAsyncClient(msg);
        }
        public void Open(string fileName, FileExtType extType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }
        public MsgRecord CacheClear()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTCalibrationEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

        public void GetRawFiles()
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw } }
            };
            PublishAsyncClient(msg);
        }
    }
}
