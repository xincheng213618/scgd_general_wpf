namespace ProjectARVRPro;

// A display-only estimate. Capture the content at startup: template IDs can be
// reordered and a template may be edited before its current run completes.
internal readonly record struct FlowRuntimeEstimateKey(string Name, string? Content, string? RuntimeVariant = null);

internal sealed class FlowRuntimeEstimateCache
{
    private readonly Dictionary<string, (string? Content, string? RuntimeVariant, long ElapsedMs)> _completed = new(StringComparer.Ordinal);

    public long GetElapsed(FlowRuntimeEstimateKey key) =>
        !string.IsNullOrEmpty(key.Name)
        && _completed.TryGetValue(key.Name, out var entry)
        && string.Equals(entry.Content, key.Content, StringComparison.Ordinal)
        && string.Equals(entry.RuntimeVariant, key.RuntimeVariant, StringComparison.Ordinal)
            ? entry.ElapsedMs : 0;

    public void RecordCompleted(FlowRuntimeEstimateKey key, long elapsedMs)
    {
        if (!string.IsNullOrEmpty(key.Name) && elapsedMs > 0)
            _completed[key.Name] = (key.Content, key.RuntimeVariant, elapsedMs);
    }
}
