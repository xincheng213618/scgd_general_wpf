using ColorVision.Solution.Terminal;
using ColorVision.Solution.Workspace;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
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
        private FoldingManager? _foldingManager;
        private object? _foldingStrategy;
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
        {
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
            SearchPanel.Install(textEditor);

            _themeController = new AvalonEditorThemeController(textEditor);
            SetSyntaxHighlighting(null);

            _foldingUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _foldingUpdateTimer.Tick += FoldingUpdateTimer_Tick;
            _foldingUpdateTimer.Start();

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

            textEditor.Load(_currentFileName);
            string text = textEditor.Text;
            OriginalText = text;
            if (IsJsonDocument(_currentFileName) && text.Length < 10000)
            {
                try
                {
                    textEditor.Text = JToken.Parse(text).ToString(Formatting.Indented);
                }
                catch (JsonReaderException)
                {
                    textEditor.Text = text;
                }
            }
            else
            {
                textEditor.Text = text;
            }

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

        private static bool IsJsonDocument(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cvproj", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPythonDocument(string? filePath)
        {
            string extension = Path.GetExtension(filePath ?? string.Empty);
            return extension.Equals(".py", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".pyw", StringComparison.OrdinalIgnoreCase);
        }

        private void SetSyntaxHighlighting(IHighlightingDefinition? highlightingDefinition, bool updateSelection = true)
        {
            _themeController?.SetHighlighting(highlightingDefinition);
            ConfigureFolding(highlightingDefinition);

            if (updateSelection)
            {
                _isUpdatingHighlightingSelection = true;
                highlightingComboBox.SelectedItem = highlightingDefinition;
                _isUpdatingHighlightingSelection = false;
            }

            highlightingComboBox.Text = highlightingDefinition?.Name ?? "纯文本";
        }

        private void HighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingHighlightingSelection)
                return;

            SetSyntaxHighlighting(highlightingComboBox.SelectedItem as IHighlightingDefinition, updateSelection: false);
        }

        private void ConfigureFolding(IHighlightingDefinition? highlightingDefinition)
        {
            switch (highlightingDefinition?.Name)
            {
                case "XML":
                    _foldingStrategy = new XmlFoldingStrategy();
                    textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                    break;
                case "C#":
                case "C++":
                case "PHP":
                case "Java":
                    _foldingStrategy = new BraceFoldingStrategy();
                    textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy(textEditor.Options);
                    break;
                default:
                    _foldingStrategy = null;
                    textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                    break;
            }

            if (_foldingStrategy != null)
            {
                _foldingManager ??= FoldingManager.Install(textEditor.TextArea);
                UpdateFoldings();
            }
            else if (_foldingManager != null)
            {
                FoldingManager.Uninstall(_foldingManager);
                _foldingManager = null;
            }
        }

        private void UpdateFoldings()
        {
            if (_foldingManager == null)
                return;

            if (_foldingStrategy is BraceFoldingStrategy braceFoldingStrategy)
                braceFoldingStrategy.UpdateFoldings(_foldingManager, textEditor.Document);
            else if (_foldingStrategy is XmlFoldingStrategy xmlFoldingStrategy)
                xmlFoldingStrategy.UpdateFoldings(_foldingManager, textEditor.Document);
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
            LineEndingText.Text = GetLineEndingLabel(textEditor.Text);
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
        }

        private void FoldingUpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateFoldings();
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
