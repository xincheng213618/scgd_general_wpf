using ColorVision.Common.Utilities;
using ColorVision.Engine.Messages;
using MQTTMessageLib.FileServer;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.RC
{
    public class RCFileUpload: MQTTServiceBase
    {
        private static RCFileUpload _instance;
        private static readonly object _locker = new();
        public static RCFileUpload GetInstance() { lock (_locker) { return _instance ??= new RCFileUpload(); } }

        public MsgRecord CreatePhysicalCameraFloder(string cameraID)
        {
            MsgSend msg = new()
            {
                DeviceCode = cameraID,
                EventName = "PhysicalCamera_Load",
            };
            return PublishAsyncClient(msg); 
        }

        public async Task<MsgRecord> UploadCalibrationFileAsync(string cameraID,string name, string fileName, int timeout = 50000)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            TaskCompletionSource<MsgRecord> tcs = new();
            string md5 = Tool.CalculateMD5(fileName);
            MsgSend msg = new()
            {
                DeviceCode = cameraID,
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

    }
}
