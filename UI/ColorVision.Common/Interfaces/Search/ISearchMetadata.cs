using System.Collections.Generic;

namespace ColorVision.UI
{
    /// <summary>Optional presentation metadata; existing ISearch implementations remain supported.</summary>
    public interface ISearchMetadata
    {
        string? Description { get; }
        string? CategoryKey { get; }
        string? Category { get; }
        IReadOnlyList<string> Aliases { get; }
        string? ActionId { get; }
    }
}
