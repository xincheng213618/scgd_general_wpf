using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ColorVision.UI.HotKey;

public sealed class HotkeySettingsRow
{
    public HotkeySettingsRow(HotKeys hotkey)
    {
        Value = hotkey;
        Presentation = HotkeyPresentation.For(hotkey);
    }

    public HotKeys Value { get; }
    public HotkeyPresentationInfo Presentation { get; }
    public string Name => Presentation.Name;
    public string Description => Presentation.Description;
    public string Shortcut => HotkeyInput.Format(Value.Hotkey);
    public bool IsAssigned => !Value.Hotkey.IsEmpty;
    public bool IsModified => !Value.Hotkey.Equals(Value.DefaultHotkey) || Value.Kinds != Value.DefaultKinds;
    public bool IsGlobal => Value.IsGlobal;
    public string Details => $"{Presentation.Category} · {Presentation.Source}\n{Value.Id}";
}

/// <summary>The editor uses detached snapshots. Only a successful service transaction changes the displayed binding.</summary>
public sealed class HotkeySettingsViewModel : INotifyPropertyChanged
{
    private readonly Func<IReadOnlyList<HotKeys>> _read;
    private readonly Func<IReadOnlyList<HotKeys>> _defaults;
    private readonly Func<IEnumerable<HotkeySetting>, HotkeyApplyResult> _apply;
    private readonly Func<HotkeyCaptureLease>? _capture;
    private readonly Func<IEnumerable<HotkeySetting>, HotkeyApplyResult>? _validate;
    private List<HotkeySettingsRow> _rows = new();
    private string _search = string.Empty;
    private string _status = string.Empty;
    private bool _isError;

    public HotkeySettingsViewModel() : this(
        () => HotkeyService.GetInstance().CreateEditableHotKeys(false),
        () => HotkeyService.GetInstance().CreateDefaultEditableHotKeys(),
        settings => HotkeyService.GetInstance().ApplyAndSaveSettings(settings),
        () => HotkeyService.GetInstance().BeginCapture(),
        settings => HotkeyService.GetInstance().ValidateSettings(settings)) { }

    public HotkeySettingsViewModel(Func<IReadOnlyList<HotKeys>> read, Func<IReadOnlyList<HotKeys>> defaults,
        Func<IEnumerable<HotkeySetting>, HotkeyApplyResult> apply, Func<HotkeyCaptureLease>? capture = null,
        Func<IEnumerable<HotkeySetting>, HotkeyApplyResult>? validate = null)
    {
        _read = read;
        _defaults = defaults;
        _apply = apply;
        _capture = capture;
        _validate = validate;
        Refresh();
    }

    public ObservableCollection<HotkeySettingsRow> Rows { get; } = new();
    public string Search { get => _search; set { if (_search == value) return; _search = value; Filter(); OnChanged(); } }
    public string Status { get => _status; private set { _status = value; OnChanged(); OnChanged(nameof(HasStatus)); } }
    public bool IsError { get => _isError; private set { _isError = value; OnChanged(); } }
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsEmpty => Rows.Count == 0;
    public bool HasCustomizations => _rows.Any(row => row.IsModified);
    public string Summary => string.IsNullOrWhiteSpace(Search)
        ? string.Format(HotkeyEditorText.Count, _rows.Count, _rows.Count(row => row.IsModified))
        : string.Format(HotkeyEditorText.SearchCount, Rows.Count);

    public HotkeyCaptureLease? BeginCapture() => _capture?.Invoke();

    public string? Validate(string id, Hotkey key, HotKeyKinds kinds)
    {
        if (!key.IsEmpty && !HotkeyInput.IsValid(key)) return HotkeyEditorText.Invalid;
        if (_validate != null)
        {
            try
            {
                HotkeyApplyResult result = _validate(new[] { new HotkeySetting { Id = id, Hotkey = key, Kinds = kinds } });
                return result.Success ? null : result.Message;
            }
            catch (Exception ex) { return $"{HotkeyEditorText.Error}: {ex.Message}"; }
        }
        HotkeySettingsRow? conflict = _rows.FirstOrDefault(row => !string.Equals(row.Value.Id, id, StringComparison.OrdinalIgnoreCase) && !key.IsEmpty && row.Value.Hotkey.Equals(key));
        return conflict == null ? null : string.Format(HotkeyEditorText.Conflict, conflict.Name);
    }

    public bool Save(HotkeySettingsRow row, Hotkey key, HotKeyKinds kinds)
    {
        string? error = Validate(row.Value.Id, key, kinds);
        if (error != null) { ReportError(error); return false; }
        return Apply(new[] { new HotkeySetting { Id = row.Value.Id, Hotkey = new Hotkey(key.Key, key.Modifiers), Kinds = kinds } });
    }

    public bool Clear(HotkeySettingsRow row) => Save(row, new Hotkey(Key.None, ModifierKeys.None), row.Value.Kinds);
    public bool Reset(HotkeySettingsRow row) => Save(row, row.Value.DefaultHotkey, row.Value.DefaultKinds);
    public bool ResetAll()
    {
        try { return Apply(_defaults().Select(HotkeySetting.FromHotKeys)); }
        catch (Exception ex) { ReportError($"{HotkeyEditorText.Error}: {ex.Message}"); return false; }
    }

    public void ReportError(string message) { IsError = true; Status = message; }

    private bool Apply(IEnumerable<HotkeySetting> settings)
    {
        try
        {
            HotkeyApplyResult result = _apply(settings);
            if (!TryRefresh()) return false;
            IsError = !result.Success;
            Status = result.Success ? HotkeyEditorText.Saved : result.Message;
            return result.Success;
        }
        catch (Exception ex)
        {
            TryRefresh();
            ReportError($"{HotkeyEditorText.Error}: {ex.Message}");
            return false;
        }
    }

    public void Refresh()
    {
        _rows = _read().Select(hotkey => new HotkeySettingsRow(hotkey)).ToList();
        Filter();
        OnChanged(nameof(HasCustomizations));
    }

    public bool TryRefresh()
    {
        try { Refresh(); return true; }
        catch (Exception ex) { ReportError($"{HotkeyEditorText.Error}: {ex.Message}"); return false; }
    }

    private void Filter()
    {
        string query = Normalize(Search);
        Rows.Clear();
        foreach (HotkeySettingsRow row in _rows)
        {
            string haystack = Normalize($"{row.Name} {row.Description} {row.Shortcut} {row.Details}");
            if (query.Length == 0 || haystack.Contains(query, StringComparison.OrdinalIgnoreCase)) Rows.Add(row);
        }
        OnChanged(nameof(Summary));
        OnChanged(nameof(IsEmpty));
    }

    private static string Normalize(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c != '+'));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
