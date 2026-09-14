using System.Globalization;

namespace Pattern;

public static class PatternText
{
    public static string Get(string key) =>
        Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);

    public static string ChartGenerationTool => Get(nameof(ChartGenerationTool));
    public static string Search => Get(nameof(Search));
    public static string TemplateFolder => Get(nameof(TemplateFolder));
    public static string OpenTemplateFolder => Get(nameof(OpenTemplateFolder));
    public static string OpenTemplateFolderTooltip => Get(nameof(OpenTemplateFolderTooltip));
    public static string Export => Get(nameof(Export));
    public static string ExportAllTemplatesZip => Get(nameof(ExportAllTemplatesZip));
    public static string Import => Get(nameof(Import));
    public static string ImportTemplateZip => Get(nameof(ImportTemplateZip));
    public static string ProjectionTool => Get(nameof(ProjectionTool));
    public static string OpenProjectionTool => Get(nameof(OpenProjectionTool));
    public static string GenerateAllTemplateImages => Get(nameof(GenerateAllTemplateImages));
    public static string GenerateAllTemplateImagesTooltip => Get(nameof(GenerateAllTemplateImagesTooltip));
    public static string OpenOutputFolder => Get(nameof(OpenOutputFolder));
    public static string OpenImageOutputFolderTooltip => Get(nameof(OpenImageOutputFolderTooltip));
    public static string Clear => Get(nameof(Clear));
    public static string ClearOutputDirectory => Get(nameof(ClearOutputDirectory));
    public static string SwitchTheme => Get(nameof(SwitchTheme));
    public static string ClearTemplateList => Get(nameof(ClearTemplateList));
    public static string EditSettings => Get(nameof(EditSettings));
    public static string FileName => Get(nameof(FileName));
    public static string EditPatternParameters => Get(nameof(EditPatternParameters));
    public static string ResetFactoryDefaults => Get(nameof(ResetFactoryDefaults));
    public static string ResetFactoryDefaultsTooltip => Get(nameof(ResetFactoryDefaultsTooltip));
    public static string Reset => Get(nameof(Reset));
    public static string ResetTooltip => Get(nameof(ResetTooltip));
    public static string SaveToTemplate => Get(nameof(SaveToTemplate));
    public static string SaveCurrentParametersToTemplate => Get(nameof(SaveCurrentParametersToTemplate));
    public static string SaveAsDefault => Get(nameof(SaveAsDefault));
    public static string SaveCurrentParametersAsDefault => Get(nameof(SaveCurrentParametersAsDefault));
    public static string GeneratePattern => Get(nameof(GeneratePattern));
    public static string GenerateCurrentPattern => Get(nameof(GenerateCurrentPattern));
    public static string SaveAs => Get(nameof(SaveAs));
    public static string SaveAsImageFile => Get(nameof(SaveAsImageFile));
    public static string TemplateLibrary => Get(nameof(TemplateLibrary));
    public static string TemplateActions => Get(nameof(TemplateActions));
    public static string PatternSettings => Get(nameof(PatternSettings));
    public static string PatternActions => Get(nameof(PatternActions));
    public static string OutputSize => Get(nameof(OutputSize));
    public static string ResolutionPreset => Get(nameof(ResolutionPreset));
    public static string ImageWidth => Get(nameof(ImageWidth));
    public static string ImageHeight => Get(nameof(ImageHeight));
    public static string PatternType => Get(nameof(PatternType));
    public static string Preview => Get(nameof(Preview));
    public static string PreviewEmptyTitle => Get(nameof(PreviewEmptyTitle));
    public static string PreviewEmptyHint => Get(nameof(PreviewEmptyHint));
    public static string NoTemplatesFound => Get(nameof(NoTemplatesFound));
    public static string SearchTemplates => Get(nameof(SearchTemplates));
    public static string GenerateHint => Get(nameof(GenerateHint));
    public static string ImageOutput => Get(nameof(ImageOutput));
}
