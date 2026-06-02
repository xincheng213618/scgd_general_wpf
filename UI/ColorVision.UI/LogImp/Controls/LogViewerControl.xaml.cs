using ColorVision.UI.LogImp.Models;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp.Controls
{
    public partial class LogViewerControl : UserControl
    {
        public static readonly DependencyProperty TextWrappingProperty = DependencyProperty.Register(
            nameof(TextWrapping),
            typeof(TextWrapping),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(TextWrapping.NoWrap, OnTextWrappingChanged));

        public static readonly DependencyProperty MaxEntriesProperty = DependencyProperty.Register(
            nameof(MaxEntries),
            typeof(int),
            typeof(LogViewerControl),
            new FrameworkPropertyMetadata(LogConstants.DefaultMaxEntries, OnMaxEntriesChanged));

        private readonly List<LogEntry> _entries = new();
        private readonly BulkObservableCollection<LogEntry> _visibleEntries = new();
        private readonly DispatcherTimer _resumeScrollTimer;
        private string _searchText = string.Empty;
        private bool _latestAtTop;
        private bool _suspendAutoScroll;

        public LogViewerControl()
        {
            InitializeComponent();

            _resumeScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(LogConstants.AutoScrollResumeDelaySeconds)
            };
            _resumeScrollTimer.Tick += ResumeScrollTimer_Tick;

            CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, Copy_Executed, Copy_CanExecute));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, SelectAll_Executed, SelectAll_CanExecute));

            PreviewKeyDown += LogViewerControl_PreviewKeyDown;
            PreviewMouseDown += LogViewerControl_PreviewMouseDown;
            EntriesListBox.PreviewMouseDown += EntriesListBox_PreviewMouseDown;
            EntriesListBox.PreviewMouseUp += EntriesListBox_PreviewMouseUp;
            EntriesListBox.PreviewMouseWheel += EntriesListBox_PreviewMouseWheel;
            Unloaded += LogViewerControl_Unloaded;

            UpdateWrappingScrollMode();
        }

        public IEnumerable<LogEntry> VisibleEntries => _visibleEntries;

        public TextWrapping TextWrapping
        {
            get => (TextWrapping)GetValue(TextWrappingProperty);
            set => SetValue(TextWrappingProperty, value);
        }

        public int MaxEntries
        {
            get => (int)GetValue(MaxEntriesProperty);
            set => SetValue(MaxEntriesProperty, value);
        }

        public bool IsSearchActive => !string.IsNullOrEmpty(_searchText);

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
            RefreshVisibleEntries();
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

            if (latestAtTop)
            {
                _entries.InsertRange(0, displayEntries);
            }
            else
            {
                _entries.AddRange(displayEntries);
            }

            var removedCount = TrimEntries(keepHead: latestAtTop);
            UpdateVisibleEntriesAfterAppend(displayEntries, latestAtTop, removedCount);

            if (autoScroll && !_suspendAutoScroll)
            {
                ScrollToLatest(latestAtTop);
            }
        }

        public void Clear()
        {
            _entries.Clear();
            _visibleEntries.Clear();
        }

        public bool ApplySearchFilter(string searchText)
        {
            _searchText = searchText;
            if (string.IsNullOrEmpty(searchText))
            {
                _visibleEntries.ResetWith(_entries);
                return true;
            }

            if (!LogSearchHelper.FilterItems(searchText, _entries, entry => entry.Text, out var filteredEntries))
            {
                return false;
            }

            _visibleEntries.ResetWith(filteredEntries);
            return true;
        }

        public void ScrollToLatest(bool latestAtTop)
        {
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
            EntriesListBox.ContextMenu = contextMenu;
        }

        public void CopySelection()
        {
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
            e.CanExecute = EntriesListBox.SelectedItems.Count > 0;
        }

        private void Copy_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CopySelection();
            e.Handled = true;
        }

        private void SelectAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = _visibleEntries.Count > 0;
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
            Focus();
        }

        private void EntriesListBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PauseAutoScroll();
        }

        private void EntriesListBox_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
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

        private void LogViewerControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _resumeScrollTimer.Stop();
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
                logViewer.RefreshVisibleEntries();
            }
        }

        private static void OnTextWrappingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LogViewerControl logViewer)
            {
                logViewer.UpdateWrappingScrollMode();
            }
        }

        private void UpdateWrappingScrollMode()
        {
            if (EntriesListBox == null)
            {
                return;
            }

            var horizontalScrollBarVisibility = TextWrapping == TextWrapping.NoWrap
                ? ScrollBarVisibility.Auto
                : ScrollBarVisibility.Disabled;
            ScrollViewer.SetHorizontalScrollBarVisibility(EntriesListBox, horizontalScrollBarVisibility);
            EntriesListBox.InvalidateMeasure();
        }
    }
}
