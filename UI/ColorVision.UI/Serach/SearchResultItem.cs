using System.Text.RegularExpressions;

namespace ColorVision.UI.Serach;

/// <summary>A display snapshot with the original command retained for execution on its owning UI context.</summary>
public sealed class SearchResultItem
{
    public SearchResultItem(ISearch source, string providerId, string shortcutText = "")
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        ProviderId = providerId;
        ISearchMetadata? metadata = source as ISearchMetadata;
        Title = source.Type == SearchType.Menu ? CleanTitle(source.Header ?? string.Empty) : (source.Header ?? string.Empty).Trim();
        ActionId = metadata?.ActionId ?? string.Empty;
        CategoryKey = !string.IsNullOrWhiteSpace(metadata?.CategoryKey) ? metadata.CategoryKey : source.Type switch
        {
            SearchType.File => "Templates",
            SearchType.ThirdPartyApp => "Tools",
            SearchType.Link => "External",
            _ => "Commands",
        };
        Category = !string.IsNullOrWhiteSpace(metadata?.Category) ? metadata.Category : SearchPaletteText.Get("Category" + CategoryKey);
        Description = !string.IsNullOrWhiteSpace(metadata?.Description) ? metadata.Description : SearchPaletteText.Get(CategoryKey switch
        {
            "Templates" => "DescriptionTemplate",
            "Tools" => "DescriptionTool",
            "External" => "DescriptionExternalSearch",
            _ => "DescriptionCommand",
        });
        Aliases = metadata?.Aliases?.Where(alias => !string.IsNullOrWhiteSpace(alias)).ToArray() ?? [];
        ShortcutText = shortcutText;
        StableId = !string.IsNullOrWhiteSpace(ActionId) ? "action:" + ActionId
            : !string.IsNullOrWhiteSpace(source.GuidId) ? $"{source.Type}:{source.GuidId}"
            : $"{providerId}:{source.Type}:{Title}";
    }

    public ISearch Source { get; }
    public string ProviderId { get; }
    public string StableId { get; }
    public string Title { get; }
    public string Description { get; }
    public string CategoryKey { get; }
    public string Category { get; }
    public IReadOnlyList<string> Aliases { get; }
    public string ActionId { get; }
    public string ShortcutText { get; }

    internal static string CleanTitle(string value)
    {
        string title = Regex.Replace(value, @"\s*[（(][&_][A-Za-z0-9][）)]\s*$", string.Empty);
        return Regex.Replace(title, "__(?=.)|_(?=.)", match => match.Value.Length == 2 ? "_" : string.Empty).Trim();
    }
}

public sealed record SearchQueryResult(IReadOnlyList<SearchResultItem> Items, IReadOnlyList<string> FailedSources, bool IsTruncated);
