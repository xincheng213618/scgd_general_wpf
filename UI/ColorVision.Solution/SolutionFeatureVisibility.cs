using ColorVision.Solution.Explorer;
using System.Windows;

namespace ColorVision.Solution
{
    /// <summary>
    /// Keeps implemented but not yet productized features out of the current UI.
    /// Change this single switch when the build and debug workflow is ready to surface.
    /// </summary>
    internal static class SolutionFeatureVisibility
    {
        public static bool ShowBuildAndDebugUI => false;
        public static Visibility BuildAndDebugMenuVisibility => ShowBuildAndDebugUI
            ? Visibility.Visible
            : Visibility.Collapsed;

        public static bool IsBuildOrDebugCapability(string capabilityId)
        {
            return string.Equals(capabilityId, ProjectCapabilityIds.Build, StringComparison.OrdinalIgnoreCase)
                || string.Equals(capabilityId, ProjectCapabilityIds.Run, StringComparison.OrdinalIgnoreCase)
                || string.Equals(capabilityId, ProjectCapabilityIds.Debug, StringComparison.OrdinalIgnoreCase);
        }
    }
}
