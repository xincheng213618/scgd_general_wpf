using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Dao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.PostProcess.Smu
{
    [PostProcess("源表关闭", "关闭源表")]
    public class SmuClosePostProcessor : IPostProcessor
    {
        private readonly Func<IEnumerable<DeviceSMU>> devices;
        private readonly TimeSpan confirmationDelay;
        private readonly TimeSpan replyTimeout;

        public SmuClosePostProcessor() : this(
            () => ServiceManager.GetInstance().DeviceServices.OfType<DeviceSMU>(), TimeSpan.FromMilliseconds(500)) { }

        internal SmuClosePostProcessor(Func<IEnumerable<DeviceSMU>> devices, TimeSpan confirmationDelay, TimeSpan? replyTimeout = null)
        {
            this.devices = devices;
            this.confirmationDelay = confirmationDelay;
            this.replyTimeout = replyTimeout ?? TimeSpan.FromSeconds(30);
        }

        public bool Process(PostProcessContext ctx) => ctx?.Batch == null ? false
            : throw new NotSupportedException("源表关闭后处理需要等待设备回执，请使用 ProcessAsync。");

        public async ValueTask<bool> ProcessAsync(PostProcessContext ctx)
        {
            if (ctx?.Batch == null) return false;

            var closing = Task.WhenAll(devices().ToArray().Select(CloseDeviceOutputAsync));
            try { await closing; }
            catch when (closing.Exception is { InnerExceptions.Count: > 1 } errors)
            {
                throw new AggregateException("多个源表关闭输出失败。", errors.InnerExceptions);
            }
            return true;
        }

        private async Task CloseDeviceOutputAsync(DeviceSMU device)
        {
            string code = device.Code;
            try
            {
                var display = device.DisplayConfig;
                SMUChannelType channel = display.Channel;
                var service = device.DService;
                var target = device.CaptureConfigurationTarget();
                bool OwnsDevice() => !device.IsDisposed && ReferenceEquals(device.DService, service) && target.IsCurrent(device);

                bool local;
                Func<Task> close;
                lock (device.SmuBackend.Sync)
                {
                    if (!OwnsDevice()) throw new InvalidOperationException("原源表已不可用。");
                    local = device.SmuBackend.OpensLocally;
                    close = local ? device.LocalSession.CapturePostProcessCloseOutput(channel, OwnsDevice)
                        : service.CapturePostProcessCloseOutput(channel, confirmationDelay, replyTimeout);
                    if (local) device.SmuBackend.BeginLocalCommand();
                }

                try
                {
                    await close();
                    if (local && OwnsDevice() && ReferenceEquals(device.DisplayConfig, display))
                        device.ClearLocalSmuReadings(channel, all: false);
                }
                finally { if (local) device.SmuBackend.EndLocalCommand(); }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"源表 {code} 关闭输出失败：{ex.Message}", ex);
            }
        }
    }
}
