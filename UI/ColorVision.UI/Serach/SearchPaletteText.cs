using System.Globalization;
using System.Resources;

namespace ColorVision.UI.Serach;

public static class SearchPaletteText
{
    private static readonly ResourceManager Resources = new("ColorVision.UI.Serach.SearchPaletteResources", typeof(SearchPaletteText).Assembly);
    public static string Get(string key) => Resources.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    public static string Title => Get(nameof(Title));
    public static string Placeholder => Get(nameof(Placeholder));
    public static string Close => Get(nameof(Close));
    public static string Clear => Get(nameof(Clear));
    public static string Filter => Get(nameof(Filter));
    public static string AllCategories => Get(nameof(AllCategories));
    public static string KeyboardHint => Get(nameof(KeyboardHint));
    public static string Settings => Get(nameof(Settings));
    public static string NoResults => Get(nameof(NoResults));
    public static string NoResultsHelp => Get(nameof(NoResultsHelp));
}
