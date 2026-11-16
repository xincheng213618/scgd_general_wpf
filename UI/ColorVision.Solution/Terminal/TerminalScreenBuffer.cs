using System.Collections;
using System.Globalization;
using System.Text;

namespace ColorVision.Solution.Terminal
{
    internal struct TerminalCell
    {
        public char Char;
        public string? Glyph;
        public int Fg; // 0 = default, 1-256 = indexed, 0x1000000 | RGB = true color
        public int Bg;
        public byte Flags;
        public byte Width; // 0 = ordinary cell, 1 = continuation cell, 2 = wide glyph

        public readonly bool IsBold => (Flags & 1) != 0;
        public readonly bool IsUnderline => (Flags & 2) != 0;
        public readonly bool IsInverse => (Flags & 4) != 0;
        public readonly bool IsDim => (Flags & 8) != 0;
        public readonly bool IsItalic => (Flags & 16) != 0;
        public readonly bool IsContinuation => Width == 1;
        public readonly int DisplayWidth => Width == 2 ? 2 : IsContinuation ? 0 : 1;
        public readonly bool IsDefaultBlank => Char == ' ' && Glyph == null && Fg == 0 && Bg == 0 && Flags == 0;

        public static TerminalCell Default => new() { Char = ' ' };
    }

    internal sealed class TerminalLine
    {
        private readonly TerminalCell[] _cells;
        private readonly int _wrapColumn;
        private string? _text;

        public ReadOnlySpan<TerminalCell> Cells => _cells;
        public int Length => _cells.Length;
        public bool IsWrapped => _wrapColumn > 0;
        public int TextColumns => IsWrapped ? _wrapColumn : Length;

        public TerminalLine(ReadOnlySpan<TerminalCell> cells, int wrapColumn = 0)
        {
            int length = cells.Length;
            while (length > wrapColumn && cells[length - 1].IsDefaultBlank)
                length--;

            _cells = cells[..length].ToArray();
            if (wrapColumn > 0)
            {
                // An edit can put text in what was previously only wide-glyph padding.
                while (length > wrapColumn && _cells[length - 1].Char == ' ' && _cells[length - 1].Glyph == null)
                    length--;
                _wrapColumn = length;
            }
        }

        public TerminalLine Slice(int start, int end, int columns, bool wraps)
        {
            var cells = _cells.AsSpan(start, end - start);
            if (columns == 1 && cells.Length == 2 && cells[1].IsContinuation)
            {
                var cell = cells[0];
                cell.Width = 0;
                return new TerminalLine([cell], wraps ? 1 : 0);
            }
            return new TerminalLine(cells, wraps ? cells.Length : 0);
        }

        public string Text
        {
            get
            {
                if (_text == null)
                {
                    var text = new StringBuilder(Length);
                    AppendText(text, 0, Length);
                    _text = text.ToString();
                }
                return _text;
            }
        }

        public void AppendText(StringBuilder target, int startColumn, int endColumn)
        {
            int start = Math.Clamp(startColumn, 0, TextColumns);
            int end = Math.Clamp(endColumn, start, TextColumns);
            if (start == end) return;
            if (_cells[start].IsContinuation)
                start--;

            foreach (var cell in _cells.AsSpan(start, end - start))
            {
                if (cell.IsContinuation) continue;
                if (cell.Glyph != null)
                    target.Append(cell.Glyph);
                else
                    target.Append(cell.Char);
            }
        }

        public int GetColumnForTextOffset(int utf16Offset, bool roundUp = false)
        {
            int offset = Math.Max(0, utf16Offset);
            for (int col = 0; col < TextColumns;)
            {
                var cell = _cells[col];
                int width = cell.DisplayWidth;
                int textLength = cell.Glyph?.Length ?? 1;
                if (offset < textLength)
                    return col + (roundUp && offset > 0 ? width : 0);
                offset -= textLength;
                col += width;
            }
            return TextColumns;
        }
    }

    /// <summary>
    /// Incrementally decodes ConPTY's VT stream into a primary and optional alternate screen.
    /// All screen mutations and snapshot reads occur on the dispatcher.
    /// </summary>
    internal sealed class TerminalScreenBuffer
    {
        private const int MaxScrollbackLines = 3000;
        private const int ScrollbackCompactionBatch = 512;
        private const int MaxCsiParameters = 32;
        private const int MaxCsiValue = 65535;

        private readonly Screen _primary;
        private Screen? _alternate;
        private Screen _active;
        private List<TerminalLine> _scrollback = new();
        private int _scrollbackStart;

        private enum ParserState { Ground, Escape, EscapeIntermediate, Csi, CsiIgnore, ControlString, ControlStringEscape }

        private ParserState _parserState;
        private char _escapeIntermediate;
        private bool _osc;
        private char _highSurrogate;
        private char _csiPrefix;
        private bool _csiIntermediate;
        private readonly int[] _parameters = new int[MaxCsiParameters];
        private readonly char[] _separators = new char[MaxCsiParameters];
        private int _parameterCount;
        private TerminalCell _style = TerminalCell.Default;
        private bool _autoWrap = true;
        private bool _originMode;
        private bool _insertMode;
        private bool _g0LineDrawing;
        private bool _g1LineDrawing;
        private bool _useG1;
        private bool _defaultTabStops = true;
        private readonly HashSet<int> _tabOverrides = [];

        public int Cols => _active.Cols;
        public int Rows => _active.Rows;
        public int CursorRow => _active.Row;
        public int CursorCol => _active.Col;
        public bool CursorVisible { get; private set; } = true;
        public bool IsAlternateScreen => _active != _primary;
        public bool ApplicationCursorKeys { get; private set; }
        public bool BracketedPaste { get; private set; }
        public bool Win32InputMode { get; private set; }
        public int ScrollbackCount => IsAlternateScreen ? 0 : _scrollback.Count - _scrollbackStart;
        public long ScrollbackDiscarded { get; private set; }
        public event Action<string>? ResponseReady;

        public TerminalScreenBuffer(int cols = 120, int rows = 30)
        {
            _primary = new Screen(Math.Max(1, cols), Math.Max(1, rows));
            _active = _primary;
        }

        public void Write(string text)
        {
            var remaining = text.AsSpan();
            while (!remaining.IsEmpty)
            {
                char c = remaining[0];
                if (_parserState == ParserState.Ground)
                {
                    if (c is >= ' ' and <= '~' && _highSurrogate == '\0' && !_insertMode && !LineDrawingActive)
                    {
                        int end = remaining.IndexOfAnyExceptInRange(' ', '~');
                        if (end < 0) end = remaining.Length;
                        WriteAscii(remaining[..end]);
                        remaining = remaining[end..];
                        continue;
                    }

                    WriteGround(c);
                    remaining = remaining[1..];
                    continue;
                }

                remaining = remaining[1..];
                if (c is '\x18' or '\x1a') // CAN / SUB
                {
                    _parserState = ParserState.Ground;
                    continue;
                }

                switch (_parserState)
                {
                    case ParserState.ControlString:
                    case ParserState.ControlStringEscape:
                        ReadControlString(c);
                        break;
                    case ParserState.Escape:
                    case ParserState.EscapeIntermediate:
                        ReadEscape(c);
                        break;
                    default:
                        ReadCsi(c);
                        break;
                }
            }
        }

        private void WriteGround(char c)
        {
            if (_highSurrogate != '\0')
            {
                char high = _highSurrogate;
                _highSurrogate = '\0';
                if (Rune.TryCreate(high, c, out var pair))
                {
                    WriteRune(pair);
                    return;
                }
                WriteRune(Rune.ReplacementChar);
            }

            if (c is >= ' ' and < '\x7f')
            {
                WriteGlyph(LineDrawingActive ? MapLineDrawing(c) : c, null, 1);
                return;
            }

            switch (c)
            {
                case '\x1b': _parserState = ParserState.Escape; break;
                case '\u009b': StartCsi(); break;
                case '\u009d': StartControlString(true); break;
                case '\u0090' or '\u0098' or '\u009e' or '\u009f': StartControlString(false); break;
                case '\u0084': LineFeed(); break;
                case '\u0085': ExecuteControl('\r'); LineFeed(); break;
                case '\u008d': ReverseIndex(); break;
                case < ' ': ExecuteControl(c); break;
                case >= '\x7f' and < '\u00a0': break;
                case >= '\ud800' and <= '\udbff': _highSurrogate = c; break;
                default: WriteRune(Rune.TryCreate(c, out var rune) ? rune : Rune.ReplacementChar); break;
            }
        }

        private void ExecuteControl(char c)
        {
            switch (c)
            {
                case '\r': _active.Col = 0; _active.WrapPending = false; break;
                case '\n' or '\v' or '\f': LineFeed(); break;
                case '\b': _active.Col = Math.Max(0, _active.Col - 1); _active.WrapPending = false; break;
                case '\t': MoveTab(1, 1); break;
                case '\x0e': _useG1 = true; break;
                case '\x0f': _useG1 = false; break;
            }
        }

        private bool LineDrawingActive => _useG1 ? _g1LineDrawing : _g0LineDrawing;

        private static char MapLineDrawing(char c) => c switch
        {
            '_' => ' ', '`' => '◆', 'a' => '▒', 'b' => '␉', 'c' => '␌', 'd' => '␍', 'e' => '␊',
            'f' => '°', 'g' => '±', 'h' => '␤', 'i' => '␋',
            'j' => '┘', 'k' => '┐', 'l' => '┌', 'm' => '└', 'n' => '┼',
            'o' => '⎺', 'p' => '⎻', 'q' => '─', 'r' => '⎼', 's' => '⎽',
            't' => '├', 'u' => '┤', 'v' => '┴', 'w' => '┬', 'x' => '│',
            'y' => '≤', 'z' => '≥', '{' => 'π', '|' => '≠', '}' => '£', '~' => '·',
            _ => c
        };

        private bool UsesDefaultTab(int col) => _defaultTabStops && col > 0 && col % 8 == 0;

        private void SetTabStop(int col, bool enabled)
        {
            if (UsesDefaultTab(col) == enabled)
                _tabOverrides.Remove(col);
            else
                _tabOverrides.Add(col);
        }

        private void MoveTab(int count, int direction)
        {
            int boundary = direction == 1 ? Cols - 1 : 0;
            int col = _active.Col;
            while (col != boundary && count > 0)
            {
                col += direction;
                if (UsesDefaultTab(col) ^ _tabOverrides.Contains(col))
                    count--;
            }
            MoveCursor(_active.Row, col);
        }

        private void WriteRune(Rune rune)
        {
            int value = rune.Value;
            var category = Rune.GetUnicodeCategory(rune);
            bool combining = category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
                or UnicodeCategory.SpacingCombiningMark || IsZeroWidthFormat(value) || value is >= 0x1f3fb and <= 0x1f3ff;
            if (combining)
            {
                if (TryGetPreviousGlyph(out var row, out int column))
                    AppendGlyph(row, column, rune);
                return;
            }
            if (TryAppendJoinedRune(rune)) return;

            WriteGlyph(value <= char.MaxValue ? (char)value : '\0', value > char.MaxValue ? rune.ToString() : null, GetWidth(value));
        }

        private static bool IsZeroWidthFormat(int value) => value is 0x061c or 0x180e or 0xfeff
            or >= 0x200b and <= 0x200f or >= 0x202a and <= 0x202e or >= 0x2060 and <= 0x206f
            or 0xe0001 or >= 0xe0020 and <= 0xe007f;

        private static int GetWidth(int value) => value switch
        {
            >= 0x1100 and <= 0x115f or 0x2329 or 0x232a or >= 0x231a and <= 0x231b or
            >= 0x23e9 and <= 0x23ec or 0x23f0 or 0x23f3 or 0x25fd or 0x25fe or 0x2614 or 0x2615 or
            >= 0x2648 and <= 0x2653 or 0x267f or 0x2693 or 0x26a1 or 0x26aa or 0x26ab or 0x26bd or 0x26be or
            0x26c4 or 0x26c5 or 0x26ce or 0x26d4 or 0x26ea or 0x26f2 or 0x26f3 or 0x26f5 or 0x26fa or 0x26fd or
            0x2705 or 0x270a or 0x270b or 0x2728 or 0x274c or 0x274e or >= 0x2753 and <= 0x2755 or 0x2757 or
            >= 0x2795 and <= 0x2797 or 0x27b0 or 0x27bf or 0x2b1b or 0x2b1c or 0x2b50 or 0x2b55 or
            >= 0x2e80 and <= 0xa4cf and not 0x303f or >= 0xac00 and <= 0xd7a3 or
            >= 0xf900 and <= 0xfaff or >= 0xfe10 and <= 0xfe19 or >= 0xfe30 and <= 0xfe6f or
            >= 0xff01 and <= 0xff60 or >= 0xffe0 and <= 0xffe6 or >= 0x1b000 and <= 0x1b122 or
            0x1f004 or 0x1f0cf or 0x1f18e or >= 0x1f191 and <= 0x1f19a or
            >= 0x1f1e6 and <= 0x1f1ff or >= 0x1f200 and <= 0x1f202 or
            >= 0x1f210 and <= 0x1f23b or >= 0x1f240 and <= 0x1f248 or >= 0x1f250 and <= 0x1f251 or
            >= 0x1f300 and <= 0x1f320 or >= 0x1f32d and <= 0x1faff or >= 0x20000 and <= 0x3fffd => 2,
            _ => 1
        };

        private bool TryAppendJoinedRune(Rune rune)
        {
            if (!TryGetPreviousGlyph(out var row, out int column))
                return false;

            string? previous = row.Cells[column].Glyph;
            if (previous == null)
                return false;

            bool joiner = previous.EndsWith('\u200d');
            bool flag = rune.Value is >= 0x1f1e6 and <= 0x1f1ff
                && Rune.GetRuneAt(previous, 0).Value is >= 0x1f1e6 and <= 0x1f1ff
                && (previous.Length == 2 || previous.Length == 3 && joiner);
            if (!joiner && !flag)
                return false;

            string combined = string.Concat(previous, rune.ToString());
            if (!flag && StringInfo.GetNextTextElementLength(combined) != combined.Length)
                return false;

            if (previous.Length < 64)
            {
                row.Cells[column].Glyph = combined;
                row.Invalidate();
            }
            return true;
        }

        private bool TryGetPreviousGlyph(out ScreenRow row, out int column)
        {
            row = _active[_active.Row];
            column = _active.WrapPending ? _active.Col : _active.Col - 1;
            if (column < 0 && _active.Row > 0 && _active[_active.Row - 1].Wrapped)
            {
                row = _active[_active.Row - 1];
                column = row.WrapColumn - 1;
            }
            if (column >= 0 && row.Cells[column].IsContinuation)
                column--;
            return column >= 0;
        }

        private void AppendGlyph(ScreenRow row, int column, Rune rune)
        {
            ref var cell = ref row.Cells[column];
            // A malformed stream must not grow one cell indefinitely.
            if (cell.Glyph is { Length: >= 64 })
                return;

            cell.Glyph = string.Concat(cell.Glyph ?? cell.Char.ToString(), rune.ToString());
            row.Invalidate();
            if (rune.Value == 0xfe0f && cell.DisplayWidth == 1 && Cols > 1)
                WidenGlyph(row, column, cell);
        }

        private void WidenGlyph(ScreenRow row, int column, TerminalCell cell)
        {
            bool cursorFollows = ReferenceEquals(row, _active[_active.Row])
                && (_active.WrapPending ? _active.Col : _active.Col - 1) == column;
            if (column == Cols - 1)
            {
                if (!cursorFollows || !_active.WrapPending) return;

                // The variation selector can turn the last single cell into
                // a double-width glyph, so place it on the next physical row.
                row.Cells[column] = EraseCell;
                row.SetWrapColumn(column);
                row.Invalidate();
                _active.Col = 0;
                LineFeed();
                row = _active[_active.Row];
                column = 0;
            }

            ClearWideBoundary(row, column + 2, EraseCell);
            StoreGlyph(row, column, cell, 2);
            if (cursorFollows)
                AdvanceCursor(column + 2);
        }

        private void WriteGlyph(char character, string? glyph, int width)
        {
            width = Math.Min(width, Cols);
            if (_active.WrapPending || width == 2 && _active.Col == Cols - 1 && _autoWrap)
            {
                var previousRow = _active[_active.Row];
                if (!_active.WrapPending)
                    EraseRange(previousRow, _active.Col, Cols, EraseCell);
                previousRow.SetWrapColumn(_active.WrapPending ? Cols : _active.Col);
                _active.Col = 0;
                LineFeed();
            }

            width = Math.Min(width, Cols - _active.Col);
            var row = _active[_active.Row];
            int col = _active.Col;
            if (_insertMode)
                EditCharacters('@', width);
            else
            {
                ClearWideBoundary(row, col, EraseCell);
                ClearWideBoundary(row, col + width, EraseCell);
            }

            var cell = _style;
            cell.Char = character;
            cell.Glyph = glyph;
            StoreGlyph(row, col, cell, width);
            AdvanceCursor(col + width);
        }

        private static void StoreGlyph(ScreenRow row, int col, TerminalCell cell, int width)
        {
            cell.Width = width == 2 ? (byte)2 : (byte)0;
            row.Cells[col] = cell;
            if (width == 2)
                row.Cells[col + 1] = new TerminalCell { Width = 1, Fg = cell.Fg, Bg = cell.Bg, Flags = cell.Flags };

            row.ExtendWrapColumn(col + width);
            row.Invalidate();
        }

        private void AdvanceCursor(int nextColumn)
        {
            _active.Col = Math.Min(nextColumn, Cols - 1);
            _active.WrapPending = _autoWrap && nextColumn == Cols;
        }

        private void WriteAscii(ReadOnlySpan<char> text)
        {
            var style = _style;
            var blank = EraseCell;
            int cols = Cols;
            while (!text.IsEmpty)
            {
                if (_active.WrapPending)
                {
                    _active[_active.Row].SetWrapColumn(cols);
                    _active.Col = 0;
                    LineFeed();
                }

                int col = _active.Col;
                int count = Math.Min(text.Length, cols - col);
                int end = col + count;
                var row = _active[_active.Row];
                ClearWideBoundary(row, col, blank);
                ClearWideBoundary(row, end, blank);
                for (int offset = 0; offset < count; offset++)
                {
                    style.Char = text[offset];
                    row.Cells[col + offset] = style;
                }

                text = text[count..];
                if (!_autoWrap && !text.IsEmpty)
                {
                    style.Char = text[^1];
                    row.Cells[^1] = style;
                    text = [];
                }

                row.ExtendWrapColumn(end);
                row.Invalidate();
                _active.Col = Math.Min(end, cols - 1);
                _active.WrapPending = _autoWrap && end == cols;
            }
        }

        private void StartCsi()
        {
            _parserState = ParserState.Csi;
            _csiPrefix = '\0';
            _csiIntermediate = false;
            _parameterCount = 1;
            _parameters[0] = -1;
            _separators[0] = '\0';
        }

        private void StartControlString(bool osc)
        {
            _parserState = ParserState.ControlString;
            _osc = osc;
        }

        private void ReadControlString(char c)
        {
            if (c == '\u009c' || _osc && c == '\a' || _parserState == ParserState.ControlStringEscape && c == '\\')
                _parserState = ParserState.Ground;
            else
                _parserState = c == '\x1b' ? ParserState.ControlStringEscape : ParserState.ControlString;
        }

        private void ReadEscape(char c)
        {
            if (c < ' ')
            {
                if (c == '\x1b')
                    _parserState = ParserState.Escape;
                else
                    ExecuteControl(c);
                return;
            }
            if (c is >= ' ' and <= '/')
            {
                _escapeIntermediate = _parserState == ParserState.Escape ? c : '\0';
                _parserState = ParserState.EscapeIntermediate;
                return;
            }

            if (c == '\x7f') return;
            char intermediate = _parserState == ParserState.EscapeIntermediate ? _escapeIntermediate : '\0';
            bool hasIntermediate = _parserState == ParserState.EscapeIntermediate;
            _parserState = ParserState.Ground;
            if (hasIntermediate)
            {
                if (intermediate == '(' && c is '0' or 'B')
                    _g0LineDrawing = c == '0';
                else if (intermediate == ')' && c is '0' or 'B')
                    _g1LineDrawing = c == '0';
                return;
            }

            switch (c)
            {
                case '[': StartCsi(); break;
                case ']': StartControlString(true); break;
                case 'P' or 'X' or '^' or '_': StartControlString(false); break;
                case '7': SaveCursor(); break;
                case '8': RestoreCursor(); break;
                case 'M': ReverseIndex(); break;
                case 'D': LineFeed(); break;
                case 'E': _active.Col = 0; LineFeed(); break;
                case 'H': SetTabStop(_active.Col, true); break;
                case 'c': Reset(); break;
            }
        }

        private void ReadCsi(char c)
        {
            if (c is >= '@' and <= '~')
            {
                bool supported = _parserState == ParserState.Csi && !_csiIntermediate;
                _parserState = ParserState.Ground;
                if (supported) ExecuteCsi(c);
                return;
            }

            if (c == '\x1b')
            {
                _parserState = ParserState.Escape;
                return;
            }
            if (c < ' ')
            {
                ExecuteControl(c);
                return;
            }
            if (c == '\x7f' || _parserState == ParserState.CsiIgnore) return;

            if (c is >= ' ' and <= '/')
                _csiIntermediate = true;
            else if (c is >= '0' and <= '9' && !_csiIntermediate)
                _parameters[_parameterCount - 1] = Math.Min(MaxCsiValue, Math.Max(0, _parameters[_parameterCount - 1]) * 10 + c - '0');
            else if (c is ';' or ':' && !_csiIntermediate && _parameterCount < MaxCsiParameters)
            {
                _parameters[_parameterCount] = -1;
                _separators[_parameterCount++] = c;
            }
            else if (c is >= '<' and <= '?' && !_csiIntermediate && _csiPrefix == '\0' && _parameterCount == 1 && _parameters[0] < 0)
                _csiPrefix = c;
            else
                _parserState = ParserState.CsiIgnore;
        }

        private void ExecuteCsi(char command)
        {
            var parameters = _parameters.AsSpan(0, _parameterCount);
            if (_csiPrefix == '?')
            {
                if (command is 'h' or 'l')
                    foreach (int mode in parameters)
                        SetPrivateMode(mode, command == 'h');
                return;
            }
            if (_csiPrefix != '\0') return;

            int first = Math.Max(0, parameters[0]);
            int second = parameters.Length > 1 ? Math.Max(0, parameters[1]) : 0;
            int count = Math.Max(1, first);
            int top = _originMode ? _active.Top : 0;
            int bottom = _originMode ? _active.Bottom : Rows - 1;
            switch (command)
            {
                case 'A': MoveCursor(Math.Max(_active.Row - count, _active.Row >= _active.Top ? _active.Top : 0), _active.Col); break;
                case 'B' or 'e': MoveCursor(Math.Min(_active.Row + count, _active.Row <= _active.Bottom ? _active.Bottom : Rows - 1), _active.Col); break;
                case 'C' or 'a': MoveCursor(_active.Row, _active.Col + count); break;
                case 'D': MoveCursor(_active.Row, _active.Col - count); break;
                case 'E': MoveCursor(Math.Min(_active.Row + count, _active.Row <= _active.Bottom ? _active.Bottom : Rows - 1), 0); break;
                case 'F': MoveCursor(Math.Max(_active.Row - count, _active.Row >= _active.Top ? _active.Top : 0), 0); break;
                case 'G' or '\x60': MoveCursor(_active.Row, count - 1); break;
                case 'H' or 'f': MoveCursor(Math.Min(top + count - 1, bottom), Math.Max(0, second - 1)); break;
                case 'I': MoveTab(count, 1); break;
                case 'd': MoveCursor(Math.Min(top + count - 1, bottom), _active.Col); break;
                case 'J': EraseDisplay(first); break;
                case 'K': EraseLine(first); break;
                case 'P' or '@' or 'X': EditCharacters(command, count); break;
                case 'L':
                    if (_active.Row >= _active.Top && _active.Row <= _active.Bottom)
                        ScrollDown(_active.Row, _active.Bottom, count);
                    break;
                case 'M':
                    if (_active.Row >= _active.Top && _active.Row <= _active.Bottom)
                        ScrollUp(_active.Row, _active.Bottom, count, false);
                    break;
                case 'S': ScrollUp(_active.Top, _active.Bottom, count, true); break;
                case 'T': ScrollDown(_active.Top, _active.Bottom, count); break;
                case 'Z': MoveTab(count, -1); break;
                case 'c' when first == 0: ResponseReady?.Invoke("\x1b[?1;2c"); break;
                case 'g' when first == 0: SetTabStop(_active.Col, false); break;
                case 'g' when first == 3: _defaultTabStops = false; _tabOverrides.Clear(); break;
                case 'm': SetRendition(parameters); break;
                case 'n' when first == 5: ResponseReady?.Invoke("\x1b[0n"); break;
                case 'n' when first == 6:
                    ResponseReady?.Invoke(FormattableString.Invariant($"\x1b[{_active.Row - top + 1};{_active.Col + 1}R"));
                    break;
                case 'r': SetMargins(count - 1, second == 0 ? Rows - 1 : second - 1); break;
                case 's': SaveCursor(); break;
                case 'u': RestoreCursor(); break;
                case 'h' or 'l':
                    if (parameters.Contains(4))
                        _insertMode = command == 'h';
                    break;
            }
        }

        private void SetPrivateMode(int mode, bool enabled)
        {
            switch (mode)
            {
                case 1: ApplicationCursorKeys = enabled; break;
                case 6:
                    _originMode = enabled;
                    MoveCursor(enabled ? _active.Top : 0, 0);
                    break;
                case 7:
                    _autoWrap = enabled;
                    if (!enabled) _active.WrapPending = false;
                    break;
                case 25: CursorVisible = enabled; break;
                case 47 or 1047 or 1049:
                    if (enabled && !IsAlternateScreen)
                    {
                        if (mode == 1049) SaveCursor();
                        _alternate = new Screen(Cols, Rows);
                        _active = _alternate;
                    }
                    else if (!enabled && IsAlternateScreen)
                    {
                        int cols = Cols;
                        int rows = Rows;
                        _active = _primary;
                        _alternate = null;
                        ResizePrimary(cols, rows);
                        if (mode == 1049) RestoreCursor();
                    }
                    break;
                case 1048:
                    if (enabled) SaveCursor();
                    else RestoreCursor();
                    break;
                case 2004: BracketedPaste = enabled; break;
                case 9001: Win32InputMode = enabled; break;
            }
        }

        private void SetMargins(int top, int bottom)
        {
            if (top >= bottom || bottom >= Rows)
                return;

            _active.Top = top;
            _active.Bottom = bottom;
            MoveCursor(_originMode ? top : 0, 0);
        }

        private void MoveCursor(int row, int column)
        {
            _active.Row = Math.Clamp(row, 0, Rows - 1);
            _active.Col = Math.Clamp(column, 0, Cols - 1);
            _active.WrapPending = false;
        }

        private void SaveCursor()
        {
            _active.Saved = new CursorState(_active.Row, _active.Col, _active.WrapPending, _style, _originMode, _g0LineDrawing, _g1LineDrawing, _useG1);
        }

        private void RestoreCursor()
        {
            var saved = _active.Saved;
            MoveCursor(saved.Row, saved.Col);
            _active.WrapPending = saved.WrapPending && _autoWrap && _active.Col == Cols - 1;
            _style = saved.Style;
            _originMode = saved.OriginMode;
            _g0LineDrawing = saved.G0LineDrawing;
            _g1LineDrawing = saved.G1LineDrawing;
            _useG1 = saved.UseG1;
        }

        private void LineFeed()
        {
            _active.WrapPending = false;
            if (_active.Row == _active.Bottom)
                ScrollUp(_active.Top, _active.Bottom, 1, true);
            else if (_active.Row < Rows - 1)
                _active.Row++;
        }

        private void ReverseIndex()
        {
            _active.WrapPending = false;
            if (_active.Row == _active.Top)
                ScrollDown(_active.Top, _active.Bottom, 1);
            else if (_active.Row > 0)
                _active.Row--;
        }

        private void ScrollUp(int top, int bottom, int count, bool captureHistory)
        {
            int lines = Math.Min(count, bottom - top + 1);
            var blank = EraseCell;
            for (int i = 0; i < lines; i++)
            {
                var recycled = _active[top];
                if (top == 0 && bottom == Rows - 1)
                {
                    if (captureHistory && !IsAlternateScreen)
                        EnqueueScrollback(recycled.Snapshot);
                    _active.Rotate(1);
                }
                else
                {
                    for (int row = top; row < bottom; row++)
                        _active[row] = _active[row + 1];
                    _active[bottom] = recycled;
                }
                recycled.Clear(blank);
            }
        }

        private void ScrollDown(int top, int bottom, int count)
        {
            int lines = Math.Min(count, bottom - top + 1);
            var blank = EraseCell;
            for (int i = 0; i < lines; i++)
            {
                var recycled = _active[bottom];
                if (top == 0 && bottom == Rows - 1)
                    _active.Rotate(-1);
                else
                {
                    for (int row = bottom; row > top; row--)
                        _active[row] = _active[row - 1];
                    _active[top] = recycled;
                }
                recycled.Clear(blank);
            }
        }

        private void EnqueueScrollback(TerminalLine line)
        {
            _scrollback.Add(line);
            if (_scrollback.Count - _scrollbackStart > MaxScrollbackLines)
            {
                _scrollbackStart++;
                ScrollbackDiscarded++;
            }

            // Published snapshots retain their original list and bounds. The active list
            // can be appended to without copying thousands of unchanged rows per frame.
            if (_scrollbackStart >= ScrollbackCompactionBatch)
            {
                _scrollback = _scrollback.GetRange(_scrollbackStart, MaxScrollbackLines);
                _scrollbackStart = 0;
            }
        }

        private TerminalCell EraseCell => new()
        {
            Char = ' ',
            Fg = _style.IsInverse ? _style.Fg : 0,
            Bg = _style.Bg,
            Flags = _style.IsInverse ? (byte)4 : (byte)0
        };

        private static void ClearWideBoundary(ScreenRow row, int column, TerminalCell blank)
        {
            if (column <= 0 || column >= row.Cells.Length || !row.Cells[column].IsContinuation)
                return;

            row.Cells[column - 1] = blank;
            row.Cells[column] = blank;
        }

        private static void EraseRange(ScreenRow row, int start, int end, TerminalCell blank)
        {
            ClearWideBoundary(row, start, blank);
            ClearWideBoundary(row, end, blank);
            row.Cells.AsSpan(start, end - start).Fill(blank);
            if (end == row.Cells.Length)
                row.SetWrapColumn(0);
            row.Invalidate();
        }

        private void EraseDisplay(int mode)
        {
            var blank = EraseCell;
            switch (mode)
            {
                case 0:
                    EraseRange(_active[_active.Row], _active.Col, Cols, blank);
                    for (int r = _active.Row + 1; r < Rows; r++)
                        _active[r].Clear(blank);
                    break;
                case 1:
                    for (int r = 0; r < _active.Row; r++)
                        _active[r].Clear(blank);
                    EraseRange(_active[_active.Row], 0, _active.Col + 1, blank);
                    break;
                case 2:
                    for (int r = 0; r < Rows; r++)
                        _active[r].Clear(blank);
                    break;
                case 3 when !IsAlternateScreen: ClearScrollback(); break;
            }
            _active.WrapPending = false;
        }

        private void EraseLine(int mode)
        {
            var row = _active[_active.Row];
            var blank = EraseCell;
            switch (mode)
            {
                case 0: EraseRange(row, _active.Col, Cols, blank); break;
                case 1: EraseRange(row, 0, _active.Col + 1, blank); break;
                case 2: row.Clear(blank); break;
            }
            _active.WrapPending = false;
        }

        private void EditCharacters(char command, int count)
        {
            var row = _active[_active.Row];
            int col = _active.Col;
            count = Math.Min(count, Cols - col);
            var blank = EraseCell;
            switch (command)
            {
                case '@':
                    ClearWideBoundary(row, col, blank);
                    ClearWideBoundary(row, Cols - count, blank);
                    Array.Copy(row.Cells, col, row.Cells, col + count, Cols - col - count);
                    row.Cells.AsSpan(col, count).Fill(blank);
                    break;
                case 'P':
                    ClearWideBoundary(row, col, blank);
                    ClearWideBoundary(row, col + count, blank);
                    Array.Copy(row.Cells, col + count, row.Cells, col, Cols - col - count);
                    row.Cells.AsSpan(Cols - count).Fill(blank);
                    break;
                case 'X': EraseRange(row, col, col + count, blank); break;
            }
            row.Invalidate();
            _active.WrapPending = false;
        }

        private void SetRendition(ReadOnlySpan<int> parameters)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                int end = i + 1;
                while (end < parameters.Length && _separators[end] == ':')
                    end++;

                switch (Math.Max(0, parameters[i]))
                {
                    case 0: _style = TerminalCell.Default; break;
                    case 1: _style.Flags |= 1; break;
                    case 2: _style.Flags |= 8; break;
                    case 3: _style.Flags |= 16; break;
                    case 4: _style.Flags |= 2; break;
                    case 7: _style.Flags |= 4; break;
                    case 22: _style.Flags &= unchecked((byte)~(1 | 8)); break;
                    case 23: _style.Flags &= unchecked((byte)~16); break;
                    case 24: _style.Flags &= unchecked((byte)~2); break;
                    case 27: _style.Flags &= unchecked((byte)~4); break;
                    case >= 30 and <= 37: _style.Fg = parameters[i] - 30 + 1; break;
                    case 38: if (TryReadColor(parameters, i, ref end, out int fg)) _style.Fg = fg; break;
                    case 39: _style.Fg = 0; break;
                    case >= 40 and <= 47: _style.Bg = parameters[i] - 40 + 1; break;
                    case 48: if (TryReadColor(parameters, i, ref end, out int bg)) _style.Bg = bg; break;
                    case 49: _style.Bg = 0; break;
                    case 58: TryReadColor(parameters, i, ref end, out _); break; // Underline color.
                    case >= 90 and <= 97: _style.Fg = parameters[i] - 90 + 9; break;
                    case >= 100 and <= 107: _style.Bg = parameters[i] - 100 + 9; break;
                }
                i = end - 1;
            }
        }

        private bool TryReadColor(ReadOnlySpan<int> parameters, int start, ref int end, out int color)
        {
            color = 0;
            if (start + 1 >= parameters.Length) return false;
            int mode = parameters[start + 1];
            int channelCount = mode == 2 ? 3 : mode == 5 ? 1 : 0;
            if (channelCount == 0) return false;

            int firstChannel = start + 2;
            if (_separators[start + 1] == ':')
            {
                if (mode == 2 && end - firstChannel == 4) firstChannel++; // Optional color space.
                if (end - firstChannel != channelCount) return false;
            }
            else
            {
                end = Math.Min(parameters.Length, firstChannel + channelCount);
                if (end - firstChannel != channelCount) return false;
            }

            var channels = parameters.Slice(firstChannel, channelCount);
            foreach (int channel in channels)
                if (channel is < 0 or > 255) return false;

            color = mode == 5 ? channels[0] + 1 : 0x1000000 | (channels[0] << 16) | (channels[1] << 8) | channels[2];
            return true;
        }

        public void Resize(int cols, int rows)
        {
            cols = Math.Max(1, cols);
            rows = Math.Max(1, rows);
            if (cols == Cols && rows == Rows)
                return;

            if (_alternate != null)
            {
                // Full-screen apps own their layout. Apply only the last size to the
                // document-like primary buffer once the application returns to it.
                _alternate.Resize(cols, rows, Math.Max(0, _alternate.Row - rows + 1));
                return;
            }
            ResizePrimary(cols, rows);
        }

        private void ResizePrimary(int cols, int rows)
        {
            if (cols != _primary.Cols)
                ReflowPrimary(cols, rows);
            else if (rows != _primary.Rows)
                ResizePrimaryHeight(rows);
        }

        private void ResizePrimaryHeight(int rows)
        {
            int droppedRows = Math.Min(_primary.Row, Math.Max(0, LastUsedRow(_primary) - rows + 1));
            for (int row = 0; row < droppedRows; row++)
                EnqueueScrollback(_primary[row].Snapshot);

            _primary.Resize(_primary.Cols, rows, droppedRows);
        }

        private static int LastUsedRow(Screen screen)
        {
            int last = screen.Rows - 1;
            while (last > screen.Row && screen[last].Snapshot.Length == 0)
                last--;
            return last;
        }

        private void ReflowPrimary(int cols, int rows)
        {
            var screen = _primary;
            int lastSourceRow = LastUsedRow(screen);
            int cursorSourceCol = screen.Col + (screen.WrapPending ? 1 : 0);
            int savedSourceCol = Math.Clamp(screen.Saved.Col, 0, screen.Cols - 1) + (screen.Saved.WrapPending ? 1 : 0);
            int savedSourceRow = Math.Min(screen.Saved.Row, lastSourceRow);
            var target = new ScreenRow(cols);
            int targetColumn = 0;
            int targetRow = 0;
            ReflowPosition cursor = new(-1, 0, false);
            ReflowPosition saved = new(-1, 0, false);

            // Reflow only the mutable viewport. Older rows belong to scrollback;
            // ConPTY cannot position its cursor into those rows after a resize.
            int retention = MaxScrollbackLines + rows;
            var reflowed = new Queue<ScreenRow>();
            for (int sourceRow = 0; sourceRow <= lastSourceRow; sourceRow++)
            {
                var source = screen[sourceRow];
                int cursorColumn = sourceRow == screen.Row ? cursorSourceCol : -1;
                int savedColumn = sourceRow == savedSourceRow ? savedSourceCol : -1;
                int contentEnd = source.Snapshot.TextColumns;
                int end = Math.Max(contentEnd, Math.Max(cursorColumn, savedColumn));
                for (int sourceCol = 0; sourceCol < end;)
                {
                    var cell = source.Cells[sourceCol];
                    int sourceWidth = cell.DisplayWidth;
                    int width = Math.Min(sourceWidth, cols);
                    if (targetColumn + width > cols)
                        CompleteRow(targetColumn);
                    if (cursor.Row >= 0 && targetRow >= cursor.Row + rows)
                        break;

                    if (cursorColumn >= sourceCol && cursorColumn < sourceCol + sourceWidth)
                        cursor = PositionAt(targetColumn + Math.Min(cursorColumn - sourceCol, width - 1));
                    if (savedColumn >= sourceCol && savedColumn < sourceCol + sourceWidth)
                        saved = PositionAt(targetColumn + Math.Min(savedColumn - sourceCol, width - 1));

                    cell.Width = width == 2 ? (byte)2 : (byte)0;
                    target.Cells[targetColumn] = cell;
                    if (width == 2)
                        target.Cells[targetColumn + 1] = source.Cells[sourceCol + 1];

                    targetColumn += width;
                    sourceCol += sourceWidth;
                }

                if (cursor.Row >= 0 && targetRow >= cursor.Row + rows)
                    break;
                if (savedColumn == end)
                    saved = targetColumn == cols && !screen.Saved.WrapPending
                        ? new ReflowPosition(targetRow + 1, 0, false)
                        : PositionAt(targetColumn);
                if (cursorColumn == end)
                {
                    // ConPTY advances an insertion point that was already beyond the
                    // last character. A genuine deferred wrap remains at the margin.
                    if (targetColumn == cols && !screen.WrapPending)
                        CompleteRow(cols);
                    cursor = PositionAt(targetColumn);
                }

                if (source.WrapColumn == 0 || sourceRow == lastSourceRow)
                    CompleteRow(0);
            }

            int firstRetained = targetRow - reflowed.Count;
            int firstVisible = Math.Min(cursor.Row, Math.Max(0, targetRow - rows));
            if (firstRetained > 0)
            {
                ClearScrollback();
                ScrollbackDiscarded += firstRetained;
            }

            var viewport = new ScreenRow[rows];
            for (int row = firstRetained; row < firstVisible; row++)
                EnqueueScrollback(reflowed.Dequeue().Snapshot);
            for (int row = 0; row < rows; row++)
                viewport[row] = reflowed.TryDequeue(out var available) ? available : new ScreenRow(cols);

            screen.ReplaceRows(cols, viewport, firstVisible);
            screen.Row = cursor.Row - firstVisible;
            screen.Col = cursor.Col;
            screen.WrapPending = _autoWrap && cursor.WrapPending;
            screen.Saved = screen.Saved with
            {
                Row = Math.Clamp(saved.Row - firstVisible, 0, rows - 1),
                Col = saved.Col,
                WrapPending = saved.WrapPending
            };

            ReflowPosition PositionAt(int column) => new(targetRow, Math.Min(column, cols - 1), column == cols);

            void CompleteRow(int wrapColumn)
            {
                target.SetWrapColumn(wrapColumn);
                reflowed.Enqueue(target);
                targetRow++;
                // Reuse rows already displaced beyond the entire scrollback limit.
                if (reflowed.Count > retention)
                {
                    target = reflowed.Dequeue();
                    target.Clear(TerminalCell.Default);
                }
                else
                {
                    target = new ScreenRow(cols);
                }
                targetColumn = 0;
            }
        }

        public void ClearScrollback()
        {
            ScrollbackDiscarded += _scrollback.Count - _scrollbackStart;
            _scrollback = new List<TerminalLine>();
            _scrollbackStart = 0;
        }

        public void ClearDisplayedOutput()
        {
            if (!IsAlternateScreen)
                ClearScrollback();

            int first = _active.Row;
            int last = _active.Row;
            while (first > 0 && _active[first - 1].Wrapped)
                first--;
            while (last < Rows - 1 && _active[last].Wrapped)
                last++;

            for (int row = 0; row < first; row++)
                _active[row].Clear(TerminalCell.Default);
            for (int row = last + 1; row < Rows; row++)
                _active[row].Clear(TerminalCell.Default);
        }

        public void Clear()
        {
            ClearScrollback();
            _active.Clear();
            _style = TerminalCell.Default;
        }

        private void Reset()
        {
            ClearScrollback();
            if (_alternate != null)
                _primary.Resize(Cols, Rows, 0);
            _primary.Clear();
            _active = _primary;
            _alternate = null;
            _style = TerminalCell.Default;
            _autoWrap = true;
            _originMode = false;
            _insertMode = false;
            _g0LineDrawing = _g1LineDrawing = _useG1 = false;
            _defaultTabStops = true;
            _tabOverrides.Clear();
            CursorVisible = true;
            ApplicationCursorKeys = false;
            BracketedPaste = false;
            Win32InputMode = false;
        }

        public (IReadOnlyList<TerminalLine> lines, int cursorLine, int cursorCol) RenderLines()
        {
            int lastRow = LastUsedRow(_active);

            var screen = new TerminalLine[lastRow + 1];
            for (int row = 0; row <= lastRow; row++)
                screen[row] = _active[row].Snapshot;

            int historyCount = ScrollbackCount;
            IReadOnlyList<TerminalLine> lines = historyCount == 0
                ? screen
                : new LineSnapshot(_scrollback, _scrollbackStart, historyCount, screen);
            return (lines, historyCount + _active.Row, _active.Col);
        }

        public string Render() => string.Join("\r\n", RenderLines().lines.Select(line => line.Text));

        private sealed class LineSnapshot(List<TerminalLine> history, int start, int historyCount, TerminalLine[] screen) : IReadOnlyList<TerminalLine>
        {
            public int Count => historyCount + screen.Length;
            public TerminalLine this[int index] => (uint)index >= (uint)Count
                ? throw new ArgumentOutOfRangeException(nameof(index))
                : index < historyCount ? history[start + index] : screen[index - historyCount];

            public IEnumerator<TerminalLine> GetEnumerator()
            {
                for (int row = 0; row < historyCount; row++)
                    yield return history[start + row];
                foreach (var line in screen)
                    yield return line;
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class ScreenRow(int cols)
        {
            private TerminalLine? _snapshot;
            public TerminalCell[] Cells { get; } = CreateCells(cols);
            public int WrapColumn { get; private set; }
            public bool Wrapped => WrapColumn > 0;
            public TerminalLine Snapshot => _snapshot ??= new TerminalLine(Cells, WrapColumn);

            private static TerminalCell[] CreateCells(int cols)
            {
                var cells = new TerminalCell[cols];
                Array.Fill(cells, TerminalCell.Default);
                return cells;
            }

            public void Invalidate() => _snapshot = null;
            public void SetWrapColumn(int column)
            {
                if (column == WrapColumn) return;
                WrapColumn = column;
                Invalidate();
            }

            public void ExtendWrapColumn(int column)
            {
                if (Wrapped && column > WrapColumn)
                    SetWrapColumn(column);
            }

            public void Clear(TerminalCell blank)
            {
                Array.Fill(Cells, blank);
                WrapColumn = 0;
                Invalidate();
            }
        }

        private readonly record struct CursorState(int Row, int Col, bool WrapPending, TerminalCell Style, bool OriginMode,
            bool G0LineDrawing = false, bool G1LineDrawing = false, bool UseG1 = false);

        private readonly record struct ReflowPosition(int Row, int Col, bool WrapPending);

        private sealed class Screen(int cols, int rows)
        {
            private ScreenRow[] _rows = CreateRows(cols, rows);
            private int _start;
            public int Cols { get; private set; } = cols;
            public int Rows => _rows.Length;
            public int Row;
            public int Col;
            public int Top;
            public int Bottom = rows - 1;
            public bool WrapPending;
            public CursorState Saved = new(0, 0, false, TerminalCell.Default, false);

            public ScreenRow this[int row]
            {
                get => _rows[(_start + row) % Rows];
                set => _rows[(_start + row) % Rows] = value;
            }

            public void Rotate(int delta)
            {
                _start = (_start + delta + Rows) % Rows;
            }

            private static ScreenRow[] CreateRows(int cols, int rows)
            {
                var result = new ScreenRow[rows];
                for (int row = 0; row < rows; row++)
                    result[row] = new ScreenRow(cols);
                return result;
            }

            public void Resize(int cols, int rows, int droppedRows)
            {
                var resized = new ScreenRow[rows];
                int copy = Math.Min(Cols, cols);
                int rowsToCopy = Math.Min(Rows - droppedRows, rows);
                for (int i = 0; i < rowsToCopy; i++)
                {
                    var old = this[droppedRows + i];
                    if (cols == Cols)
                        resized[i] = old;
                    else
                    {
                        resized[i] = new ScreenRow(cols);
                        int cellsToCopy = copy < Cols && old.Cells[copy].IsContinuation ? copy - 1 : copy;
                        Array.Copy(old.Cells, resized[i].Cells, cellsToCopy);
                        resized[i].SetWrapColumn(Math.Min(old.WrapColumn, cellsToCopy));
                    }
                }
                for (int i = rowsToCopy; i < rows; i++)
                    resized[i] = new ScreenRow(cols);

                bool wider = cols > Cols;
                if (wider && WrapPending) Col++;
                WrapPending = WrapPending && cols == Cols;

                ReplaceRows(cols, resized, droppedRows);
                Row = Math.Clamp(Row - droppedRows, 0, Rows - 1);
                Col = Math.Min(Col, Cols - 1);
                Saved = Saved with { Row = Math.Max(0, Saved.Row - droppedRows) };
            }

            public void ReplaceRows(int cols, ScreenRow[] resized, int droppedRows)
            {
                bool wholeScreen = Top == 0 && Bottom == Rows - 1;
                _rows = resized;
                _start = 0;
                Cols = cols;
                Top = wholeScreen ? 0 : Math.Clamp(Top - droppedRows, 0, Rows - 1);
                Bottom = wholeScreen ? Rows - 1 : Math.Clamp(Bottom - droppedRows, 0, Rows - 1);
                if (Top >= Bottom)
                {
                    Top = 0;
                    Bottom = Rows - 1;
                }
            }

            public void Clear()
            {
                foreach (var row in _rows)
                    row.Clear(TerminalCell.Default);
                Row = 0;
                Col = 0;
                Top = 0;
                Bottom = Rows - 1;
                WrapPending = false;
                Saved = new CursorState(0, 0, false, TerminalCell.Default, false);
            }
        }

        public static (byte R, byte G, byte B) GetAnsiColor(int index) => index switch
        {
            0 => (0, 0, 0),
            1 => (205, 49, 49),
            2 => (13, 188, 121),
            3 => (229, 229, 16),
            4 => (36, 114, 200),
            5 => (188, 63, 188),
            6 => (17, 168, 205),
            7 => (204, 204, 204),
            8 => (118, 118, 118),
            9 => (241, 76, 76),
            10 => (35, 209, 139),
            11 => (245, 245, 67),
            12 => (59, 142, 234),
            13 => (214, 112, 214),
            14 => (41, 184, 219),
            15 => (229, 229, 229),
            _ => (204, 204, 204)
        };

        public static (byte R, byte G, byte B) Get256Color(int index)
        {
            if (index < 16) return GetAnsiColor(index);
            if (index >= 232)
            {
                byte level = (byte)(8 + (index - 232) * 10);
                return (level, level, level);
            }

            int cube = index - 16;
            return (ColorLevel(cube / 36), ColorLevel(cube / 6 % 6), ColorLevel(cube % 6));
        }

        private static byte ColorLevel(int cubeIndex) => (byte)(cubeIndex == 0 ? 0 : 55 + cubeIndex * 40);
    }
}
