using ColorVision.Common.Utilities;
using ColorVision.Services.Devices.Calibration.Templates;
using ColorVision.Services.Msg;
using MQTTMessageLib;
using MQTTMessageLib.Calibration;
using MQTTMessageLib.Camera;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

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
            if (msg.DeviceCode != Config.Code) return;
            if (msg.Code == 0)
            {
                switch (msg.EventName)
                {
                    case "Calibration":

                        //object obj = msg.Data;
                        //Application.Current.Dispatcher.BeginInvoke(() => MessageBox.Show(Application.Current.MainWindow, obj.ToString()));
                        //break;
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
                    default:
                        OnMessageRecved?.Invoke(this, new MessageRecvArgs(msg.EventName, msg.SerialNumber, msg.Code, msg.Data));
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

            MsgSend msg = new MsgSend
            {
                EventName = MQTTCalibrationEventEnum.Event_GetData,
                SerialNumber = sn,
                Params = Params
            };
            return PublishAsyncClient(msg);
        }

        public async Task<MsgRecord> UploadCalibrationFileAsync(string name, string fileName, int fileType, int timeout = 50000)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start(); // 开始计时

            TaskCompletionSource<MsgRecord> tcs = new TaskCompletionSource<MsgRecord>();
            string md5 = Tool.CalculateMD5(fileName);
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_Upload,
                Params = new Dictionary<string, object> { { "Name", name }, { "FileName", fileName }, { "FileExtType", FileExtType.Calibration }, { "MD5", md5 } }
            };
            MsgRecord msgRecord = PublishAsyncClient(msg);

            MsgRecordStateChangedHandler handler = (sender) =>
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  状态{sender}  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetResult(msgRecord);
            };
            msgRecord.MsgRecordStateChanged += handler;
            var timeoutTask = Task.Delay(timeout);
            try
            {

                var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                if (completedTask == timeoutTask)
                {
                    log.Info($"UploadCalibrationFileAsync:{fileName}  超时  Operation time: {stopwatch.ElapsedMilliseconds} ms");
                    tcs.TrySetException(new TimeoutException("The operation has timed out."));
                }
                return await tcs.Task; // 如果超时，这里将会抛出异常
            }
            catch (Exception ex)
            {
                log.Info($"UploadCalibrationFileAsync:{fileName}  异常 {ex.Message} Operation time: {stopwatch.ElapsedMilliseconds} ms");
                tcs.TrySetException(ex);
                return await tcs.Task; // 
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= handler;
            }
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

        public MsgRecord GetChannel(int recId, CVImageChannelType chType)
        {
            MsgSend msg = new MsgSend
            {
                EventName = MQTTFileServerEventEnum.Event_File_GetChannel,
                Params = new Dictionary<string, object> { { "RecID", recId }, { "ChannelType", chType } }
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
