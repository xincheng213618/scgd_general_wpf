using ColorVision.Engine.Media;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    internal sealed record LumFourColorReplacementResult(string BackupPath, Exception? RestartError)
    {
        public string Message => RestartError == null
            ? "已替换当前文件，服务重启完成"
            : $"文件已替换，服务重启失败：{RestartError.Message}。请检查服务后手动重启。";
    }

    internal static class LumFourColorCalibrationReplacement
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);

        public static async Task<LumFourColorReplacementResult> ReplaceAndRestartAsync(
            LumFourColorSourceSnapshot source, CVRawManualCieConfig corrected,
            Func<Task> restartServices)
        {
            ArgumentNullException.ThrowIfNull(restartServices);
            if (!Gate.Wait(0)) throw new InvalidOperationException("已有校正文件正在替换并重启服务，请稍后再试。");
            try
            {
                // A write/backup failure must never interrupt services. A restart failure must not disguise a completed write.
                string backup = source.ReplaceOriginal(corrected);
                try
                {
                    await restartServices();
                    return new(backup, null);
                }
                catch (Exception ex)
                {
                    return new(backup, ex);
                }
            }
            finally { Gate.Release(); }
        }
    }
}
