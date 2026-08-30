using System.Globalization;
using System.Resources;

namespace ColorVision.UI.HotKey;

public static class HotkeyEditorText
{
    private static readonly ResourceManager ResourceManager = new("ColorVision.UI.HotKey.HotkeyEditorResources", typeof(HotkeyEditorText).Assembly);
    public static string Get(string key) => ResourceManager.GetString(key, Properties.Resources.Culture ?? CultureInfo.CurrentUICulture) ?? key;
    public static string Title => Get(nameof(Title));
    public static string Subtitle => Get(nameof(Subtitle));
    public static string Search => Get(nameof(Search));
    public static string ResetAll => Get(nameof(ResetAll));
    public static string Reset => Get(nameof(Reset));
    public static string Edit => Get(nameof(Edit));
    public static string Clear => Get(nameof(Clear));
    public static string Unassigned => Get(nameof(Unassigned));
    public static string Global => Get(nameof(Global));
    public static string Modified => Get(nameof(Modified));
    public static string Empty => Get(nameof(Empty));
    public static string EmptyHelp => Get(nameof(EmptyHelp));
    public static string Count => Get(nameof(Count));
    public static string SearchCount => Get(nameof(SearchCount));
    public static string Saved => Get(nameof(Saved));
    public static string ResetConfirm => Get(nameof(ResetConfirm));
    public static string ResetTitle => Get(nameof(ResetTitle));
    public static string EditTitle => Get(nameof(EditTitle));
    public static string Record => Get(nameof(Record));
    public static string RecordHelp => Get(nameof(RecordHelp));
    public static string GlobalOption => Get(nameof(GlobalOption));
    public static string GlobalHelp => Get(nameof(GlobalHelp));
    public static string Default => Get(nameof(Default));
    public static string Apply => Get(nameof(Apply));
    public static string Cancel => Get(nameof(Cancel));
    public static string Invalid => Get(nameof(Invalid));
    public static string Conflict => Get(nameof(Conflict));
    public static string Error => Get(nameof(Error));
    public static string NoChange => Get(nameof(NoChange));
    public static string CaptureError => Get(nameof(CaptureError));
    public static string ScopeWindow => Get(nameof(ScopeWindow));
    public static string ScopeGlobal => Get(nameof(ScopeGlobal));
    public static string ClearValue => Get(nameof(ClearValue));
    public static string ChangedHelp => Get(nameof(ChangedHelp));
    public static string KeyboardHint => Get(nameof(KeyboardHint));
    public static string CaptureLabel => Get(nameof(CaptureLabel));
    public static string SavedHelp => Get(nameof(SavedHelp));
    public static string Add => Get(nameof(Add));
    public static string Duplicate => Get(nameof(Duplicate));
    public static string ClearSearch => Get(nameof(ClearSearch));
    public static string ClearFilters => Get(nameof(ClearFilters));
    public static string Filter => Get(nameof(Filter));
    public static string All => Get(nameof(All));
    public static string NoActions => Get(nameof(NoActions));
    public static string NoActionsHelp => Get(nameof(NoActionsHelp));
}
