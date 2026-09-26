using ColorVision.UI;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Reflection;
using System.IO;

namespace ProjectARVRPro;

public enum ProjectSettingsPage { Testing, Display, Results, Images }

internal sealed record ProjectSettingsGroup(object Source, IReadOnlyList<PropertyInfo> Properties);

internal sealed record ProjectSettingsSection(ProjectSettingsPage Id, string Title, string Description, IReadOnlyList<ProjectSettingsGroup> Groups)
{
    internal bool Matches(string query) => string.IsNullOrWhiteSpace(query)
        || $"{Title} {Description}".Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Groups.Any(group => group.Properties.Any(property =>
            $"{property.Name} {property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName} {property.GetCustomAttribute<CategoryAttribute>()?.Category} {property.GetCustomAttribute<DescriptionAttribute>()?.Description}"
                .Contains(query, StringComparison.CurrentCultureIgnoreCase)));
}

internal sealed record ProjectSettingsError(ProjectSettingsPage Page, string Message);

/// <summary>Validated editor values for the existing configuration identities; successful saves advance the baseline.</summary>
internal sealed class ProjectSettingsSession
{
    private sealed record Setting(object Source, object Draft, PropertyInfo Property, object? InitialValue)
    {
        internal object? SavedValue { get; set; } = InitialValue;
    }
    private readonly List<Setting> _settings = [];

    internal ProjectARVRProConfig Project { get; } = new();
    internal ViewResultManagerConfig Results { get; } = new();
    internal IReadOnlyList<ProjectSettingsSection> Sections { get; }
    internal bool HasChanges => _settings.Any(setting => !Equals(setting.SavedValue, setting.Property.GetValue(setting.Draft)));

    internal ProjectSettingsSession(ProjectARVRProConfig project, ViewResultManagerConfig results, bool sourceImageSupportsBmp)
    {
        CopyToDraft(project, Project);
        CopyToDraft(results, Results);
        // The two format selectors are UI adapters for one stored value. Copying both would turn BMP into TIFF.
        Results.SourceImageSupportsBmp = sourceImageSupportsBmp;
        if (!sourceImageSupportsBmp && Results.SourceExportFormat == SourceImageFormat.BMP)
            Results.SourceExportFormat = SourceImageFormat.TIFF;

        Sections =
        [
            Section(ProjectSettingsPage.Testing, "检测设置", "设置失败处理和 SN 锁定方式。", ["测试策略"]),
            Section(ProjectSettingsPage.Display, "界面显示", "调整工作区、结果图层和默认查询条数。", ["界面", "结果图层", "结果列表"]),
            Section(ProjectSettingsPage.Results, "结果保存", "设置保存位置、CSV、兼容格式和客户报表。", ["输出路径", "保存选项", "兼容格式", "客制化输出", "结果编号"]),
            Section(ProjectSettingsPage.Images, "图像保存", "标记图和原图保存在结果输出目录；开启后显示格式等选项。", ["标记图", "原图"]),
        ];
    }

    private ProjectSettingsSection Section(ProjectSettingsPage id, string title, string description, string[] categories)
    {
        var groups = new List<ProjectSettingsGroup>();
        foreach (object source in new object[] { Project, Results })
        {
            var properties = PropertyEditorHelper.GetEditableProperties(source.GetType())
                .Where(property => categories.Contains(property.GetCustomAttribute<CategoryAttribute>()?.Category))
                .OrderBy(property => Array.IndexOf(categories, property.GetCustomAttribute<CategoryAttribute>()?.Category)).ToArray();
            if (properties.Length > 0) groups.Add(new(source, properties));
        }
        return new(id, title, description, groups);
    }

    private void CopyToDraft(object source, object draft)
    {
        // Runtime state is excluded by Browsable(false); the canonical source format is the sole stored exception.
        foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                && property.GetCustomAttribute<JsonIgnoreAttribute>() == null
                && property.GetCustomAttribute<CategoryAttribute>()?.Category != "串口参数"
                && (property.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false
                    || property.Name == nameof(ViewResultManagerConfig.SourceExportFormat))))
        {
            object? value = property.GetValue(source);
            property.SetValue(draft, value);
            _settings.Add(new(source, draft, property, value));
        }
    }

    internal void RestoreDefaults()
    {
        var projectDefaults = new ProjectARVRProConfig();
        var resultDefaults = new ViewResultManagerConfig();
        foreach (var setting in _settings)
            setting.Property.SetValue(setting.Draft, setting.Property.GetValue(setting.Draft == Project ? projectDefaults : resultDefaults));
    }

    internal ProjectSettingsError? Validate()
    {
        foreach (var setting in _settings)
        {
            if (ValidateValue(setting.Draft, setting.Property.Name, setting.Property.GetValue(setting.Draft)) is not { } error) continue;
            var section = Sections.First(section => section.Groups.Any(group => group.Source == setting.Draft && group.Properties.Contains(setting.Property)));
            return new(section.Id, error);
        }
        return null;
    }

    internal static string? ValidateValue(object source, string property, object? value)
    {
        if (source is ProjectARVRProConfig)
        {
            if (property == nameof(ProjectARVRProConfig.TryCountMax) && value is int count && count < 0) return "最大尝试次数不能小于0。";
            if (property == nameof(ProjectARVRProConfig.ResultOverlayFontSize) && value is double size && !double.IsFinite(size)) return "结果文字字号必须是有效数值。";
        }
        if (source is ViewResultManagerConfig)
        {
            if (property == nameof(ViewResultManagerConfig.CodeDateFormat))
            {
                try { _ = DateTime.Now.ToString(value as string); }
                catch (FormatException) { return "编号时间格式无效，请使用有效的日期时间格式。"; }
            }
            if (property is nameof(ViewResultManagerConfig.CsvSavePath) or nameof(ViewResultManagerConfig.CustomXlsxSavePath))
            {
                string path = value as string ?? "";
                if (string.IsNullOrWhiteSpace(path))
                    return property == nameof(ViewResultManagerConfig.CustomXlsxSavePath) ? null : "结果输出目录不能为空。";
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    string relative = fullPath[(Path.GetPathRoot(fullPath)?.Length ?? 0)..];
                    if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)) return "目录包含无效字符。";
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return "目录格式无效。"; }
            }
        }
        return null;
    }

    internal void SaveChange(object draft, string property, Action persist)
    {
        var setting = _settings.FirstOrDefault(setting => setting.Draft == draft && setting.Property.Name == property);
        if (setting == null) return; // Derived visibility and format-adapter notifications are not stored values.
        if (ValidateValue(draft, property, setting.Property.GetValue(draft)) is { } error) throw new InvalidOperationException(error);
        if (Equals(setting.SavedValue, setting.Property.GetValue(draft))) return;
        // Preserve the existing high-bit BMP normalization on the first real edit, not merely on opening.
        SaveSettings(_settings.Where(candidate => candidate == setting || (candidate.Draft == Results
            && !Results.SourceImageSupportsBmp && candidate.Property.Name == nameof(ViewResultManagerConfig.SourceExportFormat))), persist);
    }

    internal void Save(Action persist)
    {
        if (Validate() is { } error) throw new InvalidOperationException(error.Message);
        SaveSettings(_settings, persist);
    }

    private static void SaveSettings(IEnumerable<Setting> settings, Action persist)
    {
        var changes = settings.Where(setting => !Equals(setting.SavedValue, setting.Property.GetValue(setting.Draft)))
            .Select(setting => (Setting: setting, Previous: setting.Property.GetValue(setting.Source))).ToArray();
        if (changes.Length == 0) return;
        try
        {
            foreach (var (setting, _) in changes)
                setting.Property.SetValue(setting.Source, setting.Property.GetValue(setting.Draft));
            persist();
            foreach (var (setting, _) in changes) setting.SavedValue = setting.Property.GetValue(setting.Draft);
        }
        catch
        {
            foreach (var (setting, previous) in changes)
                setting.Property.SetValue(setting.Source, previous);
            throw;
        }
    }
}
