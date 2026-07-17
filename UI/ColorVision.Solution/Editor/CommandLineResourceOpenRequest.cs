using ColorVision.UI.Shell;

namespace ColorVision.Solution.Editor
{
    public sealed record CommandLineResourceOpenRequest(
        string? WorkspacePath,
        IReadOnlyList<string> ResourcePaths)
    {
        public static CommandLineResourceOpenRequest Create(
            ArgumentParseResult parsedArguments,
            string? preferredWorkspacePath = null)
        {
            ArgumentNullException.ThrowIfNull(parsedArguments);

            string? workspacePath = preferredWorkspacePath;
            if (string.IsNullOrWhiteSpace(workspacePath)
                && parsedArguments.Values.TryGetValue("solutionpath", out string? explicitWorkspacePath))
            {
                workspacePath = explicitWorkspacePath;
            }

            var resourcePaths = new List<string>();
            if (parsedArguments.Values.TryGetValue("input", out string? inputPath))
                resourcePaths.Add(inputPath);
            resourcePaths.AddRange(parsedArguments.PositionalArguments);
            resourcePaths = resourcePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(ResourcePathIdentityComparer.Instance)
                .ToList();

            if (string.IsNullOrWhiteSpace(workspacePath))
                workspacePath = resourcePaths.FirstOrDefault(SolutionManager.IsSupportedOpenPath);

            if (!string.IsNullOrWhiteSpace(workspacePath))
            {
                resourcePaths.RemoveAll(path =>
                    ResourcePathIdentityComparer.Instance.Equals(path, workspacePath));
            }

            return new CommandLineResourceOpenRequest(workspacePath, resourcePaths);
        }
    }
}
