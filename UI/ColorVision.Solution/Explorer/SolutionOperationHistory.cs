namespace ColorVision.Solution.Explorer
{
    internal sealed record SolutionOperationEntry(
        string Description,
        string BeforeSnapshot,
        string AfterSnapshot);

    /// <summary>
    /// Bounded per-solution operation history. Stack movement only occurs
    /// after a snapshot has been applied successfully.
    /// </summary>
    internal sealed class SolutionOperationHistory
    {
        private const int MaximumEntries = 100;
        private readonly List<SolutionOperationEntry> _undoEntries = new();
        private readonly List<SolutionOperationEntry> _redoEntries = new();

        public bool CanUndo => _undoEntries.Count > 0;
        public bool CanRedo => _redoEntries.Count > 0;
        public string? UndoDescription => _undoEntries.LastOrDefault()?.Description;
        public string? RedoDescription => _redoEntries.LastOrDefault()?.Description;

        public event EventHandler? Changed;

        public void Record(string description, string beforeSnapshot, string afterSnapshot)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            ArgumentException.ThrowIfNullOrWhiteSpace(beforeSnapshot);
            ArgumentException.ThrowIfNullOrWhiteSpace(afterSnapshot);
            if (string.Equals(beforeSnapshot, afterSnapshot, StringComparison.Ordinal))
                return;

            _undoEntries.Add(new SolutionOperationEntry(description, beforeSnapshot, afterSnapshot));
            if (_undoEntries.Count > MaximumEntries)
                _undoEntries.RemoveAt(0);
            _redoEntries.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public bool TryUndo(Func<string, bool> applySnapshot)
        {
            ArgumentNullException.ThrowIfNull(applySnapshot);
            if (_undoEntries.LastOrDefault() is not { } entry
                || !applySnapshot(entry.BeforeSnapshot))
            {
                return false;
            }

            _undoEntries.RemoveAt(_undoEntries.Count - 1);
            _redoEntries.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool TryRedo(Func<string, bool> applySnapshot)
        {
            ArgumentNullException.ThrowIfNull(applySnapshot);
            if (_redoEntries.LastOrDefault() is not { } entry
                || !applySnapshot(entry.AfterSnapshot))
            {
                return false;
            }

            _redoEntries.RemoveAt(_redoEntries.Count - 1);
            _undoEntries.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Clear()
        {
            if (_undoEntries.Count == 0 && _redoEntries.Count == 0)
                return;
            _undoEntries.Clear();
            _redoEntries.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
