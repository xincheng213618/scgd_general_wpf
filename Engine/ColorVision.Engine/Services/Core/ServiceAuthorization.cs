using ColorVision.UI.Authorizations;
using System.Globalization;
using System.IO;

namespace ColorVision.Engine.Services;

internal static class ServiceAuthorization
{
    internal static string ResourceId(SysResourceModel resource) => !string.IsNullOrWhiteSpace(resource.Code)
        ? resource.Code : "resource:" + resource.Id.ToString(CultureInfo.InvariantCulture);

    internal static bool Can(string actionId, SysResourceModel resource) =>
        AccessControl.Evaluate(actionId, PermissionMode.Administrator, ResourceId(resource)).Allowed;

    internal static void Demand(string actionId, SysResourceModel resource) =>
        AccessControl.Demand(actionId, PermissionMode.Administrator, ResourceId(resource));

    internal static bool CanWriteCalibrationFile(string? path) => AccessControl.Evaluate("calibration.file.write", PermissionMode.Administrator,
        path == null ? null : "calibration-file:" + Path.GetFullPath(path)).Allowed;

    internal static void DemandCalibrationFileWrite(string path) => AccessControl.Demand("calibration.file.write", PermissionMode.Administrator,
        "calibration-file:" + Path.GetFullPath(path));
}
