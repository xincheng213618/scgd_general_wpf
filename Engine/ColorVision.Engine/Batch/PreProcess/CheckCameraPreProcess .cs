using ColorVision.Engine.Messages;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using cvColorVision;
using FlowEngineLib;
using FlowEngineLib.Node.Camera;
using log4net;
using System;
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
                if (deviceCamera.DService.IsVideoOpen)
                {
                    deviceCamera.CameraVideoControl.Close();
                    MsgRecord msgRecord = deviceCamera.DService.Close();
                    // 创建 TaskCompletionSource 用于等待事件
                    var tcs = new TaskCompletionSource<bool>();

                    // 定义事件处理程序

                    void MsgRecord_MsgRecordStateChanged(object? sender ,MsgRecordState e)
                    {
                        if (e == MsgRecordState.Success)
                        {
                            log.Info($"关闭相机{deviceCamera.Name}成功");
                            tcs.TrySetResult(true);
                        }
                        else if (e == MsgRecordState.Fail)
                        {
                            log.Info($"关闭相机{deviceCamera.Name}失败");
                            tcs.TrySetResult(false);
                        }
                    }

                    // 订阅事件
                    msgRecord.MsgRecordStateChanged += MsgRecord_MsgRecordStateChanged;

                    try
                    {
                        // 等待任务完成或超时（例如 5000 毫秒）
                        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(5000));

                        if (completedTask == tcs.Task)
                        {
                            // 任务正常完成
                            await tcs.Task;
                        }
                        else
                        {
                            // 超时处理
                            log.Warn($"关闭相机{deviceCamera.Name}超时");
                            MessageBox.Show($"关闭相机{deviceCamera.Name}超时");
                            return false;
                        }
                    }
                    finally
                    {
                        // 务必取消订阅，防止内存泄漏
                        msgRecord.MsgRecordStateChanged -= MsgRecord_MsgRecordStateChanged;
                    }
                    deviceCamera.Config.TakeImageMode = TakeImageMode.Measure_Normal;
                    MsgRecord msgRecord1 = deviceCamera.DService.Open(deviceCamera.Config.CameraID, deviceCamera.Config.TakeImageMode, (int)deviceCamera.Config.ImageBpp);
                    // 创建 TaskCompletionSource 用于等待事件
                    var tcs1 = new TaskCompletionSource<bool>();

                    // 定义事件处理程序

                    void MsgRecord_MsgRecordStateChanged1(object? sender, MsgRecordState e)
                    {
                        if (e == MsgRecordState.Success)
                        {
                            log.Info($"打开相机{deviceCamera.Name}成功");
                            tcs.TrySetResult(true);
                        }
                        else if (e == MsgRecordState.Fail)
                        {
                            log.Info($"打开相机{deviceCamera.Name}失败");
                            tcs.TrySetResult(false);
                        }
                    }

                    // 订阅事件
                    msgRecord1.MsgRecordStateChanged += MsgRecord_MsgRecordStateChanged1;

                    try
                    {
                        // 等待任务完成或超时（例如 5000 毫秒）
                        var completedTask = await Task.WhenAny(tcs1.Task, Task.Delay(5000));

                        if (completedTask == tcs1.Task)
                        {
                            // 任务正常完成
                            await tcs.Task;
                            return true;
                        }
                        else
                        {
                            // 超时处理
                            log.Warn($"关闭相机{deviceCamera.Name}超时");
                            MessageBox.Show($"关闭相机{deviceCamera.Name}超时");
                            return false;
                        }
                    }
                    finally
                    {
                        // 务必取消订阅，防止内存泄漏
                        msgRecord1.MsgRecordStateChanged -= MsgRecord_MsgRecordStateChanged1;
                    }
                }
            }

            return true;
        }
    }
}
