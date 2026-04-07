using log4net;
using System.IO;
using System.Text.RegularExpressions;

namespace ColorVision.Solution.Terminal
{
    /// <summary>
    /// Context-aware persistent command history for the terminal.
    /// Maintains separate history lists per shell context (e.g., "shell", "python", "node").
    /// Detects the current context from the terminal prompt line.
    /// Each context persists to its own file so history survives across sessions.
    /// </summary>
    internal class CommandHistory
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CommandHistory));

        private const int MaxHistory = 1000;
        private const string DefaultContext = "shell";

        private readonly string _baseDir;
        private readonly Dictionary<string, ContextHistory> _contexts = new();
        private string _currentContext = DefaultContext;

        public string CurrentContext => _currentContext;

        public CommandHistory()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baseDir = Path.Combine(appData, "ColorVision");
            Directory.CreateDirectory(_baseDir);
            EnsureContext(DefaultContext);
        }

        /// <summary>
        /// Detect the current context from the terminal prompt line and switch to it.
        /// Call this before Add/Navigate operations.
        /// </summary>
        public void DetectContext(string promptLine)
        {
            string context = ClassifyPrompt(promptLine);
            if (context != _currentContext)
            {
                _currentContext = context;
                EnsureContext(context);
            }
        }

        /// <summary>
        /// Add a command to the current context's history.
        /// </summary>
        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            var ctx = GetCurrentContext();

            if (ctx.History.Count > 0 && ctx.History[^1] == command)
            {
                ResetNavigation();
                return;
            }

            ctx.History.Add(command);
            if (ctx.History.Count > MaxHistory)
                ctx.History.RemoveAt(0);

            ResetNavigation();
            Save(_currentContext);
        }

        public string? NavigateUp(string currentInput)
        {
            var ctx = GetCurrentContext();
            if (ctx.History.Count == 0) return null;

            if (ctx.Index == ctx.History.Count)
                ctx.SavedCurrent = currentInput;

            if (ctx.Index > 0)
            {
                ctx.Index--;
                return ctx.History[ctx.Index];
            }
            return null;
        }

        public string? NavigateDown()
        {
            var ctx = GetCurrentContext();
            if (ctx.Index >= ctx.History.Count) return null;

            ctx.Index++;
            if (ctx.Index == ctx.History.Count)
                return ctx.SavedCurrent;

            return ctx.History[ctx.Index];
        }

        public void ResetNavigation()
        {
            var ctx = GetCurrentContext();
            ctx.Index = ctx.History.Count;
            ctx.SavedCurrent = "";
        }

        /// <summary>
        /// Classify a prompt line into a context name.
        /// </summary>
        private static string ClassifyPrompt(string promptLine)
        {
            string trimmed = promptLine.TrimStart();

            // Python REPL: ">>> " or "... " (continuation)
            if (trimmed.StartsWith(">>>") || trimmed.StartsWith("..."))
                return "python";

            // IPython / Jupyter: "In [1]: "
            if (Regex.IsMatch(trimmed, @"^In \[\d+\]:"))
                return "python";

            // Node.js REPL: single "> " at start (very short prompt)
            // Be conservative: only if line is just "> " with possible trailing input
            if (Regex.IsMatch(promptLine, @"^> \S") && promptLine.Length < 200
                && !promptLine.Contains('\\') && !promptLine.Contains('/'))
                return "node";

            // Lua: just "> " similar to node, but if we detect lua-specific...
            // SQLite: "sqlite> "
            if (trimmed.StartsWith("sqlite>"))
                return "sqlite";

            // MySQL: "mysql> "
            if (trimmed.StartsWith("mysql>"))
                return "mysql";

            // Ruby IRB: "irb(main):001:0> "
            if (Regex.IsMatch(trimmed, @"^irb\("))
                return "ruby";

            // Everything else (CMD, PowerShell, bash, etc.) → shell
            return DefaultContext;
        }

        private ContextHistory GetCurrentContext()
        {
            EnsureContext(_currentContext);
            return _contexts[_currentContext];
        }

        private void EnsureContext(string context)
        {
            if (_contexts.ContainsKey(context)) return;

            var ctx = new ContextHistory();
            _contexts[context] = ctx;
            Load(context);
            ctx.Index = ctx.History.Count;
        }

        private string GetFilePath(string context)
        {
            // "shell" → terminal_history.txt (backward compatible)
            // others → terminal_history_python.txt, etc.
            string fileName = context == DefaultContext
                ? "terminal_history.txt"
                : $"terminal_history_{context}.txt";
            return Path.Combine(_baseDir, fileName);
        }

        private void Load(string context)
        {
            try
            {
                string path = GetFilePath(context);
                if (!File.Exists(path)) return;
                var ctx = _contexts[context];
                var lines = File.ReadAllLines(path);
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                        ctx.History.Add(line);
                }
                while (ctx.History.Count > MaxHistory)
                    ctx.History.RemoveAt(0);
            }
            catch (Exception ex)
            {
                log.Warn($"CommandHistory: failed to load context '{context}': {ex.Message}");
            }
        }

        private void Save(string context)
        {
            try
            {
                if (!_contexts.TryGetValue(context, out var ctx)) return;
                File.WriteAllLines(GetFilePath(context), ctx.History);
            }
            catch (Exception ex)
            {
                log.Warn($"CommandHistory: failed to save context '{context}': {ex.Message}");
            }
        }

        private sealed class ContextHistory
        {
            public List<string> History { get; } = new();
            public int Index { get; set; }
            public string SavedCurrent { get; set; } = "";
        }
    }
}
