using ColorVision.Solution.Terminal;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.Solution.Editor.AvalonEditor
{
    /// <summary>
    /// Workspace text editor with file-aware syntax highlighting and Python execution.
    /// </summary>
    public partial class AvalonEditControll : UserControl, IDisposable, IEditorDocumentContent, IResourcePathAwareDocumentContent, IReloadableEditorDocumentContent
    {
        public static RoutedUICommand RunPythonCommand { get; } = new(
            "运行 Python",
            nameof(RunPythonCommand),
            typeof(AvalonEditControll));

        private DispatcherTimer? _foldingUpdateTimer;
        private AvalonEditorThemeController? _themeController;
        private EditorTypingAssistance? _typingAssistance;
        private FoldingManager? _foldingManager;
        private IHighlightingDefinition? _highlightingDefinition;
        private bool _foldingPending;
        private CancellationTokenSource? _foldingCancellation;
        private readonly EditorPreferences _preferences;
        private readonly EditorIndentGuides _indentGuides = new();
        private double _zoom = 1;
        private string? _currentFileName;
        private bool _isUpdatingHighlightingSelection;
        private bool _isFormatted;
        private bool _isDirty;
        private bool _disposed;

        public string OriginalText { get; private set; } = string.Empty;

        public bool IsDirty => _isDirty;
        public bool CanSave => !string.IsNullOrWhiteSpace(_currentFileName);
        public event EventHandler? DocumentStateChanged;

        public AvalonEditControll()
            : this(ConfigService.Instance?.GetRequiredService<EditorPreferences>() ?? new EditorPreferences())
        {
        }

        internal AvalonEditControll(EditorPreferences preferences)
        {
            _preferences = preferences;
            InitializeComponent();
            InitializeEditor();
        }

        public AvalonEditControll(string currentFileName)
            : this()
        {
            OpenFile(currentFileName);
        }

        public bool OpenFile(string currentFileName)
        {
            if (string.IsNullOrWhiteSpace(currentFileName) || !File.Exists(currentFileName))
                return false;

            _currentFileName = Path.GetFullPath(currentFileName);
            return ReloadFromDisk();
        }

        private void InitializeEditor()
        {
            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
            textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;
            textEditor.TextChanged += TextEditor_TextChanged;
            UndoButton.CommandTarget = textEditor.TextArea;
            RedoButton.CommandTarget = textEditor.TextArea;
            SearchBar.Initialize(textEditor);
            textEditor.TextArea.TextView.BackgroundRenderers.Add(_indentGuides);
            textEditor.Document.UndoStack.PropertyChanged += UndoStateChanged;

            _themeController = new AvalonEditorThemeController(textEditor);
            _themeController.ColorsChanged += ThemeColorsChanged;
            Minimap.Initialize(textEditor, () => _highlightingDefinition);
            _typingAssistance = new EditorTypingAssistance(textEditor, _preferences, () => _highlightingDefinition?.Name);
            SearchBar.MatchesChanged += SearchMatchesChanged;
            SetSyntaxHighlighting(null);

            _foldingUpdateTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(300) };
            _foldingUpdateTimer.Tick += FoldingUpdateTimer_Tick;
            Loaded += Editor_Loaded;
            Unloaded += Editor_Unloaded;
            IsVisibleChanged += Editor_IsVisibleChanged;
            PreviewMouseWheel += Editor_PreviewMouseWheel;
            PropertyChangedEventManager.AddHandler(_preferences, PreferencesChanged, string.Empty);
            DataContext = _preferences;
            ApplyPreferences();
            InstallContextMenu();
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (_, e) => { EditorDocumentService.TrySaveDocument(this); e.Handled = true; },
                (_, e) => { e.CanExecute = CanSave && IsDirty; e.Handled = true; }));

            AddHandler(
                Keyboard.PreviewKeyDownEvent,
                new KeyEventHandler(AvalonEditControll_PreviewKeyDown),
                handledEventsToo: true);
            UpdateDocumentMetadata();
            UpdateRunButtonVisibility();
        }

        public void SetJsonText(string text)
        {
            OriginalText = text;
            try
            {
                var parsedJson = JToken.Parse(text);
                _isFormatted = text.Contains('\n') || text.Contains('\t');
                textEditor.Text = parsedJson.ToString(Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                textEditor.Text = text;
            }

            SetSyntaxHighlighting(HighlightingManager.Instance.GetDefinition("Json"));
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
            UpdateDocumentMetadata();
        }

        public string GetJsonText()
        {
            string text = textEditor.Text;
            try
            {
                var parsedJson = JToken.Parse(text);
                return parsedJson.ToString(_isFormatted ? Formatting.Indented : Formatting.None);
            }
            catch (JsonReaderException)
            {
                return OriginalText;
            }
        }

        public void NavigateTo(int lineNumber, int columnNumber = 1)
        {
            if (textEditor.Document == null || textEditor.Document.LineCount == 0)
                return;

            int targetLineNumber = Math.Clamp(lineNumber, 1, textEditor.Document.LineCount);
            var targetLine = textEditor.Document.GetLineByNumber(targetLineNumber);
            int targetColumn = Math.Clamp(columnNumber, 1, targetLine.Length + 1);
            RememberNavigation();
            textEditor.TextArea.Caret.Offset = targetLine.Offset + targetColumn - 1;
            textEditor.ScrollToLine(targetLineNumber);
            textEditor.Focus();
        }

        private void openFileClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog { CheckFileExists = true };
            if (dialog.ShowDialog() ?? false)
                ResourceOpenService.Instance.TryOpenWithFeedback(dialog.FileName);
        }

        public bool Save()
        {
            if (!CanSave)
                return false;

            textEditor.Save(_currentFileName!);
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
            return true;
        }

        public bool TryUpdateResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) || !File.Exists(resourcePath))
                return false;

            _currentFileName = Path.GetFullPath(resourcePath);
            ApplyFileSyntaxHighlighting();
            UpdateRunButtonVisibility();
            return true;
        }

        public bool ReloadFromDisk()
        {
            if (string.IsNullOrWhiteSpace(_currentFileName) || !File.Exists(_currentFileName))
                return false;

            ResetCodeNavigation();
            textEditor.Load(_currentFileName);
            string text = textEditor.Text;
            OriginalText = text;

            ApplyFileSyntaxHighlighting();
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
            UpdateDocumentMetadata();
            UpdateRunButtonVisibility();
            return true;
        }

        private void ApplyFileSyntaxHighlighting()
        {
            SetSyntaxHighlighting(GetHighlightingDefinition(_currentFileName));
        }

        internal static IHighlightingDefinition? GetHighlightingDefinition(string? filePath)
        {
            string extension = Path.GetExtension(filePath ?? string.Empty);
            return extension.ToLowerInvariant() switch
            {
                ".cvproj" => HighlightingManager.Instance.GetDefinition("Json"),
                ".csproj" or ".fsproj" or ".vbproj" => HighlightingManager.Instance.GetDefinition("XML"),
                _ => HighlightingManager.Instance.GetDefinitionByExtension(extension),
            };
        }

        internal static bool IsPythonDocument(string? filePath)
        {
            string extension = Path.GetExtension(filePath ?? string.Empty);
            return extension.Equals(".py", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pyw", StringComparison.OrdinalIgnoreCase);
        }

        private void SetSyntaxHighlighting(IHighlightingDefinition? highlightingDefinition, bool updateSelection = true)
        {
            _typingAssistance?.Reset();
            _highlightingDefinition = highlightingDefinition;
            _themeController?.SetHighlighting(highlightingDefinition);
            ConfigureFolding(highlightingDefinition);
            Minimap.InvalidateDocument();

            if (updateSelection)
            {
                _isUpdatingHighlightingSelection = true;
                highlightingComboBox.SelectedItem = highlightingDefinition;
                _isUpdatingHighlightingSelection = false;
            }

            highlightingComboBox.Text = highlightingDefinition?.Name ?? "纯文本";
            ScheduleFoldings();
        }

        private void HighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingHighlightingSelection)
                return;

            SetSyntaxHighlighting(highlightingComboBox.SelectedItem as IHighlightingDefinition, updateSelection: false);
        }

        private void ConfigureFolding(IHighlightingDefinition? highlightingDefinition)
        {
            string? language = highlightingDefinition?.Name;
            textEditor.TextArea.IndentationStrategy = language == "Python"
                ? new PythonIndentationStrategy(() => textEditor.Options.IndentationString, () => textEditor.TextArea.GetService(typeof(IHighlighter)) as IHighlighter)
                : language is "C#" or "C++" or "Java" or "JavaScript" or "PHP"
                    ? new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy(textEditor.Options)
                    : new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            if ((EditorFoldingStrategy.Supports(language) || language == "XML") && textEditor.Document.TextLength <= 2_000_000)
            {
                _foldingManager ??= FoldingManager.Install(textEditor.TextArea);
                ScheduleFoldings();
            }
            else if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }
        }

        private async Task UpdateFoldingsAsync()
        {
            if (!_foldingPending || _disposed) return;
            _foldingPending = false;
            if (textEditor.Document.TextLength > 2_000_000) { _foldingManager?.Clear(); ApplyCodeModel(EditorCodeModel.Empty); return; }
            if (_foldingManager == null && (EditorFoldingStrategy.Supports(_highlightingDefinition?.Name) || _highlightingDefinition?.Name == "XML"))
            {
                _foldingManager = FoldingManager.Install(textEditor.TextArea);
            }
            _foldingCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _foldingCancellation = cancellation;
            var document = textEditor.Document;
            var version = document.Version;
            var snapshot = document.CreateSnapshot();
            var definition = _highlightingDefinition;
            if (definition != null) _ = definition.MainRuleSet;
            int indentationSize = _preferences.IndentationSize;
            try
            {
                var result = await Task.Run(() =>
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var workingDocument = new ICSharpCode.AvalonEdit.Document.TextDocument(snapshot);
                    var analysis = new EditorCodeAnalysis(definition?.Name);
                    int errorOffset = -1;
                    NewFolding[] foldings;
                    if (definition != null)
                    {
                        foldings = EditorFoldingStrategy.CreateFoldings(workingDocument, definition, indentationSize, analysis, cancellation.Token).ToArray();
                        if (definition.Name == "XML") foldings = new XmlFoldingStrategy().CreateNewFoldings(workingDocument, out errorOffset).ToArray();
                    }
                    else
                    {
                        foldings = [];
                        foreach (var line in workingDocument.Lines) { cancellation.Token.ThrowIfCancellationRequested(); analysis.ReadLine(line, workingDocument.GetText(line)); }
                    }
                    return (Foldings: foldings, ErrorOffset: errorOffset, Code: analysis.Complete(workingDocument));
                }, cancellation.Token);
                if (!_disposed && !cancellation.IsCancellationRequested && ReferenceEquals(document, textEditor.Document)
                    && ReferenceEquals(version, document.Version) && ReferenceEquals(definition, _highlightingDefinition))
                {
                    _foldingManager?.UpdateFoldings(result.Foldings, result.ErrorOffset);
                    ApplyCodeModel(result.Code);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception ex) { log4net.LogManager.GetLogger(typeof(AvalonEditControll)).Warn("Unable to update editor foldings.", ex); }
            finally { if (ReferenceEquals(_foldingCancellation, cancellation)) _foldingCancellation = null; }
        }

        private void ScheduleFoldings()
        {
            _foldingPending = true;
            _foldingCancellation?.Cancel();
            _foldingUpdateTimer?.Stop();
            if (IsLoaded && IsVisible) _foldingUpdateTimer?.Start();
        }

        private void UpdateRunButtonVisibility()
        {
            RunPythonButton.Visibility = IsPythonDocument(_currentFileName)
                ? Visibility.Visible
                : Visibility.Collapsed;
            CommandManager.InvalidateRequerySuggested();
        }

        private void RunPython_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanSave && IsPythonDocument(_currentFileName);
            e.Handled = true;
        }

        private void RunPython_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RunCurrentPythonDocument();

            e.Handled = true;
        }

        private void AvalonEditControll_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled) return;
            Minimap.HidePreview();
            var modifiers = Keyboard.Modifiers;
            if (HandleCodeNavigationKey(e, modifiers)) return;
            if (modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.F: _typingAssistance?.CloseCompletion(); SearchBar.Open(false); break;
                    case Key.H: _typingAssistance?.CloseCompletion(); SearchBar.Open(true); break;
                    case Key.G: OpenGoTo(); break;
                    case Key.D0: SetZoom(1); break;
                    case Key.OemPlus: case Key.Add: SetZoom(_zoom + .1); break;
                    case Key.OemMinus: case Key.Subtract: SetZoom(_zoom - .1); break;
                    default:
                        if (!textEditor.IsKeyboardFocusWithin) return;
                        if (e.Key == Key.D) EditorTextOperations.Duplicate(textEditor);
                        else if (e.Key == Key.OemQuestion && EditorTextOperations.CommentPrefix(_highlightingDefinition?.Name) is { } prefix) EditorTextOperations.ToggleComment(textEditor, prefix);
                        else return;
                        break;
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F3 && (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift))
            {
                SearchBar.FindNext(modifiers == ModifierKeys.Shift); e.Handled = true; return;
            }
            if (textEditor.IsKeyboardFocusWithin && modifiers == ModifierKeys.Alt && (e.SystemKey is Key.Up or Key.Down))
            {
                EditorTextOperations.MoveLines(textEditor, e.SystemKey == Key.Up ? -1 : 1); e.Handled = true; return;
            }
            if (e.Key != Key.F5 || Keyboard.Modifiers != ModifierKeys.None
                || !CanSave || !IsPythonDocument(_currentFileName))
            {
                return;
            }

            RunCurrentPythonDocument();
            e.Handled = true;
        }

        private void RunCurrentPythonDocument()
        {
            if ((!IsDirty || EditorDocumentService.TrySaveDocument(this)) && _currentFileName != null)
                TerminalService.GetInstance().RunScript(_currentFileName);
        }

        private void UpdateDocumentMetadata()
        {
            EncodingText.Text = GetEncodingLabel(textEditor.Encoding);
            string label = "—";
            foreach (var line in textEditor.Document.Lines)
            {
                if (line.DelimiterLength == 0) continue;
                string next = GetLineEndingLabel(textEditor.Document.GetText(line.EndOffset, line.DelimiterLength));
                if (label != "—" && label != next) { label = "混合"; break; }
                label = next;
            }
            LineEndingText.Text = label;
        }

        internal static string GetEncodingLabel(Encoding? encoding)
        {
            encoding ??= Encoding.UTF8;
            return encoding.CodePage switch
            {
                65001 => "UTF-8",
                1200 => "UTF-16 LE",
                1201 => "UTF-16 BE",
                _ => encoding.WebName.ToUpperInvariant(),
            };
        }

        internal static string GetLineEndingLabel(string text)
        {
            if (text.Contains("\r\n", StringComparison.Ordinal))
                return "CRLF";
            if (text.Contains('\n'))
                return "LF";
            if (text.Contains('\r'))
                return "CR";
            return "—";
        }

        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            StatusText.Text = $"{Properties.Resources.Line}:{textEditor.TextArea.Caret.Line} {Properties.Resources.Column}:{textEditor.TextArea.Caret.Column}";
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            SetDirty(!textEditor.Document.UndoStack.IsOriginalFile);
            DiagnosticButton.Visibility = Visibility.Collapsed;
            Minimap.ErrorLine = null;
            if (SymbolPanel.Visibility == Visibility.Visible) { SymbolList.ItemsSource = null; SymbolHint.Text = "正在更新文档声明…"; }
            ScheduleFoldings();
        }

        private async void FoldingUpdateTimer_Tick(object? sender, EventArgs e)
        {
            _foldingUpdateTimer?.Stop();
            UpdateDocumentMetadata();
            await UpdateFoldingsAsync();
        }

        private void SetDirty(bool value)
        {
            if (_isDirty == value)
                return;

            _isDirty = value;
            DocumentStateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _typingAssistance?.Dispose();
            SearchBar.MatchesChanged -= SearchMatchesChanged;
            ResetCodeNavigation();
            _foldingCancellation?.Cancel();
            Loaded -= Editor_Loaded;
            Unloaded -= Editor_Unloaded;
            IsVisibleChanged -= Editor_IsVisibleChanged;
            PreviewMouseWheel -= Editor_PreviewMouseWheel;
            PropertyChangedEventManager.RemoveHandler(_preferences, PreferencesChanged, string.Empty);
            textEditor.Document.UndoStack.PropertyChanged -= UndoStateChanged;
            Minimap.Dispose();
            SearchBar.Dispose();
            textEditor.TextArea.TextView.BackgroundRenderers.Remove(_indentGuides);
            _foldingUpdateTimer?.Stop();
            if (_foldingUpdateTimer != null)
                _foldingUpdateTimer.Tick -= FoldingUpdateTimer_Tick;
            _foldingUpdateTimer = null;

            _themeController?.Dispose();
            _themeController = null;
            RemoveHandler(
                Keyboard.PreviewKeyDownEvent,
                new KeyEventHandler(AvalonEditControll_PreviewKeyDown));
            textEditor.TextChanged -= TextEditor_TextChanged;
            textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;

            if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }

            textEditor.Clear();
            textEditor.Document = null;
            DocumentStateChanged = null;
            GC.SuppressFinalize(this);
        }
    }
}
