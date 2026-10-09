using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Themes.Controls;

/// <summary>Segmented IPv4 input; optional text mode preserves host names, IPv6 and mixed transport addresses.</summary>
public partial class IPAddressBox : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(IPAddressBox),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, TextChangedCallback));
    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(IPAddressBox), new PropertyMetadata(false, ModeChangedCallback));
    public static readonly DependencyProperty AllowTextModeProperty = DependencyProperty.Register(nameof(AllowTextMode), typeof(bool), typeof(IPAddressBox), new PropertyMetadata(false, ModeChangedCallback));
    public static readonly DependencyProperty IsSegmentedProperty = DependencyProperty.Register(nameof(IsSegmented), typeof(bool), typeof(IPAddressBox), new PropertyMetadata(true, ModeChangedCallback));
    public static readonly RoutedEvent TextChangedEvent = EventManager.RegisterRoutedEvent(nameof(TextChanged), RoutingStrategy.Bubble, typeof(TextChangedEventHandler), typeof(IPAddressBox));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool AllowTextMode { get => (bool)GetValue(AllowTextModeProperty); set => SetValue(AllowTextModeProperty, value); }
    public bool IsSegmented { get => (bool)GetValue(IsSegmentedProperty); set => SetValue(IsSegmentedProperty, value); }
    public event TextChangedEventHandler TextChanged { add => AddHandler(TextChangedEvent, value); remove => RemoveHandler(TextChangedEvent, value); }
    private TextBox[] parts = Array.Empty<TextBox>();
    private bool updating;
    private bool textModeSelected;

    public IPAddressBox()
    {
        InitializeComponent();
        parts = new[] { Part0, Part1, Part2, Part3 };
        foreach (var part in parts)
        {
            part.TextChanged += SegmentChanged;
            part.PreviewTextInput += SegmentInput;
            part.PreviewKeyDown += SegmentKeyDown;
            DataObject.AddPastingHandler(part, SegmentPaste);
        }
        SynchronizeText();
        RefreshMode();
    }

    private static void TextChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var box = (IPAddressBox)sender;
        if (!box.updating) box.SynchronizeText();
        box.RaiseEvent(new TextChangedEventArgs(TextChangedEvent, UndoAction.None));
    }

    private static void ModeChangedCallback(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var box = (IPAddressBox)sender;
        if (args.Property == AllowTextModeProperty) box.SynchronizeText();
        box.RefreshMode();
    }

    private void SynchronizeText()
    {
        if (parts.Length == 0) return;
        string text = Text?.Trim() ?? string.Empty;
        string[] values = text.Split('.');
        updating = true;
        try { for (int i = 0; i < parts.Length; i++) parts[i].Text = values.Length == 4 ? values[i] : string.Empty; }
        finally { updating = false; }
        if (AllowTextMode && !textModeSelected && !BindingOperations.IsDataBound(this, IsSegmentedProperty))
            SetCurrentValue(IsSegmentedProperty, text.Length == 0 || TryParseAddress(text, out _));
    }

    private void RefreshMode()
    {
        if (PlainText == null) return;
        PlainText.Visibility = IsSegmented ? Visibility.Collapsed : Visibility.Visible;
        ModeButton.Visibility = AllowTextMode && !IsReadOnly ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SegmentChanged(object sender, TextChangedEventArgs args)
    {
        // Child events must not be mistaken for an address change by consumers.
        args.Handled = true;
        if (updating || !IsSegmented) return;
        updating = true;
        try { SetCurrentValue(TextProperty, parts.All(part => part.Text.Length == 0) ? string.Empty : string.Join(".", parts.Select(part => part.Text))); }
        finally { updating = false; }
    }

    private void SegmentInput(object sender, TextCompositionEventArgs args)
    {
        if (IsReadOnly) { args.Handled = true; return; }
        var part = (TextBox)sender;
        if (args.Text == ".")
        {
            args.Handled = true;
            if (part.Text.Length > 0) MoveSegment(part, 1);
            return;
        }
        string candidate = part.Text.Remove(part.SelectionStart, part.SelectionLength).Insert(part.SelectionStart, args.Text);
        if (!IsOctet(candidate)) { args.Handled = true; return; }
        // Apply first, then advance, so filling three digits never changes the next segment accidentally.
        args.Handled = true;
        part.SelectedText = args.Text;
        part.CaretIndex = part.Text.Length;
        if (candidate.Length == 3) MoveSegment(part, 1);
    }

    private void SegmentKeyDown(object sender, KeyEventArgs args)
    {
        var part = (TextBox)sender;
        if (args.Key == Key.Right && part.CaretIndex == part.Text.Length && part.SelectionLength == 0) args.Handled = MoveSegment(part, 1);
        else if (args.Key is Key.Left or Key.Back && part.CaretIndex == 0 && part.SelectionLength == 0) args.Handled = MoveSegment(part, -1);
    }

    private bool MoveSegment(TextBox part, int offset)
    {
        int index = Array.IndexOf(parts, part) + offset;
        if (index is < 0 or > 3) return false;
        parts[index].Focus();
        parts[index].SelectAll();
        return true;
    }

    public void FocusFirstSegment()
    {
        if (!IsSegmented) { PlainText.Focus(); PlainText.SelectAll(); return; }
        parts[0].Focus();
        parts[0].SelectAll();
    }

    private void SegmentPaste(object sender, DataObjectPastingEventArgs args)
    {
        args.CancelCommand();
        if (IsReadOnly || args.DataObject.GetData(DataFormats.UnicodeText) is not string value) return;
        if (TryPasteAddress(value)) return;
        var part = (TextBox)sender;
        string candidate = part.Text.Remove(part.SelectionStart, part.SelectionLength).Insert(part.SelectionStart, value);
        if (IsOctet(candidate)) part.SelectedText = value;
    }

    public bool TryPasteAddress(string value)
    {
        if (IsReadOnly || !TryParseAddress(value.Trim(), out string[] values)) return false;
        SetCurrentValue(TextProperty, string.Join(".", values));
        return true;
    }

    private static bool IsOctet(string value) => value.Length is > 0 and <= 3 && value.All(character => character is >= '0' and <= '9')
        && byte.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    private static bool TryParseAddress(string value, out string[] values)
    {
        values = value.Split('.');
        return values.Length == 4 && values.All(IsOctet);
    }

    private void Mode_Click(object sender, RoutedEventArgs args)
    {
        ModeButton.ContextMenu.PlacementTarget = ModeButton;
        ModeButton.ContextMenu.IsOpen = true;
    }
    private void IPv4Mode_Click(object sender, RoutedEventArgs args) { textModeSelected = false; SetCurrentValue(IsSegmentedProperty, true); FocusFirstSegment(); }
    private void TextMode_Click(object sender, RoutedEventArgs args) { textModeSelected = true; SetCurrentValue(IsSegmentedProperty, false); FocusFirstSegment(); }
}
