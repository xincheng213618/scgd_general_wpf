namespace ColorVision.UI.LogImp.Models
{
    public sealed class LogEntry
    {
        private static readonly char[] LineEndings = { '\r', '\n' };

        public LogEntry(string text, LogEntryLevel level)
        {
            Text = text.TrimEnd(LineEndings);
            Level = level;
        }

        public string Text { get; }

        public LogEntryLevel Level { get; }
    }
}
