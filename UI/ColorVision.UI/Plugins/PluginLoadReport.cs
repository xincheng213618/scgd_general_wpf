namespace ColorVision.UI.Plugins
{
    public enum PluginLoadStatus
    {
        Loaded,
        Disabled,
        Retired,
        Skipped,
        Failed,
    }

    /// <summary>
    /// Outcome for one plugin directory. <see cref="UserMessage"/> is set only for failures the
    /// host is expected to show the user; other details stay in <see cref="Detail"/> and the log.
    /// </summary>
    public sealed record PluginLoadResult(
        string Id,
        string Directory,
        PluginLoadStatus Status,
        string? Detail = null,
        string? UserMessage = null,
        Exception? Error = null);

    /// <summary>What one PluginLoader pass did; presenting it is the host's decision.</summary>
    public sealed class PluginLoadReport
    {
        public PluginLoadReport(IReadOnlyList<PluginLoadResult> results)
        {
            Results = results;
        }

        public static PluginLoadReport Empty { get; } = new([]);

        public IReadOnlyList<PluginLoadResult> Results { get; }

        public IEnumerable<PluginLoadResult> Failures => Results.Where(result => result.Status == PluginLoadStatus.Failed);

        public bool HasFailures => Failures.Any();

        /// <summary>Failure messages meant for the user, in load order.</summary>
        public IReadOnlyList<string> UserMessages => Failures
            .Select(result => result.UserMessage)
            .OfType<string>()
            .ToArray();
    }
}
