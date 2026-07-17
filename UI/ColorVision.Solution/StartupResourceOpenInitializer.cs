using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Shell;

namespace ColorVision.Solution
{
    /// <summary>
    /// Opens command-line files only after the main window and the initial
    /// workspace are ready, so every entry point uses the same resource router.
    /// </summary>
    public sealed class StartupResourceOpenInitializer : MainWindowInitializedBase
    {
        public override int Order { get; set; } = 100;

        public override async Task Initialize()
        {
            ArgumentParser parser = ArgumentParser.GetInstance();
            string? inputPath = parser.GetValue("input");
            string? solutionPath = parser.GetValue("solutionpath");
            string? deferredInputPath = GetDeferredInputPath(inputPath, solutionPath);
            if (deferredInputPath == null)
                return;

            await SolutionManager.GetInstance().InitialWorkspaceOpenTask;
            await ResourceOpenService.Instance.TryOpenWithFeedbackAsync(deferredInputPath);
        }

        internal static string? GetDeferredInputPath(
            string? inputPath,
            string? solutionPath)
        {
            if (string.IsNullOrWhiteSpace(inputPath)
                || string.Equals(inputPath, solutionPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return inputPath;
        }
    }
}
