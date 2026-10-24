using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Editor;

public enum FlowEditorThemeMode
{
    [Description("跟随软件主题")] FollowApplication,
    [Description("浅色")] Light,
    [Description("深色")] Dark,
    [Description("自定义配色")] Custom
}

/// <summary>Local presentation preferences shared by flow editors, never stored in a flow document.</summary>
[DisplayName("工作流程设置")]
public sealed class FlowEditorSettings : ViewModelBase, IConfig
{
    [Category("主题"), DisplayName("画布主题")]
    [Description("默认跟随软件主题。选择浅色、深色或自定义配色后，仅改变流程编辑器的显示。")]
    public FlowEditorThemeMode ThemeMode
    {
        get => _themeMode;
        set => SetProperty(ref _themeMode, Enum.IsDefined(value) ? value : FlowEditorThemeMode.FollowApplication);
    }
    private FlowEditorThemeMode _themeMode;

    [Category("画布"), DisplayName("显示网格")]
    public bool ShowGrid { get => _showGrid; set => SetProperty(ref _showGrid, value); }
    private bool _showGrid = true;

    [Category("画布"), DisplayName("节点对齐辅助")]
    [Description("拖动节点时显示对齐辅助线，并吸附到相邻节点的对齐位置。")]
    public bool ShowMagnet { get => _showMagnet; set => SetProperty(ref _showMagnet, value); }
    private bool _showMagnet = true;

    [Category("画布"), DisplayName("显示节点阴影")]
    public bool ShowNodeShadow { get => _showNodeShadow; set => SetProperty(ref _showNodeShadow, value); }
    private bool _showNodeShadow;

    [Category("画布"), DisplayName("节点圆角"), Range(0, 24)]
    public int NodeCornerRadius { get => _nodeCornerRadius; set => SetProperty(ref _nodeCornerRadius, Math.Clamp(value, 0, 24)); }
    private int _nodeCornerRadius = 10;

    [Category("画布"), DisplayName("连线弯曲度"), Range(0.0, 1.0), PropertyEditorType(typeof(SliderPropertiesEditor))]
    [Description("范围 0–1，0 为直线。")]
    public double Curvature { get => _curvature; set => SetProperty(ref _curvature, double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.3); }
    private double _curvature = 0.3;

    [Category("自定义配色"), DisplayName("使用深色控件"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    [Description("控制工具栏、菜单和属性编辑器的基础明暗；下方颜色只在自定义配色模式生效。")]
    public bool CustomIsDark { get => _customIsDark; set => SetProperty(ref _customIsDark, value); }
    private bool _customIsDark = true;

    [Category("自定义配色"), DisplayName("画布背景"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color CanvasColor { get => _canvasColor; set => SetProperty(ref _canvasColor, Opaque(value)); }
    private Color _canvasColor = Color.FromRgb(34, 36, 38);

    [Category("自定义配色"), DisplayName("节点背景"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color SurfaceColor { get => _surfaceColor; set => SetProperty(ref _surfaceColor, Opaque(value)); }
    private Color _surfaceColor = Color.FromRgb(56, 58, 62);

    [Category("自定义配色"), DisplayName("主网格线"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color GridMajorColor { get => _gridMajorColor; set => SetProperty(ref _gridMajorColor, Opaque(value)); }
    private Color _gridMajorColor = Color.FromRgb(48, 51, 55);

    [Category("自定义配色"), DisplayName("次网格线"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color GridMinorColor { get => _gridMinorColor; set => SetProperty(ref _gridMinorColor, Opaque(value)); }
    private Color _gridMinorColor = Color.FromRgb(41, 44, 47);

    [Category("自定义配色"), DisplayName("文字颜色"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color TextColor { get => _textColor; set => SetProperty(ref _textColor, Opaque(value)); }
    private Color _textColor = Color.FromRgb(236, 238, 242);

    [Category("自定义配色"), DisplayName("辅助文字"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color SecondaryTextColor { get => _secondaryTextColor; set => SetProperty(ref _secondaryTextColor, Opaque(value)); }
    private Color _secondaryTextColor = Color.FromRgb(180, 187, 198);

    [Category("自定义配色"), DisplayName("节点边框"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color BorderColor { get => _borderColor; set => SetProperty(ref _borderColor, Opaque(value)); }
    private Color _borderColor = Color.FromRgb(72, 76, 82);

    [Category("自定义配色"), DisplayName("强调颜色"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    [Description("用于选中边框、蓝色节点标题和对应端口连线。")]
    public Color AccentColor { get => _accentColor; set => SetProperty(ref _accentColor, Opaque(value)); }
    private Color _accentColor = Color.FromRgb(85, 155, 224);

    [Category("自定义配色"), DisplayName("成功颜色"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color SuccessColor { get => _successColor; set => SetProperty(ref _successColor, Opaque(value)); }
    private Color _successColor = Color.FromRgb(71, 163, 109);

    [Category("自定义配色"), DisplayName("错误颜色"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color ErrorColor { get => _errorColor; set => SetProperty(ref _errorColor, Opaque(value)); }
    private Color _errorColor = Color.FromRgb(212, 92, 92);

    [Category("自定义配色"), DisplayName("警示颜色"), PropertyVisibility(nameof(ThemeMode), FlowEditorThemeMode.Custom)]
    public Color WarningColor { get => _warningColor; set => SetProperty(ref _warningColor, Opaque(value)); }
    private Color _warningColor = Color.FromRgb(182, 139, 45);

    internal STNodeVisualTheme ResolveTheme(Theme applicationTheme)
    {
        if (ThemeMode != FlowEditorThemeMode.Custom)
        {
            bool isDark = ThemeMode == FlowEditorThemeMode.Dark
                || (ThemeMode == FlowEditorThemeMode.FollowApplication && applicationTheme == Theme.Dark);
            return isDark ? STNodeVisualTheme.Dark : STNodeVisualTheme.Light;
        }

        return new STNodeVisualTheme(CustomIsDark)
        {
            Canvas = ToDrawing(CanvasColor), Surface = ToDrawing(SurfaceColor),
            GridMajor = ToDrawing(GridMajorColor), GridMinor = ToDrawing(GridMinorColor),
            Text = ToDrawing(TextColor), SecondaryText = ToDrawing(SecondaryTextColor),
            Border = ToDrawing(BorderColor), Accent = ToDrawing(AccentColor),
            TitleAccent = ToDrawing(AccentColor), ProgressAccent = ToDrawing(AccentColor),
            TitleSuccess = ToDrawing(SuccessColor),
            Success = ToDrawing(SuccessColor), Error = ToDrawing(ErrorColor), Warning = ToDrawing(WarningColor)
        };
    }

    private static Color Opaque(Color color) => Color.FromRgb(color.R, color.G, color.B);
    private static System.Drawing.Color ToDrawing(Color color) => System.Drawing.Color.FromArgb(color.R, color.G, color.B);
}
