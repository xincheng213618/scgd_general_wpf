using AvalonDock.Layout;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.Solution.Workspace;

/// <summary>
/// Keeps the fixed-tab state with the open AvalonDock document instance.
/// Fixed documents are grouped at the front of their pane and cannot be dragged or floated.
/// </summary>
public static class DocumentTabPinManager
{
    private static readonly ConditionalWeakTable<LayoutDocument, DocumentTabPinState> States = new();

    public static ICommand ToggleCommand { get; } = new ToggleDocumentTabPinCommand();

    public static bool IsPinned(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return GetState(document).IsPinned;
    }

    public static string GetToggleText(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return GetState(document).ToggleText;
    }

    internal static DocumentTabPinState GetState(LayoutDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return States.GetValue(document, static value => new DocumentTabPinState(value));
    }

    internal static void MoveToFixedBoundary(LayoutDocument document)
    {
        if (document.Parent is not LayoutDocumentPane pane)
            return;

        int targetIndex = pane.Children
            .OfType<LayoutDocument>()
            .Count(candidate => !ReferenceEquals(candidate, document)
                && States.TryGetValue(candidate, out DocumentTabPinState? state)
                && state.IsPinned);
        int currentIndex = pane.IndexOf(document);
        if (currentIndex >= 0 && currentIndex != targetIndex)
            pane.MoveChild(currentIndex, targetIndex);
    }

    private sealed class ToggleDocumentTabPinCommand : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => parameter is LayoutDocument { Parent: LayoutDocumentPane };

        public void Execute(object? parameter)
        {
            if (parameter is LayoutDocument document && CanExecute(document))
                GetState(document).Toggle();
        }
    }
}

internal sealed class DocumentTabPinState : INotifyPropertyChanged
{
    private readonly LayoutDocument _document;
    private bool _canMoveBeforePin;

    internal DocumentTabPinState(LayoutDocument document)
    {
        _document = document;
    }

    public bool IsPinned { get; private set; }

    public string ToggleText => GetText(IsPinned ? "Sol_DocumentTabUnpin" : "Sol_DocumentTabPin",
        IsPinned ? "Unpin tab" : "Pin tab");

    public event PropertyChangedEventHandler? PropertyChanged;

    internal void Toggle()
    {
        if (IsPinned)
        {
            IsPinned = false;
            _document.CanMove = _canMoveBeforePin;
        }
        else
        {
            _canMoveBeforePin = _document.CanMove;
            _document.CanMove = false;
            IsPinned = true;
        }

        DocumentTabPinManager.MoveToFixedBoundary(_document);

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPinned)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToggleText)));
        CommandManager.InvalidateRequerySuggested();
    }

    private static string GetText(string name, string fallback)
        => Properties.Resources.ResourceManager.GetString(name, Properties.Resources.Culture) ?? fallback;
}

public sealed class DocumentTabPinStateConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is LayoutDocument document ? DocumentTabPinManager.GetState(document) : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
