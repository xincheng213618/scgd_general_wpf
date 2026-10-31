using System.Diagnostics;
using ColorVision.UI.ServiceHost;
using log4net;

namespace ColorVision.Update
{
    public static class ApplicationUpdateScanProtection
    {
        public const string ProtectionIdEnvironmentVariable = "COLORVISION_UPDATE_SCAN_PROTECTION_ID";
        private static readonly ILog log = LogManager.GetLogger(typeof(ApplicationUpdateScanProtection));
        private static int _completionStarted;

        public static string? TryBegin(string updateRoot)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                ServiceHostResponse pingResponse = ColorVisionServiceHostClient.Default
                    .PingAsync(TimeSpan.FromSeconds(1))
                    .GetAwaiter()
                    .GetResult();
                if (!pingResponse.Success)
                {
                    stopwatch.Stop();
                    log.Info($"Microsoft Defender update scan protection was unavailable after {stopwatch.ElapsedMilliseconds} ms: {pingResponse.Message}");
                    return null;
                }

                ServiceHostResponse response = ColorVisionServiceHostClient.Default
                    .BeginApplicationUpdateScanProtectionAsync(updateRoot)
                    .GetAwaiter()
                    .GetResult();
                stopwatch.Stop();
                if (!response.Success)
                {
                    log.Info($"Microsoft Defender update scan protection was unavailable after {stopwatch.ElapsedMilliseconds} ms: {response.Message}");
                    return null;
                }

                string? protectionId = response.Data?["protectionId"]?.ToString();
                log.Info(string.IsNullOrWhiteSpace(protectionId)
                    ? $"Update paths already had Microsoft Defender exclusions; checked in {stopwatch.ElapsedMilliseconds} ms."
                    : $"Temporary Microsoft Defender update scan protection enabled in {stopwatch.ElapsedMilliseconds} ms.");
                return string.IsNullOrWhiteSpace(protectionId) ? null : protectionId;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                log.Info($"ColorVisionServiceHost could not enable Microsoft Defender update scan protection after {stopwatch.ElapsedMilliseconds} ms.", ex);
                return null;
            }
        }

        public static void TryComplete(string? protectionId)
        {
            if (string.IsNullOrWhiteSpace(protectionId))
                return;

            try
            {
                ServiceHostResponse response = ColorVisionServiceHostClient.Default
                    .CompleteApplicationUpdateScanProtectionAsync(protectionId)
                    .GetAwaiter()
                    .GetResult();
                if (response.Success)
                    log.Info("Temporary Microsoft Defender update scan protection cleared.");
                else
                    log.Warn($"Microsoft Defender update scan protection cleanup was deferred to the service timeout: {response.Message}");
            }
            catch (Exception ex)
            {
                log.Warn("Microsoft Defender update scan protection cleanup was deferred to the service timeout.", ex);
            }
        }

        public static void CompleteAfterUpdateRestart()
        {
            string? protectionId = Environment.GetEnvironmentVariable(ProtectionIdEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(protectionId)
                || Interlocked.Exchange(ref _completionStarted, 1) != 0)
            {
                return;
            }

            Environment.SetEnvironmentVariable(ProtectionIdEnvironmentVariable, null);
            _ = Task.Run(() => TryComplete(protectionId));
        }
    }
}
