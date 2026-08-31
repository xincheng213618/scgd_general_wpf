using log4net;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI.Serach;

public partial class SearchControl : UserControl
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SearchControl));
    private readonly SearchPaletteViewModel _model;
    private readonly Action<string> _recordUsed;
    private readonly Action _refreshCatalog;
    private IInputElement? _commandTarget;
    private object? _targetDataContext;
    private Window? _targetWindow;
    private bool _targetWasLoaded;
    private bool _isComposing;
    private int _compositionVersion;

    public SearchControl() : this((query, category, token) => SearchManager.GetInstance().QueryAsync(query, token, category: category),
        id => SearchManager.GetInstance().RecordUsed(id), () => SearchManager.GetInstance().InvalidateCatalog()) { }

    internal SearchControl(Func<string, string?, CancellationToken, Task<SearchQueryResult>> query, Action<string>? recordUsed = null, Action? refreshCatalog = null)
    {
        InitializeComponent();
        _recordUsed = recordUsed ?? (_ => { });
        _refreshCatalog = refreshCatalog ?? (() => { });
        _model = new SearchPaletteViewModel(query, item => SearchCommandExecutor.CanExecute(item.Source.Command, this, _commandTarget));
        PaletteRoot.DataContext = _model;
        TextCompositionManager.AddPreviewTextInputStartHandler(Searchbox, CompositionStarted);
        TextCompositionManager.AddPreviewTextInputHandler(Searchbox, CompositionCompleted);
        Unloaded += (_, _) => { if (_model.IsOpen) Close(); };
    }

    public event EventHandler? Closed;
    internal SearchPaletteViewModel Model => _model;

    public void Open(IInputElement? commandTarget)
    {
        if (_model.IsOpen) { FocusSearchBox(); return; }
        _commandTarget = commandTarget;
        _targetDataContext = GetTargetDataContext(commandTarget);
        _targetWindow = (commandTarget is DependencyObject dependency ? Window.GetWindow(dependency) : null)
            ?? Window.GetWindow(this) ?? Application.Current?.MainWindow;
        _targetWasLoaded = commandTarget is FrameworkElement { IsLoaded: true } or FrameworkContentElement { IsLoaded: true };
        _isComposing = false;
        _compositionVersion++;
        _refreshCatalog();
        _model.Open();
        FocusSearchBox();
    }

    public void Close()
    {
        if (!_model.IsOpen) return;
        _model.Close();
        _isComposing = false;
        _compositionVersion++;
        Closed?.Invoke(this, EventArgs.Empty);
        _commandTarget = null;
        _targetDataContext = null;
        _targetWindow = null;
    }

    public void FocusSearchBox()
    {
        Searchbox.Focus();
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_model.IsOpen && IsVisible) Searchbox.Focus();
        }));
    }

    private void CompositionStarted(object sender, TextCompositionEventArgs e)
    {
        _compositionVersion++;
        _isComposing = true;
    }
    private void CompositionCompleted(object sender, TextCompositionEventArgs e)
    {
        // Keep Enter/Esc protected for the rest of the IME commit event sequence.
        int version = _compositionVersion;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (version == _compositionVersion) _isComposing = false;
        }));
    }

    private void Palette_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isComposing || e.Key == Key.ImeProcessed) return;
        if (CategoryFilter.IsDropDownOpen) return;
        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (!e.IsRepeat) Close();
            return;
        }
        // Let the type selector and footer buttons keep their native keyboard interactions.
        if (!Searchbox.IsKeyboardFocusWithin && !ListViewSearch.IsKeyboardFocusWithin) return;
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        switch (e.Key)
        {
            case Key.Down:
            case Key.Up:
                _model.MoveSelection(e.Key == Key.Down ? 1 : -1);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                if (!e.IsRepeat) SubmitSelection();
                break;
        }
    }

    internal bool SubmitSelection()
    {
        if (_isComposing || !_model.TryGetSelection(out SearchPaletteEntry? entry)) return false;
        ICommand? command = entry!.Result.Source.Command;
        IInputElement? target = _commandTarget;
        object? originalDataContext = _targetDataContext;
        Window? originalWindow = _targetWindow;
        bool originallyLoaded = _targetWasLoaded;
        bool TargetIsValid() => IsOriginalTargetValid(target, originalDataContext, originalWindow, originallyLoaded);
        if (command is RoutedCommand && !TargetIsValid())
        {
            _model.SetStatus(SearchPaletteText.Get("TargetUnavailable"));
            return false;
        }
        if (!SearchCommandExecutor.CanExecute(command, this, target))
        {
            _model.SetStatus(SearchPaletteText.Get("Unavailable"));
            return false;
        }
        Window? owner = _targetWindow ?? Window.GetWindow(this);
        Close();
        try
        {
            if (!SearchCommandExecutor.TryExecute(command, this, target, command is RoutedCommand ? TargetIsValid : null)) return false;
            _recordUsed(entry.Result.StableId);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Log.Warn($"Search result execution failed: {entry.Result.StableId}", exception);
            string message = string.Format(CultureInfo.CurrentCulture, SearchPaletteText.Get("ExecutionFailed"), exception.Message);
            MessageBox.Show(owner, message, SearchPaletteText.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    private static bool IsOriginalTargetValid(IInputElement? target, object? dataContext, Window? owner, bool originallyLoaded)
    {
        if (target == null) return false;
        bool loaded = target is FrameworkElement { IsLoaded: true } or FrameworkContentElement { IsLoaded: true };
        return (!originallyLoaded || (loaded && target is DependencyObject dependency && Window.GetWindow(dependency) == owner))
            && ReferenceEquals(GetTargetDataContext(target), dataContext);
    }

    private static object? GetTargetDataContext(IInputElement? target) => target switch
    {
        FrameworkElement element => element.DataContext,
        FrameworkContentElement element => element.DataContext,
        _ => null
    };

    private void Result_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(ListViewSearch, e.OriginalSource as DependencyObject) is not ListBoxItem item
            || item.DataContext is not SearchPaletteEntry entry) return;
        _model.Selected = entry;
        e.Handled = true;
        SubmitSelection();
    }

    private void Result_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListViewSearch.SelectedItem != null) ListViewSearch.ScrollIntoView(ListViewSearch.SelectedItem);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Clear_Click(object sender, RoutedEventArgs e) { _model.SearchText = string.Empty; FocusSearchBox(); }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        Window? owner = _targetWindow ?? Window.GetWindow(this);
        Close();
        new SearchSettingsWindow { Owner = owner }.ShowDialog();
        _refreshCatalog();
    }
}
