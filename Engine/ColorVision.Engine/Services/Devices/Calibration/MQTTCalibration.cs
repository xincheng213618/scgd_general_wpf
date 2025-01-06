using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.PhyCameras.Group;
using CVCommCore;
using CVCommCore.CVImage;
using MQTTMessageLib;
using MQTTMessageLib.Calibration;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.Calibration
{
    public class MQTTCalibration : MQTTDeviceService<ConfigCalibration>
    {
        public MQTTCalibration(ConfigCalibration config) : base(config)
        {
            MsgReturnReceived += ProcessingReceived;
            DeviceStatus = DeviceStatusType.Unknown;
        }

        private void ProcessingReceived(MsgReturn msg)
        {
            if (msg.DeviceCode != Config.Code) return;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Calibration":
                    default:
                        break;
                }
            }
            else
            {
                switch (msg.EventName)
                {
                    case "Calibration":
                        //Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Appli`2cation.Current.GetActiveWindow(), "校准失败"));
                        break;
                    default:
                        break;
                }
            }


        }


        public MsgRecord Calibration(CalibrationParam item, string fileName, FileExtType fileExtType, int pid, string tempName, string serialNumber, float R, float G, float B)
        {
            string sn = null;
            if (string.IsNullOrWhiteSpace(serialNumber)) sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            else sn = serialNumber;
            var Params = new Dictionary<string, object>() { { "ImgFileName", fileName }, { "FileType", fileExtType }, };
            Params.Add("TemplateParam", new CVTemplateParam() { ID = pid, Name = tempName });
            Params.Add("DeviceParam", new DeviceParamCalibration() { exp = new float[] { R, G, B }, gain = 1, });

            MsgSend msg = new()
            {
                EventName = MQTTCalibrationEventEnum.Event_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }


        public void Open(string fileName, FileExtType extType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_Download,
                ServiceName = Config.Code,
                Params = new Dictionary<string, object> { { "FileName", fileName }, { "FileExtType", extType } }
            };
            PublishAsyncClient(msg);
        }
        public MsgRecord CacheClear()
        {
            MsgSend msg = new()
            {
                EventName = MQTTCalibrationEventEnum.Event_Delete_Data,
                Params = new Dictionary<string, object> { }
            };
            return PublishAsyncClient(msg);
        }

        public MsgRecord GetChannel(int recId, CVImageChannelType chType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_GetChannel,
                Params = new Dictionary<string, object> { { "RecID", recId }, { "ChannelType", chType } }
            };
            return PublishAsyncClient(msg);
        }
        public void GetRawFiles(string deviceCode, string deviceType)
        {
            MsgSend msg = new()
            {
                EventName = MQTTFileServerEventEnum.Event_File_List_All,
                Params = new Dictionary<string, object> { { "FileExtType", FileExtType.Raw }, { "DeviceCode", deviceCode }, { "DeviceType", deviceType } }
            };
            PublishAsyncClient(msg);
        }
    }
}
