using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Scheduler;
using Quartz;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.Devices.Camera.Job
{

    [DisplayName("相机拍摄任务")]
    public class CameraCaptureJob : IJob, IConfigurableJob
    {
        public System.Type ConfigType => typeof(CameraCaptureJobConfig);

        public IJobConfig CreateDefaultConfig()
        {  
            var config = new CameraCaptureJobConfig();
            // Set default to first available device
            var firstDevice = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>().FirstOrDefault();
            if (firstDevice != null)
            {
                config.DeviceCameraName = firstDevice.Config.Name;
            }
            return config;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);

            // 定时任务逻辑
            DeviceCamera deviceCamera = null;

            if (schedulerInfo.Config is CameraCaptureJobConfig config && !string.IsNullOrEmpty(config.DeviceCameraName))
            {
                deviceCamera = ServiceManager.GetInstance().DeviceServices.OfType<DeviceCamera>()
                    .FirstOrDefault(d => d.Config.Name == config.DeviceCameraName);
            }
            if (deviceCamera == null)
            {
                // 记录错误或抛出异常，让 Listener 捕获失败
                throw new JobExecutionException("未找到可用的相机设备");
            }

            // 使用 TaskCompletionSource 来实现异步等待
            var tcs = new TaskCompletionSource<bool>();

            // 在 UI 线程触发拍摄（如果 GetData 需要 UI 线程）
            // 注意：如果 deviceCamera 内部已经处理了线程问题，这里可以直接调用。
            // 假设 GetData 返回的 MsgRecord 已经开始工作
            MsgRecord msgRecord = null;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                msgRecord = deviceCamera.DisplayCameraControlLazy.Value.GetData();
            });

            if (msgRecord == null)
            {
                throw new JobExecutionException("相机 GetData 返回空");
            }

            // 定义事件处理函数，方便后续解绑
            void Handler(MsgRecordState state)
            {
                // 判断结束状态：成功、失败、或者其他终止状态
                if (state == MsgRecordState.Success || state == MsgRecordState.Fail || state == MsgRecordState.Timeout)
                {
                    // 解绑事件防止内存泄漏
                    msgRecord.MsgRecordStateChanged -= Handler;

                    if (state == MsgRecordState.Success)
                    {
                        tcs.TrySetResult(true);
                    }
                    else
                    {
                        // 任务失败，抛出异常让 TaskCompletionSource 知道
                        tcs.TrySetException(new JobExecutionException($"相机采集失败，状态: {state}"));
                    }
                }
            }

            // 订阅状态变更
            msgRecord.MsgRecordStateChanged += Handler;

            // 处理已经完成的情况（防止订阅前就已经完成了）
            // 这里假设 MsgRecord 有一个 CurrentState 属性或者类似的机制判断是否已经完成
            // 如果没有，上面的 Handler 逻辑足以处理后续变化，但如果有 Timeout 设置，建议增加超时等待

            // 设置超时时间，例如 30 秒（可配置）
            int timeoutSeconds = schedulerInfo.TimeoutSeconds > 0 ? schedulerInfo.TimeoutSeconds : 30;
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));

            // 等待任务完成或超时
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                msgRecord.MsgRecordStateChanged -= Handler; // 超时解绑
                throw new JobExecutionException($"相机采集超时 ({timeoutSeconds}s)");
            }

            await tcs.Task;
        }
    }
}

