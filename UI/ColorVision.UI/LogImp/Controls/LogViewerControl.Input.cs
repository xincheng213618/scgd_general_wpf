using ColorVision.UI.LogImp.Models;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.LogImp.Controls
{
    public partial class LogViewerControl
    {
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
    }
}
