#pragma warning disable CA1309
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Node.Camera;
using log4net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Batch.PreProcess
{
    public class CheckCameraPreProcessConfig : PreProcessConfigBase
    {

    }

    [PreProcess("相机检测", "相机如果是视频模式或者是未打开，会提示用户")]
    public class CheckCameraPreProcess : PreProcessBase<CheckCameraPreProcessConfig>
    {
        public static readonly ILog log = LogManager.GetLogger(typeof(CheckCameraPreProcess));
        public override async Task<bool> PreProcess(IPreProcessContext ctx)
        {
            if (ctx.CVBaseServerNodes == null) return true;
            if (ctx.CVBaseServerNodes.Count ==0) return true;
            List<string> list = new List<string>();
            ctx.CVBaseServerNodes.OfType<CommCameraNode>().ToList().ForEach(node =>
            {
                if (!list.Contains(node.DeviceCode))
                    list.Add(node.DeviceCode);
            });
            ctx.CVBaseServerNodes.OfType<LVCameraNode>().ToList().ForEach(node =>
            {
                if (!list.Contains(node.DeviceCode))
                    list.Add(node.DeviceCode);
            });
            ctx.CVBaseServerNodes.OfType<CVCameraNode>().ToList().ForEach(node =>
            {
                if (!list.Contains(node.DeviceCode))
                    list.Add(node.DeviceCode);
            });

            foreach (var item in list)
            {
                log.Info(item);
                DeviceCamera? deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().ToList().Where(x => x.Code.Equals(item)).FirstOrDefault();
                if (deviceCamera == null)
                {
                    log.Warn($"未找到相机设备 {item}");
                    continue;
                }

                if (deviceCamera.DService.IsVideoOpen)
                {
                    deviceCamera.CameraVideoControl.Close();
                    MsgRecord msgRecord = deviceCamera.DService.Close();
                    if (!await WaitForMsgRecordAsync(msgRecord, deviceCamera.Name, "关闭"))
                    {
                        return false;
                    }

                    deviceCamera.Config.TakeImageMode = TakeImageMode.Measure_Normal;
                    MsgRecord msgRecord1 = deviceCamera.DService.Open(deviceCamera.Config.CameraID, deviceCamera.Config.TakeImageMode, (int)deviceCamera.Config.ImageBpp);
                    if (!await WaitForMsgRecordAsync(msgRecord1, deviceCamera.Name, "打开"))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static async Task<bool> WaitForMsgRecordAsync(MsgRecord msgRecord, string cameraName, string actionName)
        {
            var tcs = new TaskCompletionSource<bool>();

            void MsgRecord_MsgRecordStateChanged(object? sender, MsgRecordState e)
            {
                if (e == MsgRecordState.Success)
                {
                    log.Info($"{actionName}相机{cameraName}成功");
                    tcs.TrySetResult(true);
                }
                else if (e == MsgRecordState.Fail)
                {
                    log.Info($"{actionName}相机{cameraName}失败");
                    tcs.TrySetResult(false);
                }
            }

            msgRecord.MsgRecordStateChanged += MsgRecord_MsgRecordStateChanged;

            try
            {
                var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                if (completedTask == tcs.Task)
                {
                    return await tcs.Task;
                }

                log.Warn($"{actionName}相机{cameraName}超时");
                MessageBox.Show($"{actionName}相机{cameraName}超时");
                return false;
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= MsgRecord_MsgRecordStateChanged;
            }
        }
    }
}
