using ColorVision.UI.Menus;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;

namespace ColorVision.UI.HotKey;

public sealed record HotkeyPresentationInfo(string Name, string Description, string Category, string Source);

/// <summary>Display-only metadata. Reading it never invokes a command, discovers providers, or changes persisted identity.</summary>
public static class HotkeyPresentation
{
    private static readonly ResourceManager Text = new("ColorVision.UI.HotKey.HotkeyPresentationResources", typeof(HotkeyPresentation).Assembly);
    private static readonly Regex AccessKeySuffix = new(@"\s*[（(][&_][A-Za-z0-9][）)]\s*$", RegexOptions.CultureInvariant);

    public static HotkeyPresentationInfo For(HotKeys hotkey)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        return Resolve(hotkey.Id, hotkey.Name, hotkey.DisplayName, hotkey.Description, hotkey.Category, hotkey.Source, hotkey.HotKeyHandler, null);
    }

    public static HotkeyDefinition Enrich(HotkeyDefinition definition, object? provider = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        HotkeyPresentationInfo info = Resolve(definition.Id, definition.Name, definition.DisplayName, definition.Description,
            definition.Category, definition.Source, definition.Handler, provider);
        if (string.IsNullOrWhiteSpace(definition.DisplayName)) definition.DisplayName = info.Name;
        if (string.IsNullOrWhiteSpace(definition.Description)) definition.Description = info.Description;
        if (string.IsNullOrWhiteSpace(definition.Category)) definition.Category = info.Category;
        if (string.IsNullOrWhiteSpace(definition.Source)) definition.Source = info.Source;
        return definition;
    }

    public static HotKeys Enrich(HotKeys hotkey, object? provider = null)
    {
        ArgumentNullException.ThrowIfNull(hotkey);
        HotkeyPresentationInfo info = Resolve(hotkey.Id, hotkey.Name, hotkey.DisplayName, hotkey.Description,
            hotkey.Category, hotkey.Source, hotkey.HotKeyHandler, provider);
        if (string.IsNullOrWhiteSpace(hotkey.DisplayName)) hotkey.DisplayName = info.Name;
        if (string.IsNullOrWhiteSpace(hotkey.Description)) hotkey.Description = info.Description;
        if (string.IsNullOrWhiteSpace(hotkey.Category)) hotkey.Category = info.Category;
        if (string.IsNullOrWhiteSpace(hotkey.Source)) hotkey.Source = info.Source;
        return hotkey;
    }

    internal static string GetText(string key) => Text.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static HotkeyPresentationInfo Resolve(string id, string name, string displayName, string description, string category,
        string source, HotKeyCallBackHanlder? callback, object? provider)
    {
        object? contributor = provider ?? callback?.Target;
        Type? providerType = contributor?.GetType() ?? callback?.Method.DeclaringType;
        MethodInfo? method = callback?.Method;
        // A multi-action provider's class description describes the provider, not every action it contributes.
        bool singleAction = contributor is not IHotkeyProvider && contributor is (IHotKey or IMenuItem);
        IMenuItem? menu = singleAction ? contributor as IMenuItem : null;
        string declaredName = IsTechnicalIdentity(name, id) ? string.Empty : CleanLabel(name);

        string visibleName = First(displayName, declaredName,
            ReadAttribute<DisplayNameAttribute>(method, attribute => attribute.DisplayName, providerType),
            singleAction ? ReadAttribute<DisplayNameAttribute>(providerType, attribute => attribute.DisplayName, providerType) : string.Empty,
            menu == null ? string.Empty : CleanMenuLabel(TryRead(() => menu.Header)), GetText("CustomAction"));
        string visibleDescription = First(description,
            ReadAttribute<DescriptionAttribute>(method, attribute => attribute.Description, providerType),
            singleAction ? ReadAttribute<DescriptionAttribute>(providerType, attribute => attribute.Description, providerType) : string.Empty,
            GetText("DescriptionUnavailable"));
        string visibleCategory = First(category,
            ReadAttribute<CategoryAttribute>(method, attribute => attribute.Category, providerType),
            singleAction ? ReadAttribute<CategoryAttribute>(providerType, attribute => attribute.Category, providerType) : string.Empty,
            menu == null ? string.Empty : MenuCategory(TryRead(() => menu.OwnerGuid)), GetText("OtherCategory"));
        string visibleSource = First(source, providerType?.Assembly.GetName().Name, GetText("UnknownSource"));
        return new(CleanLabel(visibleName), visibleDescription, CleanLabel(visibleCategory), visibleSource);
    }

    private static string ReadAttribute<T>(MemberInfo? member, Func<T, string> value, Type? providerType) where T : Attribute
    {
        if (member == null) return string.Empty;
        return TryRead(() => member.GetCustomAttribute<T>(inherit: true) is { } attribute
            ? LocalizeAttribute(value(attribute), providerType?.Assembly ?? member.Module.Assembly) : string.Empty);
    }

    private static string LocalizeAttribute(string value, Assembly assembly)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        foreach (string resource in assembly.GetManifestResourceNames().Where(name => name.EndsWith(".Properties.Resources.resources", StringComparison.Ordinal)))
        {
            string? translated = TryRead(() => new ResourceManager(resource[..^10], assembly).GetString(value, CultureInfo.CurrentUICulture));
            if (!string.IsNullOrWhiteSpace(translated)) return translated;
        }
        return value;
    }

    private static string MenuCategory(string owner) => owner switch
    {
        MenuItemConstants.File => CleanMenuLabel(Properties.Resources.MenuFile),
        MenuItemConstants.Edit => CleanMenuLabel(Properties.Resources.MenuEdit),
        MenuItemConstants.View => CleanMenuLabel(Properties.Resources.MenuView),
        MenuItemConstants.Tool => CleanMenuLabel(Properties.Resources.MenuTool),
        MenuItemConstants.Help => CleanMenuLabel(Properties.Resources.MenuHelp),
        _ => string.Empty,
    };

    private static bool IsTechnicalIdentity(string value, string id) => string.IsNullOrWhiteSpace(value)
        || (string.Equals(value, id, StringComparison.Ordinal) && value.IndexOfAny(['.', '+', '<', '>']) >= 0);

    private static string CleanLabel(string? value) => AccessKeySuffix.Replace(value ?? string.Empty, string.Empty).Trim();

    private static string CleanMenuLabel(string? value)
    {
        string label = CleanLabel(value);
        // WPF menu headers use a single underscore for access keys, and a doubled one for a literal underscore.
        return Regex.Replace(label, "__(?=.)|_(?=.)", match => match.Value.Length == 2 ? "_" : string.Empty);
    }

    private static string First(params string?[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string TryRead(Func<string?> value)
    {
        try { return value() ?? string.Empty; }
        catch (Exception exception) when (exception is not OutOfMemoryException) { return string.Empty; }
    }
}

/// <summary>Localized descriptions supplied by the corresponding built-in action providers.</summary>
public static class BuiltInHotkeyDescriptions
{
    public static string OpenSettings => HotkeyPresentation.GetText("OpenSettings");
    public static string OpenLog => HotkeyPresentation.GetText("OpenLog");
    public static string CheckUpdates => HotkeyPresentation.GetText("CheckUpdates");
    public static string ToggleStatusBar => HotkeyPresentation.GetText("ToggleStatusBar");
    public static string OpenAbout => HotkeyPresentation.GetText("OpenAbout");
    public static string ResetLayout => HotkeyPresentation.GetText("ResetLayout");
    public static string ResetLayoutConfirmation => HotkeyPresentation.GetText("ResetLayoutConfirmation");
    public static string SearchCommandsName => HotkeyPresentation.GetText("SearchCommandsName");
    public static string SearchCommands => HotkeyPresentation.GetText("SearchCommands");
    public static string SearchEntryPlaceholder => HotkeyPresentation.GetText("SearchEntryPlaceholder");
    public static string ContextualFindName => HotkeyPresentation.GetText("ContextualFindName");
    public static string ContextualFind => HotkeyPresentation.GetText("ContextualFind");
}
