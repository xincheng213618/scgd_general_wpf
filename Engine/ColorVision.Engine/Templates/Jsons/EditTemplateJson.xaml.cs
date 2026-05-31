using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Utilities;
using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Templates.Jsons
{

    public class EditTemplateJsonConfig :IConfig
    {
        public static EditTemplateJsonConfig Instance { get; set; } = ConfigService.Instance.GetRequiredService<EditTemplateJsonConfig>();

        public double Width { get => _Width; set { _Width = value; } }
        private double _Width = double.NaN;

        public bool UsePropertyEditor { get; set; } = false; // Default to text editor mode
    }

    public partial class EditTemplateJson : UserControl, ITemplateUserControl
    {
        private const int MaxCopilotJsonChars = 16000;
        private readonly string _copilotContextSourceId = $"template-json-editor:{Guid.NewGuid():N}";

        private string Description { get; set; }
        private bool _isInPropertyEditorMode = false;
        private bool _isSyncingFromPropertyEditor = false;
        private string _loadedJsonSnapshot = string.Empty;

        public EditTemplateJson(string description)
        {
            Description = description;
            InitializeComponent();
            this.Width = EditTemplateJsonConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateJsonConfig.Instance.Width = this.ActualWidth;
            };
            textEditor.TextArea.IndentationStrategy = new ICSharpCode.AvalonEdit.Indentation.DefaultIndentationStrategy();
            textEditor.ShowLineNumbers = true;
            textEditor.TextChanged += TextEditor_TextChanged;

            // Set initial mode from config
            _isInPropertyEditorMode = EditTemplateJsonConfig.Instance.UsePropertyEditor;
            EditorModeToggle.IsChecked = _isInPropertyEditorMode;
            UpdateEditorMode();

            // Subscribe to property editor changes
            propertyEditor.JsonValueChanged += PropertyEditor_JsonValueChanged;

            Loaded += EditTemplateJson_Loaded;
            Unloaded += EditTemplateJson_Unloaded;
            IsKeyboardFocusWithinChanged += EditTemplateJson_IsKeyboardFocusWithinChanged;
        }

        private void EditTemplateJson_Loaded(object sender, RoutedEventArgs e)
        {
            PublishCopilotContext();
        }

        private void EditTemplateJson_Unloaded(object sender, RoutedEventArgs e)
        {
            CopilotLiveContextRegistry.Clear(_copilotContextSourceId);
        }

        private void EditTemplateJson_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isFocused && isFocused)
                PublishCopilotContext();
        }

        private void TextEditor_TextChanged(object? sender, EventArgs e)
        {
            DebounceTimer.AddOrResetTimer("EditTemplateJsonChanged", 50, EditTemplateJsonChanged);
        }

        public void EditTemplateJsonChanged()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (IEditTemplateJson != null)
                {
                    IEditTemplateJson.JsonValue = textEditor.Text;
                }

                PublishCopilotContext();
            });
        }


        private IEditTemplateJson IEditTemplateJson;


        public void SetParam(object param)
        {
            if (param is IEditTemplateJson editTemplateJson)
            {
                this.DataContext = param; 
                if (IEditTemplateJson !=null)
                    IEditTemplateJson.JsonValueChanged -= IEditTemplateJson_JsonValueChanged;
                IEditTemplateJson = editTemplateJson;
                _loadedJsonSnapshot = IEditTemplateJson.JsonValue ?? string.Empty;
                textEditor.Text = IEditTemplateJson.JsonValue;
                IEditTemplateJson.JsonValueChanged += IEditTemplateJson_JsonValueChanged;

                textEditor.TextChanged -= TextEditor_TextChanged;
                textEditor.TextChanged += TextEditor_TextChanged;

                // If in property editor mode, refresh the property editor with new data
                if (_isInPropertyEditorMode)
                {
                    try
                    {
                        propertyEditor.SetJson(IEditTemplateJson.JsonValue);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing property editor: {ex.Message}");
                    }
                }
            }
            DescriptionButton.IsChecked = false;
            PublishCopilotContext();
        }

        private void IEditTemplateJson_JsonValueChanged(object? sender, EventArgs e)
        {
            textEditor.Text = IEditTemplateJson.JsonValue;
            PublishCopilotContext();
        }

        private string texttemp;

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if(sender is ToggleButton toggleButton)
            {
                if (toggleButton.IsChecked == true)
                {
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    texttemp = textEditor.Text;
                    textEditor.Text = Description;
                }
                else
                {

                    textEditor.Text = texttemp;
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    textEditor.TextChanged += TextEditor_TextChanged;
                }
            }

            PublishCopilotContext();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.json.cn/",
                UseShellExecute = true
            });
            Common.NativeMethods.Clipboard.SetText(IEditTemplateJson.JsonValue);
        }

        private void EditorModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton toggleButton)
            {
                _isInPropertyEditorMode = toggleButton.IsChecked == true;
                EditTemplateJsonConfig.Instance.UsePropertyEditor = _isInPropertyEditorMode;

                if (_isInPropertyEditorMode)
                {
                    // Switch to property editor mode
                    SwitchToPropertyEditorMode();
                }
                else
                {
                    // Switch back to text mode
                    SwitchToTextMode();
                }
            }
        }

        private void SwitchToPropertyEditorMode()
        {
            try
            {
                // Load current JSON into property editor
                propertyEditor.SetJson(textEditor.Text);

                // Show property editor, hide text editor
                textEditor.Visibility = Visibility.Collapsed;
                propertyEditor.Visibility = Visibility.Visible;

                // Update toggle button text
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.TextEdit;
                PublishCopilotContext();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换到属性编辑器失败: {ex.Message}\n\n请检查 JSON 格式是否正确。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                
                // Revert toggle state
                EditorModeToggle.IsChecked = false;
                _isInPropertyEditorMode = false;
            }
        }

        private void SwitchToTextMode()
        {
            try
            {
                // Get JSON from property editor
                var json = propertyEditor.GetJson();
                if (!string.IsNullOrEmpty(json))
                {
                    // Update text editor
                    textEditor.TextChanged -= TextEditor_TextChanged;
                    textEditor.Text = json;
                    textEditor.TextChanged += TextEditor_TextChanged;
                }

                // Show text editor, hide property editor
                textEditor.Visibility = Visibility.Visible;
                propertyEditor.Visibility = Visibility.Collapsed;

                // Update toggle button text
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.PropertyEdit;
                PublishCopilotContext();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"切换到文本编辑器失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateEditorMode()
        {
            if (_isInPropertyEditorMode)
            {
                textEditor.Visibility = Visibility.Collapsed;
                propertyEditor.Visibility = Visibility.Visible;
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.TextEdit;
            }
            else
            {
                textEditor.Visibility = Visibility.Visible;
                propertyEditor.Visibility = Visibility.Collapsed;
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.PropertyEdit;
            }
        }

        private void PropertyEditor_JsonValueChanged(object? sender, string json)
        {
            if (_isSyncingFromPropertyEditor)
                return;

            _isSyncingFromPropertyEditor = true;
            try
            {
                // Update the IEditTemplateJson value when property editor changes
                if (IEditTemplateJson != null)
                {
                    IEditTemplateJson.JsonValue = json;
                }

                PublishCopilotContext();
            }
            finally
            {
                _isSyncingFromPropertyEditor = false;
            }
        }

        private void AskCopilotButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.ContextMenu == null)
                return;

            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Top;
            button.ContextMenu.IsOpen = true;
        }

        private void AskCopilotExplainTemplate_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Explain,
                "请结合已附加的模板快照，先说明这个模板大致用于什么检测逻辑，再按字段解释主要参数的作用和彼此关系。",
                sendNow: true);
        }

        private void AskCopilotExplainParameters_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Explain,
                "请结合已附加的模板快照，逐项解释当前 JSON 参数的含义、典型影响范围，以及哪些参数组合需要一起看。",
                sendNow: true);
        }

        private void AskCopilotDiagnoseTemplate_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Diagnose,
                "请结合已附加的模板快照，检查当前配置里是否存在明显异常、矛盾阈值或高风险参数，并给出判断理由与调整建议。",
                sendNow: true);
        }

        private void AskCopilotOpenPanel_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Explain,
                "请基于已附加的模板快照继续分析当前内容。",
                sendNow: false);
        }

        private void AskCopilot(CopilotPromptMode mode, string prompt, bool sendNow)
        {
            var snapshotItem = BuildCopilotSnapshotContextItem();
            if (snapshotItem == null)
            {
                MessageBox.Show(
                    Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                    "当前模板上下文尚未准备好，无法发送给 Copilot。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            PublishCopilotContext();

            var result = CopilotPromptRequestHelper.Dispatch(new CopilotPromptRequestOptions
            {
                Prompt = prompt,
                Mode = mode,
                StartNewConversation = true,
                SendNow = sendNow,
                AttachContextSnapshot = true,
                ContextAttachmentTitle = BuildCopilotContextDisplayLabel(),
                ContextAttachmentSourceId = _copilotContextSourceId,
                ContextItems = new[] { snapshotItem },
            });

            if (!result.WasSent)
            {
                MessageBox.Show(
                    Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                    result.StatusMessage,
                    "ColorVision",
                    MessageBoxButton.OK,
                    result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
        }

        private void PublishCopilotContext()
        {
            var liveContext = BuildCopilotLiveContext();
            if (liveContext == null)
                return;

            CopilotLiveContextRegistry.Publish(liveContext);
        }

        private CopilotLiveContext? BuildCopilotLiveContext()
        {
            var snapshotItem = BuildCopilotSnapshotContextItem();
            if (snapshotItem == null)
                return null;

            return new CopilotLiveContext
            {
                SourceId = _copilotContextSourceId,
                Title = snapshotItem.Title,
                Summary = snapshotItem.Summary,
                AttachmentTitle = snapshotItem.Title,
                SnapshotItems = new[] { snapshotItem },
            };
        }

        private CopilotContextItem? BuildCopilotSnapshotContextItem()
        {
            if (IEditTemplateJson == null)
                return null;

            var templateName = GetCurrentTemplateName();
            var currentJson = GetCurrentJsonForCopilot();
            var lineCount = CountJsonLines(currentJson);
            var isValidJson = !string.IsNullOrWhiteSpace(currentJson) && JsonHelper.IsValidJson(currentJson);
            var hasUnsavedChanges = !string.Equals(
                (_loadedJsonSnapshot ?? string.Empty).Trim(),
                (currentJson ?? string.Empty).Trim(),
                StringComparison.Ordinal);
            var editorMode = GetEditorModeLabel();
            var windowTitle = Window.GetWindow(this)?.Title ?? string.Empty;

            var summary = $"JSON lines {lineCount} · {(hasUnsavedChanges ? "modified" : "unchanged")} · {(isValidJson ? "valid" : "invalid")} · {editorMode}";

            var builder = new StringBuilder();
            builder.Append("Surface: ").AppendLine("Template JSON editor");
            builder.Append("Template name: ").AppendLine(templateName);
            builder.Append("Current selection: ").AppendLine(templateName);

            if (!string.IsNullOrWhiteSpace(windowTitle))
                builder.Append("Window title: ").AppendLine(windowTitle);

            builder.Append("Editor mode: ").AppendLine(editorMode);
            builder.Append("Unsaved changes: ").AppendLine(hasUnsavedChanges ? "yes" : "no");
            builder.Append("JSON validation: ").AppendLine(isValidJson ? "passed" : "failed");
            builder.Append("JSON line count: ").AppendLine(lineCount.ToString());
            builder.AppendLine();
            builder.AppendLine("Current JSON:");
            builder.AppendLine("```json");
            builder.AppendLine(TruncateForCopilot(currentJson, MaxCopilotJsonChars));
            builder.AppendLine("```");

            return new CopilotContextItem
            {
                Id = $"{_copilotContextSourceId}:snapshot",
                Title = BuildCopilotContextDisplayLabel(templateName),
                Summary = summary,
                Content = builder.ToString().Trim(),
            };
        }

        private string BuildCopilotContextDisplayLabel()
        {
            return BuildCopilotContextDisplayLabel(GetCurrentTemplateName());
        }

        private static string BuildCopilotContextDisplayLabel(string templateName)
        {
            return string.IsNullOrWhiteSpace(templateName)
                ? "Template JSON editor"
                : $"Template JSON editor · {templateName}";
        }

        private string GetCurrentTemplateName()
        {
            if (DataContext is TemplateJsonParam templateJsonParam && !string.IsNullOrWhiteSpace(templateJsonParam.Name))
                return templateJsonParam.Name;

            return IEditTemplateJson is TemplateJsonParam fallbackParam && !string.IsNullOrWhiteSpace(fallbackParam.Name)
                ? fallbackParam.Name
                : "Unnamed template";
        }

        private string GetCurrentJsonForCopilot()
        {
            if (IEditTemplateJson == null)
                return string.Empty;

            if (DescriptionButton.IsChecked == true)
                return IEditTemplateJson.JsonValue ?? string.Empty;

            if (_isInPropertyEditorMode)
            {
                try
                {
                    var propertyJson = propertyEditor.GetJson();
                    if (!string.IsNullOrWhiteSpace(propertyJson))
                        return propertyJson;
                }
                catch
                {
                }

                return IEditTemplateJson.JsonValue ?? string.Empty;
            }

            return textEditor.Text ?? string.Empty;
        }

        private string GetEditorModeLabel()
        {
            if (DescriptionButton.IsChecked == true)
                return "description view";

            return _isInPropertyEditorMode ? "property editor" : "text editor";
        }

        private static string TruncateForCopilot(string value, int maxChars)
        {
            var text = value ?? string.Empty;
            return text.Length <= maxChars
                ? text
                : text[..maxChars] + Environment.NewLine + $"...<content truncated; kept the first {maxChars} characters.>";
        }

        private static int CountJsonLines(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return 0;

            var normalized = json.Replace("\r\n", "\n").Replace('\r', '\n');
            return normalized.Split('\n').Length;
        }
    }
}
