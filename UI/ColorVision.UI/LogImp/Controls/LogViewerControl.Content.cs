using ColorVision.UI.LogImp.Models;
using System.IO;

namespace ColorVision.UI.LogImp.Controls
{
    public partial class LogViewerControl
    {
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
    }
}
