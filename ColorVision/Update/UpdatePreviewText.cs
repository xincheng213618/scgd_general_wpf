using System;
using System.Globalization;
using Resources = ColorVision.Properties.Resources;

namespace ColorVision.Update
{
    public static class UpdatePreviewText
    {
        private static string Format(string template, params object[] args)
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }

        public static string WindowTitle => Resources.CheckForUpdates;
        public static string CheckingHeading => Resources.UpdatePreviewCheckingHeading;
        public static string CheckingSummary => Resources.UpdatePreviewCheckingSummary;
        public static string ScanningTitle => Resources.UpdatePreviewScanningTitle;
        public static string NoUpdatesTitle => Resources.UpdatePreviewNoUpdatesTitle;
        public static string NoUpdatesMessage => Resources.UpdatePreviewNoUpdatesMessage;
        public static string FoundUpdatesHeading => Resources.UpdatePreviewFoundUpdatesHeading;
        public static string AlreadyLatestHeading => Resources.UpdatePreviewAlreadyLatestHeading;
        public static string UpdateNowButtonText => Resources.UpdatePreviewUpdateNowButtonText;
        public static string UpdatingButtonText => Resources.UpdatePreviewUpdatingButtonText;
        public static string LaterButtonText => Resources.UpdatePreviewLaterButtonText;
        public static string CancelButtonText => Resources.UpdatePreviewCancelButtonText;
        public static string CloseButtonText => Resources.UpdatePreviewCloseButtonText;
        public static string SkipVersionButtonText => Resources.UpdatePreviewSkipVersionButtonText;
        public static string RequiredBadge => Resources.UpdatePreviewRequiredBadge;
        public static string UnknownVersion => Resources.UpdatePreviewUnknownVersion;
        public static string ApplicationUpdateCategory => Resources.UpdatePreviewApplicationUpdateCategory;
        public static string ApplicationIncrementalCategory => Resources.UpdatePreviewApplicationIncrementalCategory;
        public static string PluginUpdateCategory => Resources.UpdatePreviewPluginUpdateCategory;
        public static string ApplicationFullPackageLabel => Resources.UpdatePreviewApplicationFullPackageLabel;
        public static string UpdateKindApplication => Resources.UpdatePreviewUpdateKindApplication;
        public static string UpdateKindPlugin => Resources.UpdatePreviewUpdateKindPlugin;
        public static string NoInstallableUpdatesMessage => Resources.UpdatePreviewNoInstallableUpdatesMessage;
        public static string PackageDownloadFailed => Resources.UpdatePreviewPackageDownloadFailed;
        public static string PluginDownloadFailed => Resources.UpdatePreviewPluginDownloadFailed;
        public static string NoPluginUpdates => Resources.UpdatePreviewNoPluginUpdates;
        public static string CombinedPackageIncomplete => Resources.UpdatePreviewCombinedPackageIncomplete;
        public static string ShowNoUpdatesLatestMessage => Resources.UpdatePreviewShowNoUpdatesLatestMessage;
        public static string DialogSummaryNoUpdates => Resources.UpdatePreviewDialogSummaryNoUpdates;
        public static string ApplicationCardSummaryFull => Resources.UpdatePreviewApplicationCardSummaryFull;
        public static string SelectionIncludesApplication => Resources.UpdatePreviewSelectionIncludesApplication;
        public static string SelectionIncludesRequired => Resources.UpdatePreviewSelectionIncludesRequired;
        public static string SelectionRestartRequired => Resources.UpdatePreviewSelectionRestartRequired;
        public static string SelectionBackupAndRestart => Resources.UpdatePreviewSelectionBackupAndRestart;
        public static string HostVersionLabel => Resources.UpdatePreviewHostVersionLabel;

        public static string ListSeparator
        {
            get
            {
                string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                return language == "zh" || language == "ja" ? "、" : ", ";
            }
        }

        public static string HostRequirement(string value) => Format(Resources.UpdatePreviewHostRequirementFormat, value);
        public static string SkippedIncompatibleUpdates(string value) => Format(Resources.UpdatePreviewSkippedIncompatibleUpdatesFormat, value);
        public static string ApplicationIncrementalPackages(int count) => Format(Resources.UpdatePreviewApplicationIncrementalPackagesFormat, count);
        public static string ApplicationCardSummaryIncremental(int count) => Format(Resources.UpdatePreviewApplicationCardSummaryIncrementalFormat, count);
        public static string DialogSummaryWithKinds(int count, string kinds) => Format(Resources.UpdatePreviewDialogSummaryWithKinds, count, kinds);
        public static string DialogSummaryDefault(int count) => Format(Resources.UpdatePreviewDialogSummaryDefault, count);
        public static string DialogSummarySkippedCount(int count) => Format(Resources.UpdatePreviewDialogSummarySkippedCount, count);
        public static string HeaderApplicationCount(int count) => Format(Resources.UpdatePreviewHeaderApplicationCount, count);
        public static string HeaderPluginCount(int count) => Format(Resources.UpdatePreviewHeaderPluginCount, count);
        public static string HeaderThemeCount(int count) => Format(Resources.UpdatePreviewHeaderThemeCount, count);
        public static string HeaderOtherCount(int count) => Format(Resources.UpdatePreviewHeaderOtherCount, count);
        public static string SelectionSelectedPlugins(int selected, int total) => Format(Resources.UpdatePreviewSelectionSelectedPluginsFormat, selected, total);
        public static string SelectionSelectedUpdates(int selected, int total) => Format(Resources.UpdatePreviewSelectionSelectedUpdatesFormat, selected, total);
        public static string ShowNoUpdatesSkippedMessage(string value) => Format(Resources.UpdatePreviewShowNoUpdatesSkippedMessage, value);
    }
}