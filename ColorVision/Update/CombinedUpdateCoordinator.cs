using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Marketplace;
using log4net;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public static class CombinedUpdateCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CombinedUpdateCoordinator));
        private static readonly SemaphoreSlim _locker = new(1, 1);

        private static CombinedUpdateWorkflowConfig WorkflowConfig => CombinedUpdateWorkflowConfig.Instance;

        public static async Task StartInteractiveAsync()
        {
            await _locker.WaitAsync();
            try
            {
                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildUpdatePlansAsync(
                    includeApplicationUpdates: true,
                    includePluginUpdates: true,
                    respectSkippedVersion: false);

                if (!HasUpdates(applicationPlan, pluginPlan))
                {
                    ShowNoUpdatesMessage(pluginPlan);
                    return;
                }

                if (MessageBox1.Show(Application.Current.GetActiveWindow(), BuildStartMessage(applicationPlan, pluginPlan), "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }

                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: true);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                WorkflowConfig.Clear();
                ConfigService.Instance.SaveConfigs();
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _locker.Release();
            }
        }

        public static async Task CheckForUpdatesOnStartupAsync()
        {
            if (Debugger.IsAttached)
                return;

            if (WorkflowConfig.IsActive)
                return;

            bool includeApplicationUpdates = AutoUpdateConfig.Instance.IsAutoUpdate;
            bool includePluginUpdates = MarketplaceWindowConfig.Instance.IsAutoUpdate;

            if (!includeApplicationUpdates && !includePluginUpdates)
                return;

            await _locker.WaitAsync();
            try
            {
                if (WorkflowConfig.IsActive)
                    return;

                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildUpdatePlansAsync(
                    includeApplicationUpdates,
                    includePluginUpdates,
                    respectSkippedVersion: true);

                if (!HasUpdates(applicationPlan, pluginPlan))
                    return;

                MessageBoxButton buttons = applicationPlan != null ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo;
                MessageBoxResult result = MessageBox1.Show(
                    Application.Current.GetActiveWindow(),
                    BuildStartMessage(applicationPlan, pluginPlan),
                    "ColorVision",
                    buttons);

                if (applicationPlan != null)
                {
                    if (result == MessageBoxResult.No)
                    {
                        AutoUpdateConfig.Instance.SkippedVersion = applicationPlan.LatestVersion.ToString();
                        ConfigService.Instance.SaveConfigs();
                        return;
                    }

                    if (result != MessageBoxResult.Yes)
                        return;
                }
                else if (result != MessageBoxResult.Yes)
                {
                    return;
                }

                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                _locker.Release();
            }
        }

        public static async Task ResumeIfNeededAsync()
        {
            if (!WorkflowConfig.IsActive)
                return;

            await _locker.WaitAsync();
            try
            {
                if (!WorkflowConfig.IsActive)
                    return;

                if (WorkflowConfig.Stage == CombinedUpdateStage.UpdatingPlugins)
                {
                    WorkflowConfig.Clear();
                    ConfigService.Instance.SaveConfigs();
                    return;
                }

                AutoUpdatePlan? applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync();
                if (applicationPlan != null)
                {
                    WorkflowConfig.Stage = CombinedUpdateStage.UpdatingApplication;
                    ConfigService.Instance.SaveConfigs();
                    AutoUpdater.GetInstance().StartUpdatePlan(applicationPlan, ClearWorkflowState);
                    return;
                }

                if (!WorkflowConfig.UpdatePluginsAfterApplication)
                {
                    WorkflowConfig.Clear();
                    ConfigService.Instance.SaveConfigs();
                    return;
                }

                Version? currentVersion = AutoUpdater.CurrentVersion;
                if (currentVersion == null)
                {
                    WorkflowConfig.Clear();
                    ConfigService.Instance.SaveConfigs();
                    return;
                }

                CombinedPluginUpdatePlan pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(currentVersion);
                await StartPluginUpdateAsync(pluginPlan, showNoUpdatesMessage: false);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                _locker.Release();
            }
        }

        private static async Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildUpdatePlansAsync(bool includeApplicationUpdates, bool includePluginUpdates, bool respectSkippedVersion)
        {
            AutoUpdatePlan? applicationPlan = null;
            if (includeApplicationUpdates)
            {
                applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync();
                if (respectSkippedVersion && ShouldSkipApplicationPlan(applicationPlan))
                {
                    applicationPlan = null;
                }
            }

            CombinedPluginUpdatePlan? pluginPlan = null;
            if (includePluginUpdates)
            {
                Version? hostVersion = applicationPlan?.LatestVersion ?? AutoUpdater.CurrentVersion;
                if (hostVersion != null)
                {
                    pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(hostVersion);
                }
            }

            return (applicationPlan, pluginPlan);
        }

        private static async Task StartWorkflowAsync(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool showNoUpdatesMessage)
        {
            if (!HasUpdates(applicationPlan, pluginPlan))
            {
                if (showNoUpdatesMessage)
                {
                    ShowNoUpdatesMessage(pluginPlan);
                }
                return;
            }

            WorkflowConfig.Activate(CombinedUpdateStage.UpdatingApplication);
            ConfigService.Instance.SaveConfigs();

            if (applicationPlan != null)
            {
                AutoUpdater.GetInstance().StartUpdatePlan(applicationPlan, ClearWorkflowState);
                return;
            }

            await StartPluginUpdateAsync(pluginPlan!, showNoUpdatesMessage);
        }

        private static bool HasUpdates(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            return applicationPlan != null || pluginPlan?.HasUpdates == true;
        }

        private static bool ShouldSkipApplicationPlan(AutoUpdatePlan? applicationPlan)
        {
            if (applicationPlan == null || string.IsNullOrWhiteSpace(AutoUpdateConfig.Instance.SkippedVersion))
                return false;

            if (!Version.TryParse(AutoUpdateConfig.Instance.SkippedVersion.Trim(), out Version? skippedVersion))
            {
                AutoUpdateConfig.Instance.SkippedVersion = string.Empty;
                return false;
            }

            return skippedVersion == applicationPlan.LatestVersion;
        }

        private static void ClearWorkflowState()
        {
            WorkflowConfig.Clear();
            ConfigService.Instance.SaveConfigs();
        }

        private static async Task StartPluginUpdateAsync(CombinedPluginUpdatePlan pluginPlan, bool showNoUpdatesMessage)
        {
            if (!pluginPlan.HasUpdates)
            {
                WorkflowConfig.Clear();
                ConfigService.Instance.SaveConfigs();
                if (showNoUpdatesMessage)
                {
                    ShowNoUpdatesMessage(pluginPlan);
                }
                return;
            }

            WorkflowConfig.Stage = CombinedUpdateStage.UpdatingPlugins;
            ConfigService.Instance.SaveConfigs();

            bool started = MarketplaceManager.GetInstance().StartCombinedUpdate(
                pluginPlan,
                restartArguments: null,
                noRestartAction: () =>
                {
                    WorkflowConfig.Clear();
                    ConfigService.Instance.SaveConfigs();
                    if (showNoUpdatesMessage)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "插件下载未成功完成，请稍后重试。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                });

            if (!started)
            {
                WorkflowConfig.Clear();
                ConfigService.Instance.SaveConfigs();
            }

            await Task.CompletedTask;
        }

        private static string BuildStartMessage(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            StringBuilder builder = new();
            if (applicationPlan != null)
            {
                if (applicationPlan.IsIncremental && applicationPlan.HasMultipleSteps)
                {
                    builder.AppendLine($"检测到主体新版本 {applicationPlan.LatestVersion}。程序会先下载 {applicationPlan.VersionsToApply.Count} 个主体增量包并顺序应用，主体阶段只重启一次。");
                }
                else if (applicationPlan.IsIncremental)
                {
                    builder.AppendLine($"检测到主体新版本 {applicationPlan.LatestVersion}。程序会先应用 1 个主体增量包。");
                }
                else
                {
                    builder.AppendLine($"检测到主体新版本 {applicationPlan.LatestVersion}。程序会先更新主体到最新版本。");
                }
            }

            if (pluginPlan?.HasUpdates == true)
            {
                builder.AppendLine($"随后还会继续更新 {pluginPlan.Updates.Count} 个插件。");
            }

            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                string skippedPlugins = string.Join("、", pluginPlan.SkippedIncompatiblePlugins);
                builder.AppendLine($"以下插件会因兼容性要求被跳过：{skippedPlugins}");
            }

            builder.Append(pluginPlan?.HasUpdates == true && applicationPlan == null ? "是否继续更新插件？" : "是否继续？");
            return builder.ToString();
        }

        private static void ShowNoUpdatesMessage(CombinedPluginUpdatePlan? pluginPlan)
        {
            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                string skippedPlugins = string.Join("、", pluginPlan.SkippedIncompatiblePlugins);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"当前没有可执行的联合更新。以下插件因兼容性要求被跳过：{skippedPlugins}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox1.Show(Application.Current.GetActiveWindow(), "当前主体和插件都已经是最新版本。", "ColorVision", MessageBoxButton.OK);
        }
    }

    public class CombinedUpdateInitializer : MainWindowInitializedBase
    {
        public override int Order { get => 0; set { } }

        public override Task Initialize() => CombinedUpdateCoordinator.ResumeIfNeededAsync();
    }
}