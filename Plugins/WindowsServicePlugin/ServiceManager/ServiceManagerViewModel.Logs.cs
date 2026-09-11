using System.Windows;
using WindowsServicePlugin.Properties;

namespace WindowsServicePlugin.ServiceManager;

public partial class ServiceManagerViewModel
{
    public async Task<bool> ClearAllServiceLogsAsync()
    {
        List<ServiceEntry> targets = Services
            .Where(entry => ServiceLogCleanup.ResolveLogDirectory(entry, Config.BaseLocation) != null)
            .ToList();

        if (targets.Count == 0)
        {
            ShowUiMessage("没有找到可清理的服务日志目录。", Resources.ClearServiceLogs, MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        RefreshAll();
        int runningCount = targets.Count(entry => entry.IsInstalled && entry.IsRunning);
        string restartNotice = runningCount > 0
            ? $"其中 {runningCount} 个服务正在运行，清理期间会短暂停止并在完成后恢复。"
            : "当前目标服务均未运行。";
        MessageBoxResult confirm = ShowUiMessage(
            $"将清空 {targets.Count} 个 CVWindowsService 服务目录下的全部日志文件。\n{restartNotice}\n\n是否继续？",
            Resources.ClearAllServiceLogs,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return false;

        SetBusy(true, "正在清空全部服务日志...");
        try
        {
            ServiceLogMaintenanceResult result = await ClearServiceLogsBatchAsync(targets).ConfigureAwait(true);
            SetProgress(100, "服务日志清理完成");
            ShowCleanupResult(Resources.ClearAllServiceLogs, result.DeletedFileCount, result.DeletedBytes, result.Failures);
            return true;
        }
        catch (Exception ex)
        {
            log.Error("清空全部服务日志失败", ex);
            ShowUiMessage($"清空服务日志失败：{ex.Message}", Resources.ClearAllServiceLogs, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            RefreshAll();
            SetBusy(false);
        }
    }

    private async Task ClearServiceLogsAsync(ServiceEntry? entry)
    {
        if (entry == null)
            return;

        entry.RefreshStatus();
        ServiceEntry? registrationCenter = Services.FirstOrDefault(service => service.ServiceName == "RegistrationCenterService");
        bool pausesRegistrationCenter = entry.ServiceName != "RegistrationCenterService" && entry.IsInstalled && entry.IsRunning && registrationCenter is { IsInstalled: true, IsRunning: true };
        string restartNotice = entry.IsInstalled && entry.IsRunning
            ? "该服务正在运行，清理期间会短暂停止并在完成后恢复。"
            : "该服务当前未运行。";
        if (pausesRegistrationCenter)
            restartNotice += " 为避免服务被自动拉起，注册中心也会短暂停止并恢复。";
        MessageBoxResult confirm = ShowUiMessage(
            $"将清空“{entry.DisplayName}”目录下的全部日志文件。\n{restartNotice}\n\n是否继续？",
            Resources.ClearServiceLogs,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
            return;

        SetBusy(true, $"正在清理 {entry.DisplayName} 日志...");
        try
        {
            ServiceLogMaintenanceResult result = await ClearServiceLogsBatchAsync([entry]).ConfigureAwait(true);
            ShowCleanupResult(Resources.ClearServiceLogs, result.DeletedFileCount, result.DeletedBytes, result.Failures);
        }
        catch (Exception ex)
        {
            log.Error($"清空 {entry.DisplayName} 日志失败", ex);
            ShowUiMessage($"清空服务日志失败：{ex.Message}", Resources.ClearServiceLogs, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RefreshAll();
            SetBusy(false);
        }
    }

    private async Task<ServiceLogMaintenanceResult> ClearServiceLogsBatchAsync(IReadOnlyList<ServiceEntry> targets)
    {
        List<string> failures = [];
        List<ServiceEntry> cleanupTargets = targets
            .DistinctBy(entry => entry.ServiceName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (ServiceEntry entry in Services)
            entry.RefreshStatus();

        ServiceEntry? registrationCenter = Services.FirstOrDefault(entry => entry.ServiceName == "RegistrationCenterService");
        List<ServiceEntry> controlledServices = cleanupTargets.ToList();
        if (cleanupTargets.Any(entry => entry.ServiceName != "RegistrationCenterService" && entry.IsInstalled && entry.IsRunning) &&
            registrationCenter is { IsInstalled: true, IsRunning: true } &&
            controlledServices.All(entry => entry.ServiceName != "RegistrationCenterService"))
        {
            controlledServices.Add(registrationCenter);
        }

        Dictionary<string, bool> originallyRunning = controlledServices.ToDictionary(
            entry => entry.ServiceName,
            entry => entry.IsInstalled && entry.IsRunning,
            StringComparer.OrdinalIgnoreCase);
        HashSet<string> stoppedServices = new(StringComparer.OrdinalIgnoreCase);
        List<ServiceEntry> stopOrder = controlledServices
            .OrderBy(entry => entry.ServiceName == "RegistrationCenterService" ? 0 : 1)
            .ToList();

        int deletedFileCount = 0;
        long deletedBytes = 0;
        try
        {
            foreach (ServiceEntry entry in stopOrder)
            {
                if (!originallyRunning[entry.ServiceName])
                    continue;

                bool stopped = await ServiceHostWindowsServiceController.ExecuteAsync(
                    entry.ServiceName,
                    ServiceHostServiceOperation.Stop,
                    log.Info,
                    entry.DisplayName,
                    entry.ExePath).ConfigureAwait(true);
                if (stopped)
                    stoppedServices.Add(entry.ServiceName);
                else
                    failures.Add($"{entry.DisplayName}: 停止服务失败，未清理日志");
            }

            bool registrationCenterCouldNotStop = registrationCenter != null &&
                originallyRunning.TryGetValue(registrationCenter.ServiceName, out bool registrationCenterWasRunning) &&
                registrationCenterWasRunning &&
                !stoppedServices.Contains(registrationCenter.ServiceName);

            for (int index = 0; index < cleanupTargets.Count; index++)
            {
                ServiceEntry entry = cleanupTargets[index];
                SetProgress((index * 100d) / cleanupTargets.Count, $"正在清理 {entry.DisplayName} 日志...");
                bool targetCouldNotStop = originallyRunning.TryGetValue(entry.ServiceName, out bool targetWasRunning) &&
                    targetWasRunning &&
                    !stoppedServices.Contains(entry.ServiceName);
                if (targetCouldNotStop || (registrationCenterCouldNotStop && entry.ServiceName != "RegistrationCenterService" && targetWasRunning))
                    continue;

                string? logDirectory = ServiceLogCleanup.ResolveLogDirectory(entry, Config.BaseLocation);
                if (string.IsNullOrWhiteSpace(logDirectory))
                {
                    failures.Add($"{entry.DisplayName}: 无法确定日志目录");
                    continue;
                }

                ServiceLogCleanupResult cleanupResult = await Task.Run(() => ServiceLogCleanup.Clear(logDirectory)).ConfigureAwait(true);
                deletedFileCount += cleanupResult.DeletedFileCount;
                deletedBytes += cleanupResult.DeletedBytes;
                failures.AddRange(cleanupResult.Failures.Select(message => $"{entry.DisplayName}: {message}"));
            }
        }
        finally
        {
            foreach (ServiceEntry entry in controlledServices.OrderBy(entry => entry.ServiceName == "RegistrationCenterService" ? 1 : 0))
            {
                if (!originallyRunning[entry.ServiceName] || !stoppedServices.Contains(entry.ServiceName))
                    continue;

                bool started = await ServiceHostWindowsServiceController.ExecuteAsync(
                    entry.ServiceName,
                    ServiceHostServiceOperation.Start,
                    log.Info,
                    entry.DisplayName,
                    entry.ExePath).ConfigureAwait(true);
                if (!started)
                    failures.Add($"{entry.DisplayName}: 日志已处理，但服务恢复启动失败，请立即检查服务状态");
            }
        }

        return new ServiceLogMaintenanceResult(deletedFileCount, deletedBytes, failures);
    }

    private static void ShowCleanupResult(string caption, int deletedFileCount, long deletedBytes, IReadOnlyList<string> failures)
    {
        string sizeText = FormatFileSize(deletedBytes);
        if (failures.Count == 0)
        {
            ShowUiMessage($"已清空 {deletedFileCount} 个日志文件，共 {sizeText}。", caption, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string details = string.Join("\n", failures.Take(5));
        if (failures.Count > 5)
            details += $"\n另有 {failures.Count - 5} 项失败。";

        ShowUiMessage(
            $"已清空 {deletedFileCount} 个日志文件，共 {sizeText}；{failures.Count} 项未完成。\n\n{details}",
            caption,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024d:0.##} KB";
        if (bytes < 1024L * 1024 * 1024)
            return $"{bytes / (1024d * 1024):0.##} MB";
        return $"{bytes / (1024d * 1024 * 1024):0.##} GB";
    }

    private sealed record ServiceLogMaintenanceResult(int DeletedFileCount, long DeletedBytes, IReadOnlyList<string> Failures);
}
