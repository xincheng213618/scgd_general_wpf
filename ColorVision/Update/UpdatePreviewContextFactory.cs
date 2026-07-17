#pragma warning disable CA1863
using ColorVision.UI.Desktop.Marketplace;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Resources = ColorVision.Properties.Resources;

namespace ColorVision.Update
{
    internal static class UpdatePreviewContextFactory
    {
        public static UpdatePreviewDialogContext CreateCheckingContext()
        {
            return new UpdatePreviewDialogContext
            {
                Heading = Resources.UpdatePreviewCheckingHeading,
                Summary = Resources.UpdatePreviewCheckingSummary,
                CheckingTitle = Resources.UpdatePreviewScanningTitle,
                CheckingSummary = Resources.UpdatePreviewCheckingSummary,
                StateGlyph = "\uE895",
                HostVersionValue = AutoUpdater.CurrentVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = Resources.UpdatePreviewCancelButtonText,
                IsChecking = true,
            };
        }

        public static UpdatePreviewDialogContext CreateNoUpdatesContext(CombinedPluginUpdatePlan? pluginPlan)
        {
            string listSeparator = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" ? "、" : ", ";
            string emptyMessage = pluginPlan?.SkippedIncompatiblePlugins.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewSkippedIncompatibleUpdatesFormat, string.Join(listSeparator, pluginPlan.SkippedIncompatiblePlugins))
                : Resources.UpdatePreviewNoInstallableUpdatesMessage;

            return new UpdatePreviewDialogContext
            {
                Heading = Resources.UpdatePreviewAlreadyLatestHeading,
                Summary = Resources.UpdatePreviewDialogSummaryNoUpdates,
                CheckingTitle = Resources.UpdatePreviewScanningTitle,
                CheckingSummary = Resources.UpdatePreviewCheckingSummary,
                EmptyStateTitle = Resources.UpdatePreviewAlreadyLatestHeading,
                EmptyStateMessage = emptyMessage,
                StateGlyph = "\uE73E",
                HostVersionValue = AutoUpdater.CurrentVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = Resources.UpdatePreviewCloseButtonText,
                IsChecking = false,
            };
        }

        public static UpdatePreviewDialogContext Build(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool isStartupCheck)
        {
            UpdatePreviewDialogContext context = new()
            {
                Heading = BuildPreviewHeading(applicationPlan, pluginPlan),
                Summary = BuildDialogSummary(applicationPlan, pluginPlan),
                HostVersionValue = (applicationPlan?.CurrentVersion ?? AutoUpdater.CurrentVersion)?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = isStartupCheck ? Resources.UpdatePreviewLaterButtonText : Resources.UpdatePreviewCancelButtonText,
            };

            AddApplicationItem(context, applicationPlan);
            AddPluginItems(context, pluginPlan);
            return context;
        }

        private static void AddApplicationItem(UpdatePreviewDialogContext context, AutoUpdatePlan? applicationPlan)
        {
            if (applicationPlan == null)
                return;

            string incrementalSummary = string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewApplicationCardSummaryIncrementalFormat, applicationPlan.VersionsToApply.Count);
            string fullSummary = Resources.UpdatePreviewApplicationCardSummaryFull;
            UpdatePreviewItem previewItem = new()
            {
                ItemId = "application",
                Kind = applicationPlan.IsIncremental ? UpdatePreviewItemKind.ApplicationIncremental : UpdatePreviewItemKind.Application,
                Category = applicationPlan.IsIncremental ? Resources.UpdatePreviewApplicationIncrementalCategory : Resources.UpdatePreviewApplicationUpdateCategory,
                Name = "ColorVision",
                SecondaryLabel = applicationPlan.IsIncremental
                    ? string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewApplicationIncrementalPackagesFormat, applicationPlan.VersionsToApply.Count)
                    : Resources.UpdatePreviewApplicationFullPackageLabel,
                CurrentVersion = applicationPlan.CurrentVersion.ToString(),
                TargetVersion = applicationPlan.LatestVersion.ToString(),
                Summary = applicationPlan.IsIncremental ? incrementalSummary : fullSummary,
                IsSelectable = false,
                CanChooseApplicationUpdateMode = applicationPlan.IsIncremental,
                ApplicationUpdateMode = applicationPlan.IsIncremental ? ApplicationUpdateMode.Incremental : ApplicationUpdateMode.Full,
            };

            previewItem.ConfigureApplicationUpdateModePresentation(applicationPlan.VersionsToApply.Count, incrementalSummary, fullSummary);
            context.Items.Add(previewItem);
        }

        private static void AddPluginItems(UpdatePreviewDialogContext context, CombinedPluginUpdatePlan? pluginPlan)
        {
            if (pluginPlan?.HasUpdates != true)
                return;

            foreach (CombinedPluginUpdateItem item in pluginPlan.Updates)
            {
                string pluginName = GetPluginDisplayName(item);
                context.Items.Add(new UpdatePreviewItem
                {
                    ItemId = GetPluginItemId(item),
                    Kind = UpdatePreviewItemKind.Plugin,
                    Category = Resources.UpdatePreviewPluginUpdateCategory,
                    Name = pluginName,
                    SecondaryLabel = BuildPluginSecondaryLabel(item, pluginName),
                    CurrentVersion = item.Plugin.AssemblyVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                    TargetVersion = item.VersionInfo.Version,
                    HostRequirement = item.VersionInfo.RequiresVersion ?? string.Empty,
                    Summary = BuildPluginCardSummary(item),
                    IsSelectable = true,
                    IsSelected = true,
                });
            }
        }

        private static string BuildDialogSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            int updateCount = (applicationPlan != null ? 1 : 0) + (pluginPlan?.Updates.Count ?? 0);
            if (updateCount == 0)
                return Resources.UpdatePreviewDialogSummaryNoUpdates;

            List<string> updateKinds = BuildUpdateKinds(applicationPlan, pluginPlan);
            StringBuilder builder = new();
            string listSeparator = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" ? "、" : ", ";

            builder.Append(updateKinds.Count > 1
                ? string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummaryWithKinds, updateCount, string.Join(listSeparator, updateKinds))
                : string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummaryDefault, updateCount));

            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
                builder.Append($" {string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummarySkippedCount, pluginPlan.SkippedIncompatiblePlugins.Count)}");

            return builder.ToString();
        }

        private static string BuildPluginCardSummary(CombinedPluginUpdateItem item)
        {
            string? note = !string.IsNullOrWhiteSpace(item.VersionInfo.ChangeLog)
                ? item.VersionInfo.ChangeLog
                : item.Plugin.PluginInfo?.ChangeLog;
            note = string.IsNullOrWhiteSpace(note) ? item.Plugin.Description : note;
            return NormalizeUpdateSummary(note);
        }

        private static string BuildPreviewHeading(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            return applicationPlan != null || pluginPlan?.HasUpdates == true
                ? Resources.UpdatePreviewFoundUpdatesHeading
                : Resources.CheckForUpdates;
        }

        private static List<string> BuildUpdateKinds(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            List<string> kinds = new();
            if (applicationPlan != null)
                kinds.Add(Resources.UpdatePreviewUpdateKindApplication);
            if (pluginPlan?.HasUpdates == true)
                kinds.Add(Resources.UpdatePreviewUpdateKindPlugin);
            return kinds;
        }

        private static string BuildPluginSecondaryLabel(CombinedPluginUpdateItem item, string pluginName)
        {
            string[] candidates = [item.Plugin.PackageName ?? string.Empty, item.Plugin.AssemblyName ?? string.Empty];
            return candidates
                .Select(candidate => candidate.Trim())
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)
                    && !string.Equals(candidate, pluginName, StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
        }

        private static string NormalizeUpdateSummary(string? text)
        {
            string fallback = Resources.UpdateCompatibilityStability;
            const int maxLength = 160;
            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            List<string> lines = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim()
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith('#'))
                .Where(line => !line.Equals("CHANGELOG", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("Changelog", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (lines.Count == 0)
                return fallback;

            List<string> paragraph = new();
            foreach (string line in lines)
            {
                if (paragraph.Count > 0 && IsLikelyParagraphBoundary(line))
                    break;
                paragraph.Add(line.TrimStart('-', '*', ' '));
            }

            string result = string.Join(" ", paragraph).Trim();
            if (string.IsNullOrWhiteSpace(result))
                return fallback;
            return result.Length > maxLength ? result[..maxLength].TrimEnd() + "…" : result;
        }

        private static bool IsLikelyParagraphBoundary(string line)
        {
            return string.IsNullOrWhiteSpace(line)
                || line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal)
                || line.StartsWith("##", StringComparison.Ordinal)
                || line.StartsWith("###", StringComparison.Ordinal);
        }

        private static string GetPluginDisplayName(CombinedPluginUpdateItem item)
        {
            return item.Plugin.Name
                ?? item.Plugin.PluginInfo?.Name
                ?? item.Plugin.PackageName
                ?? item.Plugin.AssemblyName
                ?? Resources.UpdateUnnamedPlugin;
        }

        public static string GetPluginItemId(CombinedPluginUpdateItem item)
        {
            return item.Plugin.PackageName
                ?? item.Plugin.AssemblyName
                ?? GetPluginDisplayName(item);
        }
    }
}
