using ColorVision.Themes;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Solution.Editor.AvalonEditor
{
    /// <summary>
    /// Keeps AvalonEdit's editor chrome and syntax colors aligned with the active app theme.
    /// AvalonEdit's built-in highlighting definitions use fixed light-theme colors, so merely
    /// changing the editor background is not sufficient for dark mode.
    /// </summary>
    internal sealed class AvalonEditorThemeController : IDisposable
    {
        private readonly ICSharpCode.AvalonEdit.TextEditor _editor;
        private readonly ThemeChangedHandler _themeChangedHandler;
        private IHighlightingDefinition? _highlightingDefinition;
        private ThemeAwareHighlightingColorizer? _colorizer;
        private bool _disposed;

        public AvalonEditorThemeController(ICSharpCode.AvalonEdit.TextEditor editor)
        {
            _editor = editor;
            _highlightingDefinition = _editor.SyntaxHighlighting;
            _editor.SyntaxHighlighting = null;
            _editor.Options.HighlightCurrentLine = true;
            _editor.TextArea.SetResourceReference(
                ICSharpCode.AvalonEdit.Editing.TextArea.SelectionBrushProperty,
                "EditorSelectionBrush");
            _editor.TextArea.SetResourceReference(
                ICSharpCode.AvalonEdit.Editing.TextArea.SelectionForegroundProperty,
                "EditorSelectionForegroundBrush");
            _editor.TextArea.TextView.SetResourceReference(
                TextView.CurrentLineBackgroundProperty,
                "EditorCurrentLineBrush");
            _editor.TextArea.TextView.SetResourceReference(
                TextView.CurrentLineBorderProperty,
                "EditorCurrentLineBorderPen");
            _editor.TextArea.TextView.SetResourceReference(
                TextView.NonPrintableCharacterBrushProperty,
                "EditorNonPrintableBrush");
            _editor.TextArea.TextView.SetResourceReference(
                TextView.LinkTextForegroundBrushProperty,
                "EditorLinkBrush");

            _themeChangedHandler = _ => RefreshTheme();
            ThemeManager.Current.CurrentUIThemeChanged += _themeChangedHandler;
            RefreshTheme();
        }

        public void SetHighlighting(IHighlightingDefinition? highlightingDefinition)
        {
            _highlightingDefinition = highlightingDefinition;
            InstallThemeAwareColorizer();
        }

        private void RefreshTheme()
        {
            if (_disposed)
                return;

            if (!_editor.Dispatcher.CheckAccess())
            {
                _editor.Dispatcher.BeginInvoke(RefreshTheme);
                return;
            }

            _editor.TextArea.Caret.CaretBrush = FindBrush("EditorCaretBrush", Brushes.Black);
            InstallThemeAwareColorizer();
            _editor.TextArea.TextView.Redraw();
        }

        private void InstallThemeAwareColorizer()
        {
            if (_disposed)
                return;

            if (_colorizer != null)
            {
                _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
                _colorizer = null;
            }

            if (_highlightingDefinition != null)
            {
                _colorizer = new ThemeAwareHighlightingColorizer(
                    _highlightingDefinition,
                    resourceKey => _editor.TryFindResource(resourceKey) as Brush);
                _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
            }
        }

        private Brush FindBrush(string resourceKey, Brush fallback)
        {
            return _editor.TryFindResource(resourceKey) as Brush ?? fallback;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            ThemeManager.Current.CurrentUIThemeChanged -= _themeChangedHandler;
            if (_colorizer != null)
            {
                _editor.TextArea.TextView.LineTransformers.Remove(_colorizer);
                _colorizer = null;
            }
        }
    }

    internal sealed class ThemeAwareHighlightingColorizer : HighlightingColorizer
    {
        private readonly Dictionary<string, Brush> _brushes;

        public ThemeAwareHighlightingColorizer(
            IHighlightingDefinition highlightingDefinition,
            Func<string, Brush?> findBrush)
            : base(highlightingDefinition)
        {
            string[] resourceKeys =
            [
                "EditorForegroundBrush",
                "EditorSyntaxAddedBrush",
                "EditorSyntaxCommentBrush",
                "EditorSyntaxErrorBrush",
                "EditorSyntaxBackgroundBrush",
                "EditorSyntaxKeywordBrush",
                "EditorSyntaxMethodBrush",
                "EditorSyntaxNumberBrush",
                "EditorSyntaxPreprocessorBrush",
                "EditorSyntaxPropertyBrush",
                "EditorSyntaxStringBrush",
                "EditorSyntaxTagBrush",
                "EditorSyntaxTypeBrush",
                "EditorLinkBrush",
            ];

            _brushes = resourceKeys
                .Select(resourceKey => (resourceKey, brush: findBrush(resourceKey)))
                .Where(item => item.brush != null)
                .ToDictionary(item => item.resourceKey, item => item.brush!);
        }

        protected override void ApplyColorToElement(VisualLineElement element, HighlightingColor color)
        {
            base.ApplyColorToElement(element, color);

            string? resourceKey = GetForegroundBrushResourceKey(color);
            if (resourceKey != null && _brushes.TryGetValue(resourceKey, out Brush? brush))
                element.TextRunProperties.SetForegroundBrush(brush);

            string? backgroundResourceKey = GetBackgroundBrushResourceKey(color);
            if (backgroundResourceKey != null
                && _brushes.TryGetValue(backgroundResourceKey, out Brush? backgroundBrush))
            {
                element.BackgroundBrush = backgroundBrush;
            }
        }

        internal static string? GetForegroundBrushResourceKey(HighlightingColor color)
        {
            return GetBrushResourceKey(color.Name)
                ?? (color.Foreground != null ? "EditorForegroundBrush" : null);
        }

        internal static string? GetBackgroundBrushResourceKey(HighlightingColor color)
        {
            return color.Background != null ? "EditorSyntaxBackgroundBrush" : null;
        }

        internal static string? GetBrushResourceKey(string? colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return null;

            if (colorName.Equals("Value", StringComparison.OrdinalIgnoreCase))
                return "EditorSyntaxPropertyBrush";
            if (colorName.Equals("Position", StringComparison.OrdinalIgnoreCase))
                return "EditorSyntaxNumberBrush";
            if (colorName.Equals("Header", StringComparison.OrdinalIgnoreCase))
                return "EditorSyntaxPreprocessorBrush";

            if (ContainsAny(colorName, "Removed", "Broken", "UnknownAttribute"))
                return "EditorSyntaxErrorBrush";
            if (colorName.Contains("Added", StringComparison.OrdinalIgnoreCase))
                return "EditorSyntaxAddedBrush";
            if (ContainsAny(colorName, "Comment", "BlockQuote"))
                return "EditorSyntaxCommentBrush";
            if (ContainsAny(colorName, "String", "Character", "Char", "Regex", "AttributeValue", "DateLiteral", "CData", "Code"))
                return "EditorSyntaxStringBrush";
            if (ContainsAny(colorName, "Link", "Image", "Url", "FileName"))
                return "EditorLinkBrush";
            if (ContainsAny(colorName, "Number", "Digits"))
                return "EditorSyntaxNumberBrush";
            if (ContainsAny(colorName, "Method", "Function", "Command", "Intrinsic"))
                return "EditorSyntaxMethodBrush";
            if (ContainsAny(colorName, "FieldName", "Property", "AttributeName", "Attributes", "Variable", "Parameter", "Selector"))
                return "EditorSyntaxPropertyBrush";
            if (ContainsAny(colorName, "Preprocessor", "XmlDeclaration", "DocType", "ASPSection"))
                return "EditorSyntaxPreprocessorBrush";
            if (ContainsAny(colorName, "Tag", "Heading", "Entity"))
                return "EditorSyntaxTagBrush";
            if (ContainsAny(colorName, "Type", "Class", "Namespace", "Package"))
                return "EditorSyntaxTypeBrush";
            if (ContainsAny(colorName, "Keyword", "Statement", "Modifier", "Access", "Jump", "Control", "Operator", "Literal", "Constants", "TrueFalse", "Bool", "Null", "Void", "This", "Visibility", "GetSetAddRemove", "Friend", "ExceptionHandling"))
                return "EditorSyntaxKeywordBrush";
            if (ContainsAny(colorName, "Punctuation", "Assignment", "Slash", "Colon", "Brace", "Unchanged"))
                return "EditorForegroundBrush";

            // Unknown style-only roles (for example Markdown bold/italic) must keep the inherited
            // foreground. ApplyColorToElement falls back to EditorForegroundBrush only when the
            // grammar actually supplies a fixed foreground color.
            return null;
        }

        private static bool ContainsAny(string value, params string[] candidates)
        {
            return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
