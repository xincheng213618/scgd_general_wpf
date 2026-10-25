using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Themes;
using ColorVision.UI;
using ST.Library.UI.NodeEditor;
using System.Globalization;

namespace ColorVision.Engine.Templates.Flow;

internal sealed record FlowTemplateCoverStyle(STNodeVisualTheme Theme, bool ShowGrid, int CornerRadius, float Curvature)
{
    private readonly string cultureName = CultureInfo.CurrentUICulture.Name;
    internal static FlowTemplateCoverStyle Current
    {
        get
        {
            var settings = ConfigService.Instance == null ? new FlowEditorSettings()
                : ConfigService.Instance.GetRequiredService<FlowEditorSettings>();
            return new(settings.ResolveTheme(ThemeManager.Current.CurrentUITheme), settings.ShowGrid, settings.NodeCornerRadius, (float)settings.Curvature);
        }
    }

    internal string CacheKey => string.Join("|", cultureName, ShowGrid, CornerRadius,
        Curvature.ToString("R", CultureInfo.InvariantCulture), Theme.IsDark, Theme.Canvas.ToArgb(), Theme.Surface.ToArgb(),
        Theme.GridMajor.ToArgb(), Theme.GridMinor.ToArgb(), Theme.Text.ToArgb(), Theme.SecondaryText.ToArgb(),
        Theme.Border.ToArgb(), Theme.Accent.ToArgb(), Theme.Success.ToArgb(), Theme.Error.ToArgb(), Theme.Warning.ToArgb(),
        Theme.TitleAccent.ToArgb(), Theme.TitleSuccess.ToArgb());
}
