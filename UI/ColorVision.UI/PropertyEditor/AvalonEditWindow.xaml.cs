
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Folding;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Search;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;


namespace ColorVision.UI
{
    /// <summary>
    /// Interaction logic for AvalonEditWindow.xaml
    /// </summary>
    public partial class AvalonEditWindow : Window
	{
		public AvalonEditWindow()
		{
		
			InitializeComponent();
			this.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);
			textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering;
			textEditor.TextArea.TextEntered += textEditor_TextArea_TextEntered;
			SearchPanel.Install(textEditor);		
			DispatcherTimer foldingUpdateTimer = new DispatcherTimer();
			foldingUpdateTimer.Interval = TimeSpan.FromSeconds(2);
			foldingUpdateTimer.Tick += delegate { UpdateFoldings(); };
			foldingUpdateTimer.Start();

			this.Closed += (s, e) => {
				textEditor.Clear();
				textEditor.Document = null;
				GC.Collect();
				GC.WaitForPendingFinalizers();
			};
			this.Closing += (s, e) => {
			};
		}

		public AvalonEditWindow(string currentFileName)
		{
            InitializeComponent();

            this.SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Display);

            textEditor.TextArea.TextEntering += textEditor_TextArea_TextEntering;
            textEditor.TextArea.TextEntered += textEditor_TextArea_TextEntered;
            SearchPanel.Install(textEditor);

            DispatcherTimer foldingUpdateTimer = new DispatcherTimer();
            foldingUpdateTimer.Interval = TimeSpan.FromSeconds(2);
            foldingUpdateTimer.Tick += delegate { UpdateFoldings(); };
            foldingUpdateTimer.Start();

            this.Closed += (s, e) => {
                textEditor.Clear();
                textEditor.Document = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
            };
            this.Closing += (s, e) => {
            };

            this.currentFileName = currentFileName;

			if (File.Exists(currentFileName))
			{
                string text = File.ReadAllText(currentFileName);

				if (text.Length< 10000)
				{
					try
					{
                        var parsedJson = JToken.Parse(text);
                        textEditor.Text = parsedJson.ToString(Formatting.Indented);
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
                textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(currentFileName)) ?? HighlightingManager.Instance.GetDefinitionByExtension(".Json");
                textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            }
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
        }
        public string GetJsonText()
        {
			string Text = textEditor.Text;
            try
            {
                var parsedJson = JToken.Parse(Text);

                return parsedJson.ToString(isFormatted?Formatting.Indented : Formatting.None);
            }
            catch (JsonReaderException)
            {
				return OriginalText;
            }
        }


        string currentFileName;
		
		void openFileClick(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dlg = new OpenFileDialog();
			dlg.CheckFileExists = true;
			if (dlg.ShowDialog() ?? false) {
				currentFileName = dlg.FileName;
				textEditor.Load(currentFileName);
                textEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(currentFileName));
			}
		}

        void saveFileClick(object sender, EventArgs e)
		{
			if (currentFileName == null) {
				SaveFileDialog dlg = new SaveFileDialog();
				dlg.DefaultExt = ".txt";
				if (dlg.ShowDialog() ?? false) {
					currentFileName = dlg.FileName;
				} else {
					return;
				}
			}
			textEditor.Save(currentFileName);
		}
		
		
		CompletionWindow completionWindow;
		
		void textEditor_TextArea_TextEntered(object sender, TextCompositionEventArgs e)
		{
			if (e.Text == ".") {
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
		
		void textEditor_TextArea_TextEntering(object sender, TextCompositionEventArgs e)
		{
			if (e.Text.Length > 0 && completionWindow != null) {
				if (!char.IsLetterOrDigit(e.Text[0])) {
					// Whenever a non-letter is typed while the completion window is open,
					// insert the currently selected element.
					completionWindow.CompletionList.RequestInsertion(e);
				}
			}
			// do not set e.Handled=true - we still want to insert the character that was typed
		}
		
		#region Folding
		FoldingManager foldingManager;
		object foldingStrategy;
		
		void HighlightingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (textEditor.SyntaxHighlighting == null) {
				foldingStrategy = null;
			} else {
				switch (textEditor.SyntaxHighlighting.Name) {
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
			if (foldingStrategy != null) {
				if (foldingManager == null)
					foldingManager = FoldingManager.Install(textEditor.TextArea);
				UpdateFoldings();
			} else {
				if (foldingManager != null) {
					FoldingManager.Uninstall(foldingManager);
					foldingManager = null;
				}
			}
		}
		
		void UpdateFoldings()
		{
			if (foldingStrategy is BraceFoldingStrategy) {
				((BraceFoldingStrategy)foldingStrategy).UpdateFoldings(foldingManager, textEditor.Document);
			}
			if (foldingStrategy is XmlFoldingStrategy) {
				((XmlFoldingStrategy)foldingStrategy).UpdateFoldings(foldingManager, textEditor.Document);
			}
		}
		#endregion
	}
}