using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;

namespace ColorVision.UI.HotKey;

public sealed class HotkeySettingsRow
{
    public HotkeySettingsRow(HotKeys hotkey)
    {
        Value = hotkey;
        Presentation = HotkeyPresentation.For(hotkey);
        Bindings = hotkey.GetBindings().Select((key, index) => new HotkeySettingsBindingRow(this, index, key)).ToArray();
    }

    public HotKeys Value { get; }
    public HotkeyPresentationInfo Presentation { get; }
    public string Name => Presentation.Name;
    public string Description => Presentation.Description;
    public IReadOnlyList<HotkeySettingsBindingRow> Bindings { get; }
    public string Shortcut => IsAssigned ? string.Join(" / ", Bindings.Select(binding => binding.Shortcut)) : HotkeyEditorText.Unassigned;
    public string DefaultShortcut => string.Format(HotkeyEditorText.Default, FormatBindings(Value.GetDefaultBindings()));
    public bool IsAssigned => Bindings.Count > 0;
    public bool IsUnassigned => !IsAssigned;
    public bool IsModified => !Value.GetBindings().SequenceEqual(Value.GetDefaultBindings()) || Value.Kinds != Value.DefaultKinds;
    public bool IsGlobal => Value.IsGlobal;
    public string Details => $"{Presentation.Category} · {Presentation.Source}\n{Value.Id}";
    internal static string FormatBindings(IReadOnlyList<Hotkey> bindings) => bindings.Count == 0
        ? HotkeyEditorText.Unassigned : string.Join(" / ", bindings.Select(HotkeyInput.Format));
}

public sealed record HotkeySettingsBindingRow(HotkeySettingsRow Owner, int Index, Hotkey Key)
{
    public string Shortcut => HotkeyInput.Format(Key);
    public string EditLabel => $"{HotkeyEditorText.Edit}: {Owner.Name} — {Shortcut}";
    public string ClearLabel => $"{HotkeyEditorText.Clear}: {Owner.Name} — {Shortcut}";
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
    private int _filterIndex;

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
    public string Search { get => _search; set { if (_search == value) return; _search = value ?? string.Empty; Filter(); OnChanged(); OnChanged(nameof(HasSearch)); } }
    public int FilterIndex { get => _filterIndex; set { if (_filterIndex == value) return; _filterIndex = value; Filter(); OnChanged(); } }
    public bool HasSearch => !string.IsNullOrEmpty(Search);
    public bool HasFilter => HasSearch || FilterIndex != 0;
    public string Status { get => _status; private set { _status = value; OnChanged(); OnChanged(nameof(HasStatus)); } }
    public bool IsError { get => _isError; private set { _isError = value; OnChanged(); } }
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public bool IsEmpty => Rows.Count == 0;
    public string EmptyTitle => _rows.Count == 0 ? HotkeyEditorText.NoActions : HotkeyEditorText.Empty;
    public string EmptyHelp => _rows.Count == 0 ? HotkeyEditorText.NoActionsHelp : HotkeyEditorText.EmptyHelp;
    public bool HasCustomizations => _rows.Any(row => row.IsModified);
    public string Summary => !HasFilter
        ? string.Format(HotkeyEditorText.Count, _rows.Count, _rows.Count(row => row.IsModified), _rows.Count(row => !row.IsAssigned))
        : string.Format(HotkeyEditorText.SearchCount, Rows.Count, _rows.Count);

    public HotkeyCaptureLease? BeginCapture() => _capture?.Invoke();

    public string? Validate(string id, Hotkey key, HotKeyKinds kinds)
    {
        HotkeySettingsRow? row = _rows.FirstOrDefault(item => string.Equals(item.Value.Id, id, StringComparison.OrdinalIgnoreCase));
        return row == null ? ValidateBindings(id, key.IsEmpty ? [] : [key], kinds)
            : ValidateBinding(row, row.IsAssigned ? 0 : null, key, kinds);
    }

    public string? ValidateBinding(HotkeySettingsRow row, int? bindingIndex, Hotkey key, HotKeyKinds kinds)
        => ValidateBindings(row.Value.Id, BuildBindings(row, bindingIndex, key), kinds);

    private string? ValidateBindings(string id, IReadOnlyList<Hotkey> bindings, HotKeyKinds kinds)
    {
        if (bindings.Any(key => key.IsEmpty || !HotkeyInput.IsValid(key))) return HotkeyEditorText.Invalid;
        if (bindings.Distinct().Count() != bindings.Count) return HotkeyEditorText.Duplicate;
        HotkeySetting candidate = new() { Id = id, Kinds = kinds };
        candidate.SetBindings(bindings);
        if (_validate != null)
        {
            try
            {
                HotkeyApplyResult result = _validate([candidate]);
                return result.Success ? null : result.Message;
            }
            catch (Exception ex) { return $"{HotkeyEditorText.Error}: {ex.Message}"; }
        }
        HotkeySettingsRow? conflict = _rows.FirstOrDefault(row => !string.Equals(row.Value.Id, id, StringComparison.OrdinalIgnoreCase)
            && row.Value.GetBindings().Any(key => bindings.Contains(key)));
        return conflict == null ? null : string.Format(HotkeyEditorText.Conflict, conflict.Name);
    }

    public bool Save(HotkeySettingsRow row, Hotkey key, HotKeyKinds kinds)
        => SaveBinding(row, row.IsAssigned ? 0 : null, key, kinds);

    public bool SaveBinding(HotkeySettingsRow row, int? bindingIndex, Hotkey key, HotKeyKinds kinds)
        => SaveBindings(row, BuildBindings(row, bindingIndex, key), kinds);

    private static IReadOnlyList<Hotkey> BuildBindings(HotkeySettingsRow row, int? bindingIndex, Hotkey key)
    {
        List<Hotkey> bindings = row.Value.GetBindings().ToList();
        if (bindingIndex is int index)
        {
            if (index < 0 || index >= bindings.Count) throw new ArgumentOutOfRangeException(nameof(bindingIndex));
            if (key.IsEmpty) bindings.RemoveAt(index);
            else bindings[index] = key;
        }
        else if (!key.IsEmpty) bindings.Add(key);
        return bindings;
    }

    private bool SaveBindings(HotkeySettingsRow row, IReadOnlyList<Hotkey> bindings, HotKeyKinds kinds)
    {
        string? error = ValidateBindings(row.Value.Id, bindings, kinds);
        if (error != null) { ReportError(error); return false; }
        HotkeySetting setting = new() { Id = row.Value.Id, Kinds = kinds };
        setting.SetBindings(bindings);
        return Apply([setting]);
    }

    public bool RemoveBinding(HotkeySettingsBindingRow binding) => SaveBinding(binding.Owner, binding.Index, new Hotkey(), binding.Owner.Value.Kinds);
    public bool Clear(HotkeySettingsRow row) => SaveBindings(row, [], row.Value.Kinds);
    public bool Reset(HotkeySettingsRow row) => SaveBindings(row, row.Value.GetDefaultBindings(), row.Value.DefaultKinds);
    public bool ResetAll()
    {
        try { return Apply(_defaults().Select(HotkeySetting.FromHotKeys)); }
        catch (Exception ex) { ReportError($"{HotkeyEditorText.Error}: {ex.Message}"); return false; }
    }

    public void ReportError(string message) { IsError = true; Status = message; }
    public void ClearFilters() { Search = string.Empty; FilterIndex = 0; }

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
        string[] terms = Regex.Replace(Search, @"\s*\+\s*", "+").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize).Where(term => term.Length > 0).ToArray();
        Rows.Clear();
        foreach (HotkeySettingsRow row in _rows)
        {
            if (FilterIndex == 1 && row.IsAssigned || FilterIndex == 2 && !row.IsModified) continue;
            string haystack = Normalize($"{row.Name} {row.Description} {row.Shortcut} {row.Details} {(row.IsModified ? HotkeyEditorText.Modified : "")} {(row.IsGlobal ? HotkeyEditorText.Global : "")}");
            if (terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase))) Rows.Add(row);
        }
        OnChanged(nameof(Summary));
        OnChanged(nameof(IsEmpty));
        OnChanged(nameof(HasFilter));
        OnChanged(nameof(EmptyTitle));
        OnChanged(nameof(EmptyHelp));
    }

    private static string Normalize(string value) => string.Concat(value.Where(c => !char.IsWhiteSpace(c) && c != '+'));
    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
