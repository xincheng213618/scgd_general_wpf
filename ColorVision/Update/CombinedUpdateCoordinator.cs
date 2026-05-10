using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Menus;
using log4net;
using System;
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
                AutoUpdatePlan? applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync();
                CombinedPluginUpdatePlan? pluginPlan = null;
                Version? currentVersion = AutoUpdater.CurrentVersion;

                if (applicationPlan == null && currentVersion != null)
                {
                    pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(currentVersion);
                }

                if (applicationPlan == null && (pluginPlan == null || !pluginPlan.HasUpdates))
                {
                    ShowNoUpdatesMessage(pluginPlan);
                    return;
                }

                if (MessageBox1.Show(Application.Current.GetActiveWindow(), BuildStartMessage(applicationPlan, pluginPlan), "ColorVision", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                {
                    return;
                }

                WorkflowConfig.Activate(CombinedUpdateStage.UpdatingApplication);
                ConfigService.Instance.SaveConfigs();

                if (applicationPlan != null)
                {
                    AutoUpdater.GetInstance().StartUpdatePlan(applicationPlan);
                    return;
                }

                await StartPluginUpdateAsync(pluginPlan!, showNoUpdatesMessage: true);
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
                    AutoUpdater.GetInstance().StartUpdatePlan(applicationPlan);
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
                builder.AppendLine($"检测到主体新版本 {applicationPlan.LatestVersion}。程序会先把主体更新到最新版本，过程中可能重启多次。");
            }

            if (pluginPlan?.HasUpdates == true)
            {
                builder.AppendLine($"完成主体更新后，还会继续更新 {pluginPlan.Updates.Count} 个插件。");
            }
            else
            {
                builder.AppendLine("主体完成后会再检查一次插件更新。");
            }

            builder.Append("是否继续？");
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

    public class MenuCombinedUpdate : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuUpdate);

        public override int Order => 101;

        public override string Header => "更新主体和插件";

        public override void Execute() => _ = CombinedUpdateCoordinator.StartInteractiveAsync();
    }
}