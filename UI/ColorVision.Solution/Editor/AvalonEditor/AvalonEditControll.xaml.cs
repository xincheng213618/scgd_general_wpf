#pragma warning disable
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ColorVision.Solution.Workspace;

namespace ColorVision.Solution.Editor.AvalonEditor
{
    /// <summary>
    /// AvalonEditControll.xaml 的交互逻辑
    /// </summary>
    public partial class AvalonEditControll : UserControl, IDisposable, IEditorDocumentContent, IResourcePathAwareDocumentContent, IReloadableEditorDocumentContent
    {
        private DispatcherTimer? _foldingUpdateTimer;
        private bool _isDirty;
        private bool _disposed;

        public bool IsDirty => _isDirty;
        public bool CanSave => !string.IsNullOrWhiteSpace(currentFileName);
        public event EventHandler? DocumentStateChanged;

        public AvalonEditControll()
        {
            InitializeComponent();
            textEditor.TextChanged += TextEditor_TextChanged;
        }
        public AvalonEditControll(string currentFileName)
        {
            InitializeComponent();

            this.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);

            textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering;
            textEditor.TextArea.TextEntered += textEditor_TextArea_TextEntered;
            textEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

            SearchPanel.Install(textEditor);

            _foldingUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _foldingUpdateTimer.Tick += FoldingUpdateTimer_Tick;
            _foldingUpdateTimer.Start();

            this.currentFileName = currentFileName;

            ReloadFromDisk();
            textEditor.TextChanged += TextEditor_TextChanged;
        }



        private void UserControl_Initialized(object sender, EventArgs e)
        {

        }
        bool isFormatted = false;
        public string OriginalText;

        public void SetJsonText(string Text)
        {
            OriginalText = Text;
            try
            {
                var parsedJson = JToken.Parse(Text);
                isFormatted = Text.Contains("\n") || Text.Contains("\t");
                textEditor.Text = parsedJson.ToString(Formatting.Indented);
            }
            catch (JsonReaderException)
            {
                textEditor.Text = Text;
            }
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension("Json");
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
        }
        public string GetJsonText()
        {
            string Text = textEditor.Text;
            try
            {
                var parsedJson = JToken.Parse(Text);

                return parsedJson.ToString(isFormatted ? Formatting.Indented : Formatting.None);
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

            var targetLineNumber = Math.Clamp(lineNumber, 1, textEditor.Document.LineCount);
            var targetLine = textEditor.Document.GetLineByNumber(targetLineNumber);
            var targetColumn = Math.Clamp(columnNumber, 1, targetLine.Length + 1);
            textEditor.TextArea.Caret.Offset = targetLine.Offset + targetColumn - 1;
            textEditor.ScrollToLine(targetLineNumber);
            textEditor.Focus();
        }


        string currentFileName;

        void openFileClick(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.CheckFileExists = true;
            if (dlg.ShowDialog() ?? false)
            {
                EditorManager.Instance.TryOpenFile(dlg.FileName);
            }
        }

        public bool Save()
        {
            if (!CanSave)
                return false;

            textEditor.Save(currentFileName);
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
            return true;
        }

        public bool TryUpdateResourcePath(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath) || !File.Exists(resourcePath))
                return false;

            currentFileName = Path.GetFullPath(resourcePath);
            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(currentFileName))
                ?? HighlightingManager.Instance.GetDefinitionByExtension(".Json");
            return true;
        }

        public bool ReloadFromDisk()
        {
            if (!File.Exists(currentFileName))
                return false;

            string text = File.ReadAllText(currentFileName);
            if (text.Length < 10000)
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

            textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(currentFileName))
                ?? HighlightingManager.Instance.GetDefinitionByExtension(".Json");
            textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            textEditor.Document.UndoStack.MarkAsOriginalFile();
            SetDirty(false);
            return true;
        }


        CompletionWindow completionWindow;

        void textEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (e.Text == ".")
            {
                // open code completion after the user has pressed dot:
                completionWindow = new CompletionWindow(textEditor.TextArea);
                // provide AvalonEdit with the data:
                IList<ICompletionData> data = completionWindow.CompletionList.CompletionData;
                completionWindow.Show();
                completionWindow.Closed += delegate {
                    completionWindow = null;
                };
            }
        }
        private void Caret_PositionChanged(object? sender, EventArgs e)
        {
            StatusText.Text = $"{Properties.Resources.Line}:{textEditor.TextArea.Caret.Line} {Properties.Resources.Column}:{textEditor.TextArea.Caret.Column}";
        }

        void textEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]))
                {
                    // Whenever a non-letter is typed while the completion window is open,
                    // insert the currently selected element.
                    completionWindow.CompletionList.RequestInsertion(e);
                }
            }
            // do not set e.Handled=true - we still want to insert the character that was typed
        }

        #region Folding
        FoldingManager? foldingManager;
        object? foldingStrategy;

        void HighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (textEditor.SyntaxHighlighting == null)
            {
                foldingStrategy = null;
            }
            else
            {
                switch (textEditor.SyntaxHighlighting.Name)
                {
                    case "XML":
                        foldingStrategy = new XmlFoldingStrategy();
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                        break;
                    case "C#":
                    case "C++":
                    case "PHP":
                    case "Java":
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.CSharp.CSharpIndentationStrategy(textEditor.Options);
                        foldingStrategy = new BraceFoldingStrategy();
                        break;
                    default:
                        textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
                        foldingStrategy = null;
                        break;
                }
            }
            if (foldingStrategy != null)
            {
                if (foldingManager == null)
                    foldingManager = FoldingManager.Install(textEditor.TextArea);
                UpdateFoldings();
            }
            else
            {
                if (foldingManager != null)
                {
                    FoldingManager.Uninstall(foldingManager);
                    foldingManager = null;
                }
            }
        }

        void UpdateFoldings()
        {
            if (foldingStrategy is BraceFoldingStrategy)
            {
                ((BraceFoldingStrategy)foldingStrategy).UpdateFoldings(foldingManager, textEditor.Document);
            }
            if (foldingStrategy is XmlFoldingStrategy)
            {
                ((XmlFoldingStrategy)foldingStrategy).UpdateFoldings(foldingManager, textEditor.Document);
            }
        }
        #endregion

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _foldingUpdateTimer?.Stop();
            if (_foldingUpdateTimer != null)
                _foldingUpdateTimer.Tick -= FoldingUpdateTimer_Tick;
            _foldingUpdateTimer = null;
            completionWindow?.Close();
            completionWindow = null;
            textEditor.TextChanged -= TextEditor_TextChanged;
            textEditor.TextArea.TextEntering -= textEditor_TextArea_TextEntering;
            textEditor.TextArea.TextEntered -= textEditor_TextArea_TextEntered;
            textEditor.TextArea.Caret.PositionChanged -= Caret_PositionChanged;
            if (foldingManager != null)
            {
                FoldingManager.Uninstall(foldingManager);
                foldingManager = null;
            }
            textEditor.Clear();
            textEditor.Document = null;
            DocumentStateChanged = null;
            GC.SuppressFinalize(this);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (!Save())
                return;

            // 1. 查找 Python 路径（Windows 下）
            string pythonPath = GetPythonPath();

            if (pythonPath == null)
            {
                Console.WriteLine("没有找到 Python 环境。");
                return;
            }

            // 2. 要执行的 Python 文件路径
            string pythonFile = currentFileName; // 修改为你的文件路径

            // 3. 构造启动参数，让 cmd 窗口弹出并执行 python 文件
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k \"{pythonPath} \"{pythonFile}\"\"",
                UseShellExecute = true,          // 让窗口弹出
                CreateNoWindow = false           // 让窗口显示
                                                 // 不需要重定向输出
            };

            Process.Start(psi); // 直接启动，不需要捕获输出
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

        // 自动查找 Python 路径
        static string GetPythonPath()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd",
                    Arguments = "/c where python",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(psi))
                {
                    string result = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    if (!string.IsNullOrEmpty(result))
                    {
                        // 返回第一行路径
                        return result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                }
            }
            catch { }
            return null;
        }
    }
}
