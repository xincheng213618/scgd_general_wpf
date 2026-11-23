using System.Collections;

namespace ColorVision.Solution.Terminal
{
    /// <summary>Projects retained history onto the current width. The active screen keeps its physical layout.</summary>
    internal sealed class TerminalHistoryLayout : IReadOnlyList<TerminalLine>
    {
        private const int CompactionBatch = 512;
        private const int SliceCacheCapacity = 512;

        private IReadOnlyList<TerminalLine> _source = Array.Empty<TerminalLine>();
        private readonly List<HistoryRow> _history = new();
        private readonly Dictionary<(TerminalLine Line, int Segment), TerminalLine> _slices = new();
        private long _discardedLines;
        private long _historyEnd;
        private int _historyStart;
        private int _scrollbackCount;
        private int _columns;

        private long FirstHistoryLine => _scrollbackCount == 0 ? _historyEnd : _history[_historyStart].FirstLine;
        private int HistoryLineCount => (int)(_historyEnd - FirstHistoryLine);
        public int Count => HistoryLineCount + _source.Count - _scrollbackCount;

        public void Update(IReadOnlyList<TerminalLine> source, int scrollbackCount, int columns, long discardedLines)
        {
            columns = Math.Max(1, columns);
            scrollbackCount = Math.Clamp(scrollbackCount, 0, source.Count);
            long dropped = discardedLines - _discardedLines;
            if (columns != _columns)
                _slices.Clear();

            int shared = GetSharedRowCount(source, scrollbackCount, columns, dropped);
            if (shared == 0)
            {
                _history.Clear();
                _historyStart = 0;
                _historyEnd = 0;
            }
            else
            {
                _historyStart += (int)dropped;
                if (_historyStart >= CompactionBatch)
                {
                    _history.RemoveRange(0, _historyStart);
                    _historyStart = 0;
                }
            }

            for (int row = shared; row < scrollbackCount; row++)
            {
                var projected = new HistoryRow(source[row], columns, _historyEnd);
                _history.Add(projected);
                _historyEnd += projected.SegmentCount;
            }

            _source = source;
            _scrollbackCount = scrollbackCount;
            _discardedLines = discardedLines;
            _columns = columns;
        }

        private int GetSharedRowCount(IReadOnlyList<TerminalLine> source, int count, int columns, long dropped)
        {
            if (columns != _columns || dropped < 0 || dropped >= _scrollbackCount)
                return 0;

            int shared = _scrollbackCount - (int)dropped;
            if (shared > count)
                return 0;

            // Published buffer history is append-only after accounting for evictions.
            // Identity checks also handle a new session or an alternate-screen switch.
            int first = _historyStart + (int)dropped;
            return ReferenceEquals(source[0], _history[first].Source)
                && ReferenceEquals(source[shared - 1], _history[first + shared - 1].Source)
                ? shared : 0;
        }

        public TerminalLine this[int index]
        {
            get
            {
                ArgumentOutOfRangeException.ThrowIfNegative(index);
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, Count);
                int historyLines = HistoryLineCount;
                if (index >= historyLines)
                    return _source[_scrollbackCount + index - historyLines];

                var (row, segment) = FindHistoryRow(index);
                if (row.Source.Length <= _columns)
                    return row.Source;

                var key = (row.Source, segment);
                if (!_slices.TryGetValue(key, out var line))
                {
                    var (start, end) = row.GetBounds(segment, _columns);
                    line = row.Source.Slice(start, end, _columns, segment + 1 < row.SegmentCount || row.Source.IsWrapped);
                    if (_slices.Count >= SliceCacheCapacity)
                        _slices.Clear();
                    _slices.Add(key, line);
                }
                return line;
            }
        }

        public (int Row, int Column) GetSourcePosition(int displayLine, int displayColumn = 0)
        {
            if (Count == 0) return default;
            displayLine = Math.Clamp(displayLine, 0, Count - 1);
            int historyLines = HistoryLineCount;
            if (displayLine >= historyLines)
            {
                int row = _scrollbackCount + displayLine - historyLines;
                return (row, Math.Clamp(displayColumn, 0, Math.Max(_columns, _source[row].Length)));
            }

            int index = FindHistoryIndex(displayLine);
            var layout = _history[index];
            int segment = (int)(displayLine + FirstHistoryLine - layout.FirstLine);
            var (start, end) = layout.GetBounds(segment, _columns);
            int column = _columns == 1 && end - start == 2 && displayColumn > 0 ? end : start + Math.Clamp(displayColumn, 0, end - start);
            return (index - _historyStart, column);
        }

        public (int Line, int Column) GetDisplayPosition(int sourceRow, int sourceColumn)
        {
            if (_source.Count == 0) return default;
            sourceRow = Math.Clamp(sourceRow, 0, _source.Count - 1);
            if (sourceRow >= _scrollbackCount)
                return (HistoryLineCount + sourceRow - _scrollbackCount, Math.Clamp(sourceColumn, 0, Math.Max(_columns, _source[sourceRow].Length)));

            var row = _history[_historyStart + sourceRow];
            int column = Math.Clamp(sourceColumn, 0, row.Extent);
            int segment = row.GetSegment(column, _columns);
            var (start, end) = row.GetBounds(segment, _columns);
            int displayColumn = _columns == 1 && end - start == 2 ? (column == end ? 1 : 0) : column - start;
            return ((int)(row.FirstLine - FirstHistoryLine) + segment, displayColumn);
        }

        private (HistoryRow Row, int Segment) FindHistoryRow(int displayIndex)
        {
            var row = _history[FindHistoryIndex(displayIndex)];
            return (row, (int)(displayIndex + FirstHistoryLine - row.FirstLine));
        }

        private int FindHistoryIndex(int displayIndex)
        {
            long target = displayIndex + FirstHistoryLine;
            int first = _historyStart;
            int last = _history.Count - 1;
            while (first < last)
            {
                int mid = first + (last - first + 1) / 2;
                if (_history[mid].FirstLine <= target)
                    first = mid;
                else
                    last = mid - 1;
            }
            return first;
        }

        public IEnumerator<TerminalLine> GetEnumerator()
        {
            for (int i = 0; i < Count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private readonly struct HistoryRow
        {
            private readonly int[]? _segmentEnds;
            public TerminalLine Source { get; }
            public long FirstLine { get; }
            public int Extent { get; }
            public int SegmentCount { get; }

            public HistoryRow(TerminalLine source, int columns, long firstLine)
            {
                Source = source;
                FirstLine = firstLine;
                Extent = source.Length <= columns ? source.Length : source.TextColumns;
                SegmentCount = Math.Max(1, (Extent + columns - 1) / columns);
                _segmentEnds = null;

                // Plain text rows only need arithmetic. Record segment boundaries
                // for rows where the new margin falls inside a wide glyph.
                for (int col = columns; col < Extent; col += columns)
                {
                    if (source.Cells[col].IsContinuation)
                    {
                        _segmentEnds = GetWideBoundaries(source, columns, Extent);
                        SegmentCount = _segmentEnds.Length;
                        break;
                    }
                }
            }

            private static int[] GetWideBoundaries(TerminalLine source, int columns, int extent)
            {
                var ends = new List<int>();
                int start = 0;
                while (start < extent)
                {
                    int end = Math.Min(start + columns, extent);
                    if (end < extent && source.Cells[end].IsContinuation)
                        end += columns == 1 ? 1 : -1;
                    ends.Add(end);
                    start = end;
                }
                return ends.ToArray();
            }

            public (int Start, int End) GetBounds(int segment, int columns)
            {
                if (_segmentEnds == null)
                    return (segment * columns, Math.Min(Extent, (segment + 1) * columns));
                return (segment == 0 ? 0 : _segmentEnds[segment - 1], _segmentEnds[segment]);
            }

            public int GetSegment(int column, int columns)
            {
                if (_segmentEnds == null)
                    return Math.Min(column / columns, SegmentCount - 1);
                int index = Array.BinarySearch(_segmentEnds, column);
                return index < 0 ? ~index : Math.Min(index + 1, SegmentCount - 1);
            }
        }
    }
}
