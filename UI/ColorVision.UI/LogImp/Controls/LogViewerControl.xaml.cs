using ColorVision.UI.LogImp.Models;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp.Controls
{
    public partial class LogViewerControl : UserControl
    {
        private const double RangeSelectionAutoScrollEdge = 18;

        public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap, OnTextWrappingChanged));

        public static readonly DependencyProperty ViewerModeProperty = DependencyProperty.Register(
            nameof(ViewerMode),
            typeof(LogViewerMode),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(LogViewerMode.TextBox, OnViewerModeChanged));

        public static readonly DependencyProperty MaxEntriesProperty = DependencyProperty.Register(
            nameof(MaxEntries),
            typeof(int),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(LogConstants.DefaultMaxEntries, OnMaxEntriesChanged));

        public static readonly DependencyProperty MaxCharsProperty = DependencyProperty.Register(
            nameof(MaxChars),
            typeof(int),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(-1, OnMaxCharsChanged));

        public static readonly DependencyProperty UseLevelColorsProperty = DependencyProperty.Register(
            nameof(UseLevelColors),
            typeof(bool),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(true));

        public static readonly DependencyProperty WarningForegroundProperty = DependencyProperty.Register(
            nameof(WarningForeground),
            typeof(Brush),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(CreateFrozenBrush(0xB2, 0x6A, 0x00)));

        public static readonly DependencyProperty ErrorForegroundProperty = DependencyProperty.Register(
            nameof(ErrorForeground),
            typeof(Brush),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(CreateFrozenBrush(0xD3, 0x2F, 0x2F)));

        public static readonly DependencyProperty FatalForegroundProperty = DependencyProperty.Register(
            nameof(FatalForeground),
            typeof(Brush),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(CreateFrozenBrush(0xB0, 0x00, 0x20)));

        public static readonly DependencyProperty DebugForegroundProperty = DependencyProperty.Register(
            nameof(DebugForeground),
            typeof(Brush),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(CreateFrozenBrush(0x6E, 0x77, 0x81)));

        public static readonly DependencyProperty TraceForegroundProperty = DependencyProperty.Register(
            nameof(TraceForeground),
            typeof(Brush),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(CreateFrozenBrush(0x6E, 0x77, 0x81)));

        private readonly List<LogEntry> _entries = new();
        private readonly BulkObservableCollection<LogEntry> _visibleEntries = new();
        private readonly DispatcherTimer _resumeScrollTimer;
        private readonly DispatcherTimer _rangeSelectionScrollTimer;
        private ScrollViewer? _entriesScrollViewer;
        private LogViewerMode _activeViewerMode = LogViewerMode.TextBox;
        private string _plainText = string.Empty;
        private string _searchText = string.Empty;
        private int _selectionAnchorIndex = -1;
        private Point _rangeSelectionPoint;
        private bool _latestAtTop;
        private bool _isRangeSelecting;
        private bool _viewerModeLocked;
        private bool _suspendAutoScroll;

        public LogViewerControl()
        {
            InitializeComponent();

            _resumeScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(LogConstants.AutoScrollResumeDelaySeconds)
            };
            _resumeScrollTimer.Tick += ResumeScrollTimer_Tick;

            _rangeSelectionScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(80)
            };
            _rangeSelectionScrollTimer.Tick += RangeSelectionScrollTimer_Tick;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, Copy_Executed, Copy_CanExecute));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, SelectAll_Executed, SelectAll_CanExecute));

            PreviewKeyDown += LogViewerControl_PreviewKeyDown;
            PreviewMouseDown += LogViewerControl_PreviewMouseDown;
            PlainTextBox.PreviewMouseDown += PlainTextBox_PreviewMouseDown;
            PlainTextBox.PreviewMouseUp += PlainTextBox_PreviewMouseUp;
            PlainTextBox.PreviewMouseWheel += PlainTextBox_PreviewMouseWheel;
            EntriesListBox.PreviewMouseDown += EntriesListBox_PreviewMouseDown;
            EntriesListBox.PreviewMouseMove += EntriesListBox_PreviewMouseMove;
            EntriesListBox.PreviewMouseUp += EntriesListBox_PreviewMouseUp;
            EntriesListBox.PreviewMouseWheel += EntriesListBox_PreviewMouseWheel;
            Loaded += LogViewerControl_Loaded;
            Unloaded += LogViewerControl_Unloaded;

            ApplyViewerMode(LogViewerMode.TextBox);
            UpdateWrappingScrollMode();
        }

        public IEnumerable<LogEntry> VisibleEntries => _visibleEntries;

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public LogViewerMode ViewerMode
        {
            get => (LogViewerMode)GetValue(ViewerModeProperty);
            set => SetValue(ViewerModeProperty, value);
        }

        public int MaxEntries
        {
            get => (int)GetValue(MaxEntriesProperty);
            set => SetValue(MaxEntriesProperty, value);
        }

        public int MaxChars
        {
            get => (int)GetValue(MaxCharsProperty);
            set => SetValue(MaxCharsProperty, value);
        }

        public bool UseLevelColors
        {
            get => (bool)GetValue(UseLevelColorsProperty);
            set => SetValue(UseLevelColorsProperty, value);
        }

        public Brush WarningForeground
        {
            get => (Brush)GetValue(WarningForegroundProperty);
            set => SetValue(WarningForegroundProperty, value);
        }

        public Brush ErrorForeground
        {
            get => (Brush)GetValue(ErrorForegroundProperty);
            set => SetValue(ErrorForegroundProperty, value);
        }

        public Brush FatalForeground
        {
            get => (Brush)GetValue(FatalForegroundProperty);
            set => SetValue(FatalForegroundProperty, value);
        }

        public Brush DebugForeground
        {
            get => (Brush)GetValue(DebugForegroundProperty);
            set => SetValue(DebugForegroundProperty, value);
        }

        public Brush TraceForeground
        {
            get => (Brush)GetValue(TraceForegroundProperty);
            set => SetValue(TraceForegroundProperty, value);
        }

        public bool IsSearchActive => !string.IsNullOrEmpty(_searchText);

        public bool UsesVirtualizedRendering => _activeViewerMode == LogViewerMode.Virtualized;

        public void SetEntries(IEnumerable<LogEntry> entries, bool latestAtTop)
        {
            ArgumentNullException.ThrowIfNull(entries);

            _latestAtTop = latestAtTop;
            _entries.Clear();
            _entries.AddRange(entries);

            if (latestAtTop)
            {
                _entries.Reverse();
            }

            TrimEntries(keepHead: latestAtTop);
            _plainText = BuildText(_entries);
            TrimPlainText(keepHead: latestAtTop);
            ResetSelectionState(clearSelection: true);
            RefreshActiveView();
            ScrollToLatest(latestAtTop);
        }

        public void SetText(string text, bool latestAtTop)
        {
            _latestAtTop = latestAtTop;
            _plainText = text ?? string.Empty;
            TrimPlainText(keepHead: latestAtTop);
            _entries.Clear();

            if (UsesVirtualizedRendering && !string.IsNullOrEmpty(_plainText))
            {
                _entries.AddRange(LogEntryParser.FromLines(ReadTextLines(_plainText)));
                TrimEntries(keepHead: latestAtTop);
            }

            ResetSelectionState(clearSelection: true);
            RefreshActiveView();
            ScrollToLatest(latestAtTop);
        }

        public void SetMessage(string message, LogEntryLevel level)
        {
            SetEntries(new[] { new LogEntry(message, level) }, latestAtTop: false);
        }

        public void AppendEntries(IEnumerable<LogEntry> entries, bool latestAtTop, bool autoScroll)
        {
            ArgumentNullException.ThrowIfNull(entries);

            var incomingEntries = entries.Where(entry => !string.IsNullOrEmpty(entry.Text)).ToList();
            if (incomingEntries.Count == 0)
            {
                return;
            }

            _latestAtTop = latestAtTop;
            var displayEntries = latestAtTop
                ? incomingEntries.AsEnumerable().Reverse().ToList()
                : incomingEntries;
            var wasPlainTextEmpty = string.IsNullOrEmpty(_plainText);
            var appendedText = BuildText(displayEntries);

            if (latestAtTop)
            {
                _entries.InsertRange(0, displayEntries);
                _plainText = CombineText(appendedText, _plainText);
            }
            else
            {
                _entries.AddRange(displayEntries);
                _plainText = CombineText(_plainText, appendedText);
            }

            var removedCount = TrimEntries(keepHead: latestAtTop);
            if (removedCount > 0)
            {
                _plainText = BuildText(_entries);
            }

            var textTrimmed = TrimPlainText(keepHead: latestAtTop);

            if (UsesVirtualizedRendering)
            {
                UpdateVisibleEntriesAfterAppend(displayEntries, latestAtTop, removedCount);
            }
            else
            {
                UpdatePlainTextAfterAppend(appendedText, latestAtTop, wasPlainTextEmpty, removedCount > 0 || textTrimmed);
            }

            if (autoScroll && !_suspendAutoScroll)
            {
                ScrollToLatest(latestAtTop);
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _plainText = string.Empty;
            PlainTextBox.Clear();
            ResetSelectionState(clearSelection: true);
            _visibleEntries.Clear();
        }

        public bool ApplySearchFilter(string searchText)
        {
            _searchText = searchText;
            if (!UsesVirtualizedRendering)
            {
                return RefreshPlainTextBox();
            }

            if (string.IsNullOrEmpty(searchText))
            {
                ResetSelectionState(clearSelection: true);
                _visibleEntries.ResetWith(_entries);
                return true;
            }

            if (!LogSearchHelper.FilterItems(searchText, _entries, entry => entry.Text, out var filteredEntries))
            {
                return false;
            }

            ResetSelectionState(clearSelection: true);
            _visibleEntries.ResetWith(filteredEntries);
            return true;
        }

        public void ScrollToLatest(bool latestAtTop)
        {
            if (!UsesVirtualizedRendering)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (latestAtTop)
                    {
                        PlainTextBox.ScrollToHome();
                    }
                    else
                    {
                        PlainTextBox.ScrollToEnd();
                    }
                }, DispatcherPriority.Background);
                return;
            }

            if (_visibleEntries.Count == 0)
            {
                return;
            }

            Dispatcher.BeginInvoke(() =>
            {
                if (_visibleEntries.Count == 0)
                {
                    return;
                }

                var target = latestAtTop ? _visibleEntries[0] : _visibleEntries[^1];
                EntriesListBox.ScrollIntoView(target);
            }, DispatcherPriority.Background);
        }

        public void SetViewerContextMenu(ContextMenu contextMenu)
        {
            PlainTextBox.ContextMenu = contextMenu;
            EntriesListBox.ContextMenu = contextMenu;
        }

        public void CopySelection()
        {
            if (!UsesVirtualizedRendering)
            {
                if (PlainTextBox.SelectionLength > 0)
                {
                    Common.NativeMethods.Clipboard.SetText(PlainTextBox.SelectedText);
                }

                return;
            }

            if (EntriesListBox.SelectedItems.Count == 0)
            {
                return;
            }

            var selectedEntries = EntriesListBox.SelectedItems.OfType<LogEntry>().ToHashSet();
            var builder = new StringBuilder();
            foreach (var entry in _visibleEntries)
            {
                if (!selectedEntries.Contains(entry))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(entry.Text);
            }

            if (builder.Length > 0)
            {
                Clipboard.SetText(builder.ToString());
            }
        }

        public void SelectAllEntries()
        {
            if (!UsesVirtualizedRendering)
            {
                PlainTextBox.Focus();
                PlainTextBox.SelectAll();
                return;
            }

            EntriesListBox.SelectAll();
        }

        private void UpdateVisibleEntriesAfterAppend(IReadOnlyList<LogEntry> displayEntries, bool latestAtTop, int removedCount)
        {
            if (IsSearchActive)
            {
                if (removedCount > 0)
                {
                    ApplySearchFilter(_searchText);
                    return;
                }

                if (!LogSearchHelper.FilterItems(_searchText, displayEntries, entry => entry.Text, out var filteredEntries))
                {
                    return;
                }

                if (latestAtTop)
                {
                    _visibleEntries.InsertRangeAtStart(filteredEntries);
                }
                else
                {
                    _visibleEntries.AddRange(filteredEntries);
                }

                return;
            }

            if (latestAtTop)
            {
                _visibleEntries.InsertRangeAtStart(displayEntries);
                _visibleEntries.RemoveLast(removedCount);
                return;
            }

            _visibleEntries.AddRange(displayEntries);
            _visibleEntries.RemoveFirst(removedCount);
        }

        private void RefreshVisibleEntries()
        {
            if (IsSearchActive)
            {
                ApplySearchFilter(_searchText);
                return;
            }

            _visibleEntries.ResetWith(_entries);
        }

        private void RefreshActiveView()
        {
            if (UsesVirtualizedRendering)
            {
                RefreshVisibleEntries();
                return;
            }

            RefreshPlainTextBox();
        }

        private bool RefreshPlainTextBox()
        {
            ResetSelectionState(clearSelection: false);
            if (string.IsNullOrEmpty(_searchText))
            {
                PlainTextBox.Text = _plainText;
                return true;
            }

            if (!LogSearchHelper.FilterText(_searchText, _plainText, out var filteredText))
            {
                return false;
            }

            PlainTextBox.Text = filteredText;
            return true;
        }

        private void UpdatePlainTextAfterAppend(string appendedText, bool latestAtTop, bool wasPlainTextEmpty, bool forceRefresh)
        {
            if (forceRefresh || IsSearchActive)
            {
                RefreshPlainTextBox();
                return;
            }

            if (latestAtTop)
            {
                PlainTextBox.Text = _plainText;
                return;
            }

            if (wasPlainTextEmpty)
            {
                PlainTextBox.Text = appendedText;
                return;
            }

            if (!string.IsNullOrEmpty(appendedText))
            {
                PlainTextBox.AppendText(Environment.NewLine + appendedText);
            }
        }

        private void ResetSelectionState(bool clearSelection)
        {
            _selectionAnchorIndex = -1;
            StopRangeSelection();
            if (clearSelection)
            {
                EntriesListBox.SelectedItems.Clear();
            }
        }

        private static string BuildText(IEnumerable<LogEntry> entries)
        {
            return string.Join(Environment.NewLine, entries.Select(entry => entry.Text));
        }

        private static string CombineText(string first, string second)
        {
            if (string.IsNullOrEmpty(first))
            {
                return second;
            }

            if (string.IsNullOrEmpty(second))
            {
                return first;
            }

            return first + Environment.NewLine + second;
        }

        private int TrimEntries(bool keepHead)
        {
            if (MaxEntries <= 0 || _entries.Count <= MaxEntries)
            {
                return 0;
            }

            var removeCount = _entries.Count - MaxEntries;
            if (keepHead)
            {
                _entries.RemoveRange(MaxEntries, removeCount);
            }
            else
            {
                _entries.RemoveRange(0, removeCount);
            }

            return removeCount;
        }

        private bool TrimPlainText(bool keepHead)
        {
            if (MaxChars <= LogConstants.MinMaxCharsForTrimming || _plainText.Length <= MaxChars)
            {
                return false;
            }

            _plainText = keepHead
                ? _plainText[..MaxChars]
                : _plainText[^MaxChars..];
            return true;
        }

        private static IEnumerable<string> ReadTextLines(string text)
        {
            using var reader = new StringReader(text);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private void PauseAutoScroll()
        {
            _suspendAutoScroll = true;
            _resumeScrollTimer.Stop();
        }

        private void ResumeAutoScrollWithDelay()
        {
            _resumeScrollTimer.Stop();
            _resumeScrollTimer.Start();
        }

        private void Copy_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = UsesVirtualizedRendering
                ? EntriesListBox.SelectedItems.Count > 0
                : PlainTextBox.SelectionLength > 0;
        }

        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CopySelection();
            e.Handled = true;
        }

        private void SelectAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = UsesVirtualizedRendering
                ? _visibleEntries.Count > 0
                : PlainTextBox.Text.Length > 0;
        }

        private void SelectAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SelectAllEntries();
            e.Handled = true;
        }

        private void LogViewerControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control)
            {
                return;
            }

            if (e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
            }
            else if (e.Key == Key.A)
            {
                SelectAllEntries();
                e.Handled = true;
            }
        }

        private void LogViewerControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (UsesVirtualizedRendering)
            {
                Focus();
            }
        }

        private void PlainTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PauseAutoScroll();
        }

        private void PlainTextBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ResumeAutoScrollWithDelay();
        }

        private void PlainTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PauseAutoScroll();
            ResumeAutoScrollWithDelay();
        }

        private void EntriesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PauseAutoScroll();
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            if (!TryGetEntryAtPoint(e.GetPosition(EntriesListBox), out var entry, out var entryIndex))
            {
                return;
            }

            Focus();
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (EntriesListBox.SelectedItems.Contains(entry))
                {
                    EntriesListBox.SelectedItems.Remove(entry);
                }
                else
                {
                    EntriesListBox.SelectedItems.Add(entry);
                }

                _selectionAnchorIndex = entryIndex;
                e.Handled = true;
                return;
            }

            if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift && _selectionAnchorIndex >= 0)
            {
                SelectEntryRange(_selectionAnchorIndex, entryIndex);
                e.Handled = true;
                return;
            }

            EntriesListBox.SelectedItems.Clear();
            EntriesListBox.SelectedItems.Add(entry);
            _selectionAnchorIndex = entryIndex;
            _rangeSelectionPoint = e.GetPosition(EntriesListBox);
            _isRangeSelecting = true;
            _rangeSelectionScrollTimer.Start();
            EntriesListBox.CaptureMouse();
            e.Handled = true;
        }

        private void EntriesListBox_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isRangeSelecting || e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            _rangeSelectionPoint = e.GetPosition(EntriesListBox);
            if (TryGetEntryAtPoint(_rangeSelectionPoint, out _, out var entryIndex))
            {
                SelectEntryRange(_selectionAnchorIndex, entryIndex);
                e.Handled = true;
            }
        }

        private void EntriesListBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isRangeSelecting)
            {
                StopRangeSelection();
                e.Handled = true;
            }

            ResumeAutoScrollWithDelay();
        }

        private void EntriesListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PauseAutoScroll();
            ResumeAutoScrollWithDelay();
        }

        private void ResumeScrollTimer_Tick(object? sender, EventArgs e)
        {
            _resumeScrollTimer.Stop();
            _suspendAutoScroll = false;
        }

        private void RangeSelectionScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isRangeSelecting || Mouse.LeftButton != MouseButtonState.Pressed)
            {
                StopRangeSelection();
                return;
            }

            var scrollViewer = GetEntriesScrollViewer();
            if (scrollViewer == null || EntriesListBox.ActualHeight <= 0)
            {
                return;
            }

            if (_rangeSelectionPoint.Y < RangeSelectionAutoScrollEdge)
            {
                scrollViewer.LineUp();
            }
            else if (_rangeSelectionPoint.Y > EntriesListBox.ActualHeight - RangeSelectionAutoScrollEdge)
            {
                scrollViewer.LineDown();
            }
            else
            {
                return;
            }

            if (TryGetEntryAtClampedPoint(_rangeSelectionPoint, out var entryIndex))
            {
                SelectEntryRange(_selectionAnchorIndex, entryIndex);
            }
        }

        private void LogViewerControl_Loaded(object sender, RoutedEventArgs e)
        {
            _viewerModeLocked = true;
        }

        private void LogViewerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _resumeScrollTimer.Stop();
            _rangeSelectionScrollTimer.Stop();
        }

        private static void OnViewerModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewerControl logViewer || logViewer._viewerModeLocked)
            {
                return;
            }

            logViewer.ApplyViewerMode((LogViewerMode)e.NewValue);
        }

        private static void OnMaxEntriesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewerControl logViewer)
            {
                return;
            }

            var removedCount = logViewer.TrimEntries(keepHead: logViewer._latestAtTop);
            if (removedCount > 0)
            {
                logViewer._plainText = BuildText(logViewer._entries);
                logViewer.TrimPlainText(keepHead: logViewer._latestAtTop);
                logViewer.RefreshActiveView();
            }
        }

        private static void OnMaxCharsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not LogViewerControl logViewer)
            {
                return;
            }

            if (logViewer.TrimPlainText(keepHead: logViewer._latestAtTop) && !logViewer.UsesVirtualizedRendering)
            {
                logViewer.RefreshPlainTextBox();
            }
        }

        private static void OnTextWrappingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewerControl logViewer)
            {
                logViewer.UpdateWrappingScrollMode();
            }
        }

        private void ApplyViewerMode(LogViewerMode viewerMode)
        {
            _activeViewerMode = viewerMode;
            var useVirtualized = viewerMode == LogViewerMode.Virtualized;
            if (useVirtualized && _entries.Count == 0 && !string.IsNullOrEmpty(_plainText))
            {
                _entries.AddRange(LogEntryParser.FromLines(ReadTextLines(_plainText)));
                TrimEntries(keepHead: _latestAtTop);
            }
            else if (!useVirtualized && string.IsNullOrEmpty(_plainText) && _entries.Count > 0)
            {
                _plainText = BuildText(_entries);
                TrimPlainText(keepHead: _latestAtTop);
            }

            PlainTextBox.Visibility = useVirtualized ? Visibility.Collapsed : Visibility.Visible;
            EntriesListBox.Visibility = useVirtualized ? Visibility.Visible : Visibility.Collapsed;
            UpdateWrappingScrollMode();
            RefreshActiveView();
        }

        private void UpdateWrappingScrollMode()
        {
            if (EntriesListBox == null || PlainTextBox == null)
            {
                return;
            }

            var horizontalScrollBarVisibility = TextWrapping == TextWrapping.NoWrap
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
            ScrollViewer.SetHorizontalScrollBarVisibility(EntriesListBox, horizontalScrollBarVisibility);
            ScrollViewer.SetHorizontalScrollBarVisibility(PlainTextBox, horizontalScrollBarVisibility);
            EntriesListBox.InvalidateMeasure();
            PlainTextBox.InvalidateMeasure();
        }

        private bool TryGetEntryAtPoint(Point point, out LogEntry entry, out int entryIndex)
        {
            entry = null!;
            entryIndex = -1;

            if (EntriesListBox.InputHitTest(point) is not DependencyObject dependencyObject)
            {
                return false;
            }

            while (dependencyObject != EntriesListBox)
            {
                if (dependencyObject is ListBoxItem { DataContext: LogEntry logEntry } listBoxItem)
                {
                    entry = logEntry;
                    entryIndex = EntriesListBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
                    return entryIndex >= 0;
                }

                var parent = VisualTreeHelper.GetParent(dependencyObject);
                if (parent == null)
                {
                    return false;
                }

                dependencyObject = parent;
            }

            return false;
        }

        private bool TryGetEntryAtClampedPoint(Point point, out int entryIndex)
        {
            entryIndex = -1;
            if (EntriesListBox.ActualWidth <= 0 || EntriesListBox.ActualHeight <= 0)
            {
                return false;
            }

            var maxX = Math.Max(1, EntriesListBox.ActualWidth - SystemParameters.VerticalScrollBarWidth - 1);
            var maxY = Math.Max(1, EntriesListBox.ActualHeight - SystemParameters.HorizontalScrollBarHeight - 1);
            var x = Math.Clamp(point.X, 1, maxX);
            var y = Math.Clamp(point.Y, 1, maxY);
            return TryGetEntryAtPoint(new Point(x, y), out _, out entryIndex);
        }

        private void SelectEntryRange(int startIndex, int endIndex)
        {
            if (startIndex < 0 || endIndex < 0 || _visibleEntries.Count == 0)
            {
                return;
            }

            var start = Math.Clamp(Math.Min(startIndex, endIndex), 0, _visibleEntries.Count - 1);
            var end = Math.Clamp(Math.Max(startIndex, endIndex), 0, _visibleEntries.Count - 1);

            EntriesListBox.SelectedItems.Clear();
            for (var i = start; i <= end; i++)
            {
                EntriesListBox.SelectedItems.Add(_visibleEntries[i]);
            }
        }

        private void StopRangeSelection()
        {
            _isRangeSelecting = false;
            _rangeSelectionScrollTimer.Stop();
            if (EntriesListBox.IsMouseCaptureWithin)
            {
                EntriesListBox.ReleaseMouseCapture();
            }
        }

        private ScrollViewer? GetEntriesScrollViewer()
        {
            if (_entriesScrollViewer != null)
            {
                return _entriesScrollViewer;
            }

            _entriesScrollViewer = FindVisualChild<ScrollViewer>(EntriesListBox);
            return _entriesScrollViewer;
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (var i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                {
                    return match;
                }

                var nestedMatch = FindVisualChild<T>(child);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }

        private static SolidColorBrush CreateFrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
