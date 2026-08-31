using ColorVision.Themes;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Serach;

/// <summary>A normal, non-modal owned window for application search.</summary>
public partial class SearchWindow : Window
{
    private bool _isClosing;

    public SearchWindow() : this(null) { }

    internal SearchWindow(SearchControl? content)
    {
        InitializeComponent();
        if (content != null)
        {
            UnregisterName(nameof(CommandSearchControl));
            CommandSearchControl = content;
            RegisterName(nameof(CommandSearchControl), content);
            Content = content;
        }
        Title = SearchPaletteText.Title;
        this.ApplyCaption();
        CommandSearchControl.Closed += SearchControl_Closed;
    }

    // The shell calls Open after Show so the target is captured before keyboard
    // focus moves to this window. Constructing/showing a host alone never queries.
    public void Open(IInputElement? commandTarget, Func<bool>? isCommandContextCurrent = null)
        => CommandSearchControl.Open(commandTarget, Owner, isCommandContextCurrent);

    public void FocusSearch()
    {
        if (IsActive && IsVisible) CommandSearchControl.FocusSearchBox();
    }

    private void SearchControl_Closed(object? sender, EventArgs e)
    {
        if (!_isClosing) Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        _isClosing = !e.Cancel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _isClosing = true;
        CommandSearchControl.Closed -= SearchControl_Closed;
        CommandSearchControl.Close();
        base.OnClosed(e);
    }
}
