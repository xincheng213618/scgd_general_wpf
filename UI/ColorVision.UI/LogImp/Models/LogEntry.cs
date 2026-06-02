namespace ColorVision.UI.LogImp.Models
{
    public sealed class LogEntry
    {
        public LogEntry(string text, LogEntryLevel level)
        {
            Text = text.TrimEnd('\r', '\n');
            Level = level;
        }

        public string Text { get; }

        public LogEntryLevel Level { get; }
    }
}
