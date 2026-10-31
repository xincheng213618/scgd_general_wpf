using ColorVision.UI.LogImp.Models;
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
                    Common.Clipboard.SetText(PlainTextBox.SelectedText);
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

        private static SolidColorBrush CreateFrozenBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }
    }
}
