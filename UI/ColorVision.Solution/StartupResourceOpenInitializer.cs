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
            ArgumentParseResult parsedArguments = parser.ParseSnapshot(parser.CommandLineArgs);
            CommandLineResourceOpenRequest request = CommandLineResourceOpenRequest.Create(
                parsedArguments,
                parser.GetValue("solutionpath"));
            if (request.ResourcePaths.Count == 0)
                return;

            await SolutionManager.GetInstance().InitialWorkspaceOpenTask;
            await ResourceOpenService.Instance.TryOpenManyWithFeedbackAsync(request.ResourcePaths);
        }
    }
}
