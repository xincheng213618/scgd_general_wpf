using log4net;
using ProjectARVRPro.Process;

namespace ProjectARVRPro.Services
{
    public class PictureSwitchService : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(PictureSwitchService));
        private readonly ThunderbirdSerialController _thunderbirdController;

        public PictureSwitchService(ThunderbirdSerialController thunderbirdController)
        {
            _thunderbirdController = thunderbirdController;
        }

        public async Task<bool> ExecuteAsync(ProcessMeta? meta)
        {
            PictureSwitchConfig? config = meta?.PictureSwitchConfig;
            if (config == null || !config.IsEnabled)
                return true;

            if (config.Mode != PictureSwitchMode.Thunderbird)
            {
                log.Error($"不支持的切图模式: {config.Mode}");
                return false;
            }

            if (!_thunderbirdController.IsConnected && !await TryAutoConnectThunderbirdAsync())
            {
                log.Error($"流程 {meta?.Name} 已启用雷鸟切图，但雷鸟串口未连接");
                return false;
            }

            try
            {
                int timeoutMs = config.TimeoutMs > 0 ? config.TimeoutMs : 1000;
                ThunderbirdSerialController.CommandResult result = await _thunderbirdController.SendConfiguredCommandAsync(config.SendCommand, config.ExpectedResponse, timeoutMs);
                if (!result.Success)
                {
                    log.Error($"流程 {meta?.Name} 雷鸟切图失败: Command={result.Command}, Expected={config.ExpectedResponse}, Response={result.Response ?? "<null>"}");
                    return false;
                }

                if (config.SuccessDelayMs > 0)
                {
                    log.Info($"流程 {meta?.Name} 雷鸟切图成功，等待图像稳定 {config.SuccessDelayMs}ms");
                    await Task.Delay(config.SuccessDelayMs);
                }

                log.Info($"流程 {meta?.Name} 雷鸟切图完成，执行流程");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"流程 {meta?.Name} 雷鸟切图异常", ex);
                return false;
            }
        }

        private async Task<bool> TryAutoConnectThunderbirdAsync()
        {
            if (_thunderbirdController.IsConnected)
                return true;

            if (!ProjectARVRProConfig.Instance.ThunderbirdAutoConnect)
                return false;

            if (string.IsNullOrWhiteSpace(ProjectARVRProConfig.Instance.ThunderbirdPortName))
            {
                log.Warn("雷鸟自动连接已启用，但未配置串口号");
                return false;
            }

            try
            {
                int timeoutMs = ProjectARVRProConfig.Instance.ThunderbirdTimeoutMs > 0 ? ProjectARVRProConfig.Instance.ThunderbirdTimeoutMs : 1000;
                _thunderbirdController.Open(ProjectARVRProConfig.Instance.ThunderbirdPortName, ProjectARVRProConfig.Instance.ThunderbirdBaudRate, timeoutMs);
                log.Info($"雷鸟自动连接成功: {ProjectARVRProConfig.Instance.ThunderbirdPortName}");
                await _thunderbirdController.QueryBrightnessAsync(timeoutMs);
                return true;
            }
            catch (Exception ex)
            {
                log.Warn("雷鸟自动连接失败", ex);
                return false;
            }
        }

        public void Dispose()
        {
            _thunderbirdController.Close();
            GC.SuppressFinalize(this);
        }
    }
}
