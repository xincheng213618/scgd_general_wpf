using ColorVision.Themes;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.HotKey;

public partial class HotkeyEditWindow : Window
{
    private readonly HotkeySettingsViewModel _model;
    private readonly HotkeySettingsRow _row;
    private HotkeyCaptureLease? _capture;
    private Hotkey _candidate;
    private bool _captureFailed;
    private bool _captureStarted;
    private readonly int? _bindingIndex;

    public HotkeyEditWindow(HotkeySettingsViewModel model, HotkeySettingsRow row, int? bindingIndex = 0)
    {
        _model = model;
        _row = row;
        _bindingIndex = row.IsAssigned ? bindingIndex : null;
        Hotkey key = _bindingIndex is int index ? row.Bindings[index].Key : new Hotkey();
        _candidate = new Hotkey(key.Key, key.Modifiers);
        InitializeComponent();
        this.ApplyCaption();
        DataContext = row;
        GlobalCheckBox.IsChecked = row.Value.IsGlobal;
        GlobalCheckBox.Checked += (_, _) => UpdateCandidate();
        GlobalCheckBox.Unchecked += (_, _) => UpdateCandidate();
        DefaultText.Text = row.DefaultShortcut;
        Title = _bindingIndex == null ? HotkeyEditorText.Add : HotkeyEditorText.EditTitle;
        UpdateCandidate();
        Loaded += (_, _) => { StartCapture(); CaptureBox.Focus(); };
        Closed += (_, _) => EndCapture();
    }

    private void StartCapture()
    {
        if (_captureStarted || _captureFailed) return;
        try { _capture = _model.BeginCapture(); _captureStarted = true; }
        catch (Exception ex)
        {
            DisableCapture(ex.Message);
            _model.TryRefresh();
            _model.ReportError(ex.Message);
        }
    }

    private bool EndCapture()
    {
        HotkeyCaptureLease? capture = _capture;
        _capture = null;
        _captureStarted = false;
        if (capture == null) return true;
        string? failure = null;
        try { capture.Dispose(); }
        catch (Exception ex) { failure = ex.Message; }
        if (failure == null && capture.RestoreResult?.Success == true) return true;
        string message = string.Format(HotkeyEditorText.CaptureError, failure ?? capture.RestoreResult?.Message ?? HotkeyEditorText.Error);
        DisableCapture(message);
        _model.TryRefresh();
        _model.ReportError(message);
        return false;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        ModifierKeys modifiers = Keyboard.Modifiers;
        if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin)) modifiers |= ModifierKeys.Windows;
        e.Handled = HandleKeyInput(key, modifiers, CaptureBox.IsKeyboardFocusWithin);
    }

    // The routed event supplies OS state; the editor transition can also be verified without desktop input injection.
    internal bool HandleKeyInput(Key key, ModifierKeys modifiers, bool captureFocused)
    {
        if (key == Key.Escape && modifiers == ModifierKeys.None)
        {
            Close();
            return true;
        }
        if (!captureFocused) return false;
        if (key == Key.Tab && modifiers is ModifierKeys.None or ModifierKeys.Shift) return false;
        if (_captureFailed || !_captureStarted || HotkeyInput.IsModifier(key)) return true;
        _candidate = new(key, modifiers);
        UpdateCandidate();
        return true;
    }

    private void UpdateCandidate()
    {
        CaptureBox.Text = HotkeyInput.Format(_candidate);
        if (_captureFailed) return;
        string? error = _model.ValidateBinding(_row, _bindingIndex, _candidate, GlobalCheckBox.IsChecked == true ? HotKeyKinds.Global : HotKeyKinds.Windows);
        if (_bindingIndex == null && _candidate.IsEmpty) error = HotkeyEditorText.RecordHelp;
        ErrorText.Text = error ?? string.Empty;
        ErrorText.Visibility = error == null ? Visibility.Collapsed : Visibility.Visible;
        SaveButton.IsEnabled = error == null;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }

    private void DisableCapture(string message)
    {
        _captureFailed = true;
        SaveButton.IsEnabled = false;
        CaptureBox.IsEnabled = false;
        ClearButton.IsEnabled = false;
        GlobalCheckBox.IsEnabled = false;
        ShowError(message);
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (_captureFailed) return;
        _candidate = new Hotkey(Key.None, ModifierKeys.None);
        UpdateCandidate();
        CaptureBox.Focus();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_captureFailed || !SaveButton.IsEnabled || !_captureStarted) return;
        if (!EndCapture()) { SaveButton.IsEnabled = false; return; }
        if (_model.SaveBinding(_row, _bindingIndex, _candidate, GlobalCheckBox.IsChecked == true ? HotKeyKinds.Global : HotKeyKinds.Windows))
        {
            Close();
            return;
        }
        ShowError(_model.Status);
        StartCapture();
        if (!_captureFailed) CaptureBox.Focus();
    }
}
