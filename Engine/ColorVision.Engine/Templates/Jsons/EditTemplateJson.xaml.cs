#pragma warning disable CA1805,CS8601,CS8604,CS8625
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

        public bool UsePropertyEditor { get; set; } = true; // Default to property editor mode
    }

    public sealed class CopilotTemplateJsonPatchApplyResult
    {
        public bool Success { get; init; }

        public string ErrorCode { get; init; } = string.Empty;

        public string Message { get; init; } = string.Empty;

        public static CopilotTemplateJsonPatchApplyResult Ok(string message) => new()
        {
            Success = true,
            Message = message ?? string.Empty,
        };

        public static CopilotTemplateJsonPatchApplyResult Fail(string errorCode, string message) => new()
        {
            ErrorCode = errorCode ?? string.Empty,
            Message = message ?? string.Empty,
        };
    }

    public partial class EditTemplateJson : UserControl, ITemplateUserControl
    {
        private const int MaxCopilotJsonChars = 16000;
        private const string SchemaIndexResourceName = "Templates/Jsons/Schemas/schema-index.json";
        private static readonly object CopilotEditorSyncRoot = new();
        private static readonly Dictionary<string, WeakReference<EditTemplateJson>> CopilotEditors = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _copilotContextSourceId = $"template-json-editor:{Guid.NewGuid():N}";

        private bool _isInPropertyEditorMode = false;
        private bool _isSyncingFromPropertyEditor = false;
        private string _loadedJsonSnapshot = string.Empty;
        private TemplateJsonSchemaInfo? _schemaInfo;

        public EditTemplateJson(string description)
        {
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
            RegisterCopilotEditor();
            PublishCopilotContext();
        }

        private void EditTemplateJson_Unloaded(object sender, RoutedEventArgs e)
        {
            UnregisterCopilotEditor();
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
                RegisterCopilotEditor();
                this.DataContext = param; 
                if (IEditTemplateJson !=null)
                    IEditTemplateJson.JsonValueChanged -= IEditTemplateJson_JsonValueChanged;
                IEditTemplateJson = editTemplateJson;
                _schemaInfo = TryLoadTemplateSchema(GetTemplateCode(editTemplateJson));
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
                        SetPropertyEditorJson(IEditTemplateJson.JsonValue);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error refreshing property editor: {ex.Message}");
                    }
                }
            }
            PublishCopilotContext();
        }

        private void IEditTemplateJson_JsonValueChanged(object? sender, EventArgs e)
        {
            textEditor.Text = IEditTemplateJson.JsonValue;
            PublishCopilotContext();
        }

        private void SetPropertyEditorJson(string json)
        {
            propertyEditor.SetJson(json, _schemaInfo?.SchemaJson, _schemaInfo?.Title);
        }

        private static string? GetTemplateCode(IEditTemplateJson editTemplateJson)
        {
            return editTemplateJson is TemplateJsonParam templateJsonParam
                ? templateJsonParam.TemplateJsonModel?.Code
                : null;
        }

        private static TemplateJsonSchemaInfo? TryLoadTemplateSchema(string? templateCode)
        {
            if (string.IsNullOrWhiteSpace(templateCode))
                return null;

            var embeddedSchema = TryLoadEmbeddedTemplateSchema(templateCode);
            if (embeddedSchema != null)
                return embeddedSchema;

            foreach (var indexPath in EnumerateSchemaIndexPaths())
            {
                try
                {
                    if (!File.Exists(indexPath))
                        continue;

                    using var indexDocument = JsonDocument.Parse(File.ReadAllText(indexPath));
                    if (!indexDocument.RootElement.TryGetProperty("schemas", out var schemas) ||
                        schemas.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var schemaItem in schemas.EnumerateArray())
                    {
                        if (!TryGetString(schemaItem, "code", out var code) ||
                            !string.Equals(code, templateCode, StringComparison.OrdinalIgnoreCase) ||
                            !TryGetString(schemaItem, "file", out var relativeFile))
                            continue;

                        var jsonsRoot = Directory.GetParent(Path.GetDirectoryName(indexPath) ?? string.Empty)?.FullName;
                        if (string.IsNullOrWhiteSpace(jsonsRoot))
                            continue;

                        var schemaPath = Path.GetFullPath(Path.Combine(
                            jsonsRoot,
                            relativeFile.Replace('/', Path.DirectorySeparatorChar)));

                        if (!File.Exists(schemaPath))
                            continue;

                        var schemaJson = File.ReadAllText(schemaPath);
                        var title = TryGetString(schemaItem, "name", out var itemName) ? itemName : code;

                        return new TemplateJsonSchemaInfo
                        {
                            Code = code,
                            Title = title,
                            SchemaJson = schemaJson,
                            SchemaPath = schemaPath
                        };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Load JSON schema failed: {indexPath}, {ex.Message}");
                }
            }

            return null;
        }

        private static TemplateJsonSchemaInfo? TryLoadEmbeddedTemplateSchema(string templateCode)
        {
            try
            {
                var assembly = typeof(EditTemplateJson).Assembly;
                var indexJson = ReadEmbeddedResourceText(assembly, SchemaIndexResourceName);
                if (string.IsNullOrWhiteSpace(indexJson))
                    return null;

                using var indexDocument = JsonDocument.Parse(indexJson);
                if (!indexDocument.RootElement.TryGetProperty("schemas", out var schemas) ||
                    schemas.ValueKind != JsonValueKind.Array)
                    return null;

                foreach (var schemaItem in schemas.EnumerateArray())
                {
                    if (!TryGetString(schemaItem, "code", out var code) ||
                        !string.Equals(code, templateCode, StringComparison.OrdinalIgnoreCase) ||
                        !TryGetString(schemaItem, "file", out var relativeFile))
                        continue;

                    string resourceName = "Templates/Jsons/" + relativeFile.Replace('\\', '/');
                    string windowsResourceName = "Templates/Jsons/" + relativeFile.Replace('/', '\\');
                    var schemaJson = ReadEmbeddedResourceText(assembly, resourceName)
                        ?? ReadEmbeddedResourceText(assembly, windowsResourceName)
                        ?? ReadEmbeddedResourceText(assembly, resourceName.Replace('/', '\\'));
                    if (string.IsNullOrWhiteSpace(schemaJson))
                        continue;

                    var title = TryGetString(schemaItem, "name", out var itemName) ? itemName : code;

                    return new TemplateJsonSchemaInfo
                    {
                        Code = code,
                        Title = title,
                        SchemaJson = schemaJson,
                        SchemaPath = "resource://" + resourceName
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Load embedded JSON schema failed: {ex.Message}");
            }

            return null;
        }

        private static string? ReadEmbeddedResourceText(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        private static IEnumerable<string> EnumerateSchemaIndexPaths()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var basePath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                if (string.IsNullOrWhiteSpace(basePath))
                    continue;

                var directory = new DirectoryInfo(basePath);
                for (var depth = 0; directory != null && depth < 8; depth++, directory = directory.Parent)
                {
                    foreach (var relativePath in new[]
                    {
                        Path.Combine("Templates", "Jsons", "Schemas", "schema-index.json"),
                        Path.Combine("Engine", "ColorVision.Engine", "Templates", "Jsons", "Schemas", "schema-index.json")
                    })
                    {
                        var candidate = Path.GetFullPath(Path.Combine(directory.FullName, relativePath));
                        if (seen.Add(candidate))
                            yield return candidate;
                    }
                }
            }
        }

        private static bool TryGetString(JsonElement element, string propertyName, out string value)
        {
            value = string.Empty;
            if (!element.TryGetProperty(propertyName, out var property) ||
                property.ValueKind != JsonValueKind.String)
                return false;

            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://www.json.cn/",
                UseShellExecute = true
            });
            Common.Clipboard.SetText(IEditTemplateJson.JsonValue);
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
                SetPropertyEditorJson(textEditor.Text);

                // Show property editor, hide text editor
                textEditor.Visibility = Visibility.Collapsed;
                propertyEditor.Visibility = Visibility.Visible;

                // Update toggle button text
                EditorModeToggle.Content = ColorVision.Engine.Properties.Resources.TextEdit;
                PublishCopilotContext();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Properties.Resources.Copilot_PropertyEditorSwitchFailed, ex.Message), Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
                
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
                MessageBox.Show(string.Format(Properties.Resources.Copilot_TextEditorSwitchFailed, ex.Message), Properties.Resources.Error, MessageBoxButton.OK, MessageBoxImage.Error);
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
                Properties.Resources.Copilot_ExplainTemplatePrompt,
                sendNow: true);
        }

        private void AskCopilotExplainParameters_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Explain,
                Properties.Resources.Copilot_ExplainParametersPrompt,
                sendNow: true);
        }

        private void AskCopilotDiagnoseTemplate_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Diagnose,
                Properties.Resources.Copilot_DiagnoseTemplatePrompt,
                sendNow: true);
        }

        private void AskCopilotOpenPanel_Click(object sender, RoutedEventArgs e)
        {
            AskCopilot(
                CopilotPromptMode.Explain,
                Properties.Resources.Copilot_ContinueAnalysisPrompt,
                sendNow: false);
        }

        private void AskCopilot(CopilotPromptMode mode, string prompt, bool sendNow)
        {
            var snapshotItem = BuildCopilotSnapshotContextItem();
            if (snapshotItem == null)
            {
                MessageBox.Show(
                    Window.GetWindow(this) ?? Application.Current.GetActiveWindow(),
                    Properties.Resources.Copilot_ContextNotReady,
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
            RegisterCopilotEditor();
            var liveContext = BuildCopilotLiveContext();
            if (liveContext == null)
                return;

            CopilotLiveContextRegistry.Publish(liveContext);
        }

        public static async Task<CopilotTemplateJsonPatchApplyResult> TryApplyCopilotJsonPatchAsync(
            string sourceId,
            string expectedCurrentJson,
            string patchedJson,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(sourceId))
                return CopilotTemplateJsonPatchApplyResult.Fail("template_source_missing", "The active template editor source id is missing.");

            if (!TryGetCopilotEditor(sourceId, out var editor))
                return CopilotTemplateJsonPatchApplyResult.Fail("active_template_editor_not_found", "No loaded active template JSON editor matches the preview source id.");

            if (editor.Dispatcher.CheckAccess())
                return editor.ApplyCopilotJsonPatch(expectedCurrentJson, patchedJson, cancellationToken);

            return await editor.Dispatcher.InvokeAsync(
                () => editor.ApplyCopilotJsonPatch(expectedCurrentJson, patchedJson, cancellationToken));
        }

        private static bool TryGetCopilotEditor(string sourceId, out EditTemplateJson editor)
        {
            editor = null;
            lock (CopilotEditorSyncRoot)
            {
                var staleKeys = new List<string>();
                foreach (var pair in CopilotEditors)
                {
                    if (!pair.Value.TryGetTarget(out _))
                        staleKeys.Add(pair.Key);
                }

                foreach (var key in staleKeys)
                    CopilotEditors.Remove(key);

                if (CopilotEditors.TryGetValue(sourceId.Trim(), out var reference) && reference.TryGetTarget(out editor))
                    return true;
            }

            return false;
        }

        private void RegisterCopilotEditor()
        {
            lock (CopilotEditorSyncRoot)
            {
                CopilotEditors[_copilotContextSourceId] = new WeakReference<EditTemplateJson>(this);
            }
        }

        private void UnregisterCopilotEditor()
        {
            lock (CopilotEditorSyncRoot)
            {
                CopilotEditors.Remove(_copilotContextSourceId);
            }
        }

        private CopilotTemplateJsonPatchApplyResult ApplyCopilotJsonPatch(string expectedCurrentJson, string patchedJson, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IEditTemplateJson == null)
                return CopilotTemplateJsonPatchApplyResult.Fail("template_context_unavailable", "The active template editor has no editable JSON context.");

            var currentJson = GetCurrentJsonForCopilot();
            if (!TryNormalizeJson(currentJson, out var normalizedCurrentJson, out var currentError))
                return CopilotTemplateJsonPatchApplyResult.Fail("invalid_active_template_json", $"The active template JSON is invalid: {currentError}");

            if (!TryNormalizeJson(expectedCurrentJson, out var normalizedExpectedJson, out var expectedError))
                return CopilotTemplateJsonPatchApplyResult.Fail("invalid_preview_template_json", $"The preview template JSON is invalid: {expectedError}");

            if (!string.Equals(normalizedCurrentJson, normalizedExpectedJson, StringComparison.Ordinal))
                return CopilotTemplateJsonPatchApplyResult.Fail("template_patch_conflict", "The active template JSON changed after preview_template_patch. Re-run preview_template_patch before applying.");

            if (!TryNormalizeJson(patchedJson, out _, out var patchedError))
                return CopilotTemplateJsonPatchApplyResult.Fail("invalid_patched_template_json", $"The patched template JSON is invalid: {patchedError}");

            textEditor.TextChanged -= TextEditor_TextChanged;
            try
            {
                textEditor.Text = patchedJson;
                IEditTemplateJson.JsonValue = patchedJson;

                if (_isInPropertyEditorMode)
                    SetPropertyEditorJson(patchedJson);
            }
            finally
            {
                textEditor.TextChanged += TextEditor_TextChanged;
            }

            PublishCopilotContext();
            return CopilotTemplateJsonPatchApplyResult.Ok("Template JSON patch applied to the active editor. Review and save from ColorVision when ready.");
        }

        private static bool TryNormalizeJson(string json, out string normalizedJson, out string error)
        {
            normalizedJson = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "JSON text is empty.";
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = $"JSON root must be an object, but was {document.RootElement.ValueKind}.";
                    return false;
                }

                normalizedJson = JsonSerializer.Serialize(document.RootElement);
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
                return false;
            }
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
            if (_schemaInfo != null)
                summary += $" · schema {_schemaInfo.Code}";

            var builder = new StringBuilder();
            builder.Append("Surface: ").AppendLine("Template JSON editor");
            builder.Append("Template name: ").AppendLine(templateName);
            builder.Append("Current selection: ").AppendLine(templateName);
            if (_schemaInfo != null)
            {
                builder.Append("Schema code: ").AppendLine(_schemaInfo.Code);
                builder.Append("Schema title: ").AppendLine(_schemaInfo.Title);
                builder.Append("Schema path: ").AppendLine(_schemaInfo.SchemaPath);
            }

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

        private sealed class TemplateJsonSchemaInfo
        {
            public string Code { get; init; } = string.Empty;

            public string Title { get; init; } = string.Empty;

            public string SchemaJson { get; init; } = string.Empty;

            public string SchemaPath { get; init; } = string.Empty;
        }
    }
}
