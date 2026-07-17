namespace ColorVision.Solution.Mru
{
    public sealed record MruPathEntry(
        string Path,
        DateTimeOffset LastUsedUtc,
        bool IsPinned = false);
}
