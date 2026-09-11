using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.ComponentModel;

namespace ColorVision.Solution.Editor.AvalonEditor;

public enum EditorFont { CascadiaMono, Consolas, CascadiaCode }

[DisplayName("文本编辑器")]
public sealed class EditorPreferences : ViewModelBase, IConfig
{
    [Category("编辑"), DisplayName("括号与引号自动配对")]
    public bool AutoClosePairs { get => _autoClosePairs; set { _autoClosePairs = value; OnPropertyChanged(); } }
    private bool _autoClosePairs = true;

    [Category("编辑"), DisplayName("输入时显示补全"), Description("语言关键词、文档词语和代码片段；Ctrl+Space 手动触发。")]
    public bool ShowCompletion { get => _showCompletion; set { _showCompletion = value; OnPropertyChanged(); } }
    private bool _showCompletion = true;

    [Category("显示"), DisplayName("匹配括号高亮")]
    public bool HighlightBrackets { get => _highlightBrackets; set { _highlightBrackets = value; OnPropertyChanged(); } }
    private bool _highlightBrackets = true;

    [Category("文字"), DisplayName("代码字体")]
    public EditorFont Font { get => _font; set { _font = value; OnPropertyChanged(); } }
    private EditorFont _font;

    [Category("文字"), DisplayName("字号"), Description("设备无关像素，范围 10–32。Ctrl+滚轮可以临时缩放。")]
    public double FontSize { get => _fontSize; set { _fontSize = double.IsFinite(value) ? Math.Clamp(value, 10, 32) : 14; OnPropertyChanged(); } }
    private double _fontSize = 14;

    [Category("文字"), DisplayName("加粗语法关键词")]
    public bool BoldKeywords { get => _boldKeywords; set { _boldKeywords = value; OnPropertyChanged(); } }
    private bool _boldKeywords;

    [Category("导航"), DisplayName("显示代码地图")]
    public bool ShowMinimap { get => _showMinimap; set { _showMinimap = value; OnPropertyChanged(); } }
    private bool _showMinimap = true;

    [Category("导航"), DisplayName("代码地图宽度"), Description("范围 60–180。")]
    public double MinimapWidth { get => _minimapWidth; set { _minimapWidth = double.IsFinite(value) ? Math.Clamp(value, 60, 180) : 104; OnPropertyChanged(); } }
    private double _minimapWidth = 104;

    [Category("导航"), DisplayName("悬停代码预览")]
    public bool ShowPreview { get => _showPreview; set { _showPreview = value; OnPropertyChanged(); } }
    private bool _showPreview = true;

    [Category("编辑"), DisplayName("缩进宽度")]
    public int IndentationSize { get => _indentationSize; set { _indentationSize = Math.Clamp(value, 1, 8); OnPropertyChanged(); } }
    private int _indentationSize = 4;

    [Category("编辑"), DisplayName("使用空格缩进")]
    public bool ConvertTabsToSpaces { get => _convertTabsToSpaces; set { _convertTabsToSpaces = value; OnPropertyChanged(); } }
    private bool _convertTabsToSpaces = true;

    [Category("显示"), DisplayName("缩进参考线")]
    public bool ShowIndentGuides { get => _showIndentGuides; set { _showIndentGuides = value; OnPropertyChanged(); } }
    private bool _showIndentGuides = true;

    [Category("显示"), DisplayName("自动换行")]
    public bool WordWrap { get => _wordWrap; set { _wordWrap = value; OnPropertyChanged(); } }
    private bool _wordWrap;

    [Category("显示"), DisplayName("显示行号")]
    public bool ShowLineNumbers { get => _showLineNumbers; set { _showLineNumbers = value; OnPropertyChanged(); } }
    private bool _showLineNumbers = true;

    [Category("显示"), DisplayName("显示空白字符")]
    public bool ShowWhitespace { get => _showWhitespace; set { _showWhitespace = value; OnPropertyChanged(); } }
    private bool _showWhitespace;

    internal string FontFamilyName => Font switch
    {
        EditorFont.Consolas => "Consolas, Microsoft YaHei UI",
        EditorFont.CascadiaCode => "Cascadia Code, Consolas, Microsoft YaHei UI",
        _ => "Cascadia Mono, Consolas, Microsoft YaHei UI"
    };
}
