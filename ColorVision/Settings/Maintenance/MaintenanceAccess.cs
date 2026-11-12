using ColorVision.UI.Authorizations;

namespace ColorVision.Settings.Maintenance;

internal static class MaintenanceActions
{
    internal const string OpenDatabase = "maintenance.database.open";
    internal const string OpenSnapshots = "maintenance.snapshots.open";
    internal const string BackupSettings = "maintenance.settings.backup";
    internal const string OpenBackups = "maintenance.settings.backups.open";
    internal const string ResetSettings = "maintenance.settings.reset";
    internal const string CancelReset = "maintenance.settings.cancel-reset";
    internal const string OpenRecovery = "maintenance.recovery.open";
    internal const string OpenSetup = "maintenance.setup.open";
}

internal static class MaintenanceAccess
{
    internal const string ResourceId = "application";

    internal static AccessDecision Evaluate(string actionId) => AccessControl.Evaluate(actionId, PermissionMode.Administrator, ResourceId);

    internal static void Demand(string actionId) => AccessControl.Demand(actionId, PermissionMode.Administrator, ResourceId);
}
