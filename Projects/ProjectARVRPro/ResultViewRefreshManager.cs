using ColorVision.Common.MVVM;
using ColorVision.Engine;
using ColorVision.UI;
using ColorVision.UI.Views;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ProjectARVRPro;

public sealed class ResultViewRefreshSettingItem : ViewModelBase
{
    private bool _isAutoRefreshEnabled;

    internal ResultViewRefreshSettingItem(ViewConfigBase config, string displayName, IReadOnlyList<string> instanceNames)
    {
        Config = config;
        DisplayName = displayName;
        InstanceNames = instanceNames;
        OriginalValue = config.AutoRefreshView;
        _isAutoRefreshEnabled = OriginalValue;
    }

    internal ViewConfigBase Config { get; }
    internal bool OriginalValue { get; }
    public string DisplayName { get; }
    public IReadOnlyList<string> InstanceNames { get; }
    public int AffectedCount => InstanceNames.Count;

    public string InstanceSummary => AffectedCount == 0
        ? "当前未检测到已加载实例；设置仍会保存供下次启动使用"
        : $"影响 {AffectedCount} 个视图：{string.Join("、", InstanceNames)}";

    public bool IsAutoRefreshEnabled
    {
        get => _isAutoRefreshEnabled;
        set
        {
            if (_isAutoRefreshEnabled == value)
                return;

            _isAutoRefreshEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsWarning));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public bool IsWarning => AffectedCount > 0 && IsAutoRefreshEnabled;

    public string StatusText => AffectedCount == 0
        ? "未加载"
        : IsAutoRefreshEnabled ? "正在刷新" : "已关闭";

    internal void Apply() => Config.AutoRefreshView = IsAutoRefreshEnabled;

    internal void Restore() => Config.AutoRefreshView = OriginalValue;
}

internal static class ResultViewRefreshDiscovery
{
    private sealed record RegisteredViewInstance(ViewConfigBase Config, string Identity, string DisplayName);

    private static readonly IReadOnlyDictionary<string, string> FriendlyNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ViewCameraConfig"] = "相机结果视图",
            ["ViewAlgorithmConfig"] = "算法结果视图",
            ["ViewCalibrationConfig"] = "校正结果视图",
            ["ViewSpectrumConfig"] = "光谱结果视图",
            ["ViewSMUConfig"] = "SMU 结果视图",
            ["ViewThirdPartyAlgorithmsConfig"] = "第三方算法结果视图",
        };

    internal static ObservableCollection<ResultViewRefreshSettingItem> Discover()
    {
        Dictionary<Type, ViewConfigBase> configs = ConfigHandler.GetInstance().Configs.Values
            .OfType<ViewConfigBase>()
            .GroupBy(config => config.GetType())
            .ToDictionary(group => group.Key, group => group.First());

        IReadOnlyList<RegisteredViewInstance> registeredViews = DiscoverRegisteredViewInstances(configs);

        // Reading a view's Config property can lazily register its singleton with
        // ConfigHandler. Include those registrations in this same discovery pass.
        foreach (ViewConfigBase config in ConfigHandler.GetInstance().Configs.Values.OfType<ViewConfigBase>())
            configs.TryAdd(config.GetType(), config);

        Dictionary<Type, Dictionary<string, string>> instances = configs.ToDictionary(
            config => config.Key,
            _ => new Dictionary<string, string>(StringComparer.Ordinal));

        AddDisplayControlInstances(instances);
        AddRegisteredViewInstances(instances, registeredViews);

        return new ObservableCollection<ResultViewRefreshSettingItem>(configs.Values
            .OrderBy(config => GetSortOrder(config.GetType()))
            .ThenBy(config => BuildDisplayName(config.GetType()), StringComparer.CurrentCulture)
            .Select(config => new ResultViewRefreshSettingItem(
                config,
                BuildDisplayName(config.GetType()),
                instances[config.GetType()].Values.OrderBy(name => name, StringComparer.CurrentCulture).ToArray())));
    }

    internal static string BuildDisplayName(Type configType)
    {
        if (FriendlyNames.TryGetValue(configType.Name, out string? friendlyName))
            return friendlyName;

        string name = configType.Name;
        if (name.StartsWith("View", StringComparison.Ordinal))
            name = name[4..];
        if (name.EndsWith("Config", StringComparison.Ordinal))
            name = name[..^6];
        return $"{SplitPascalCase(name)}结果视图";
    }

    internal static Type? ResolveDisplayConfigType(string displayControlTypeName, IEnumerable<Type> configTypes)
    {
        if (!displayControlTypeName.StartsWith("Display", StringComparison.Ordinal))
            return null;

        string expectedName = $"View{displayControlTypeName[7..]}Config";
        return configTypes.FirstOrDefault(type => string.Equals(type.Name, expectedName, StringComparison.Ordinal));
    }

    private static void AddDisplayControlInstances(Dictionary<Type, Dictionary<string, string>> instances)
    {
        Type[] configTypes = instances.Keys.ToArray();
        foreach (IDisPlayControl control in DisPlayManager.GetInstance().IDisPlayControls)
        {
            Type? configType = ResolveDisplayConfigType(control.GetType().Name, configTypes);
            if (configType != null)
            {
                string identity = $"{control.GetType().FullName}:{control.PersistenceKey}";
                instances[configType][identity] = control.DisPlayName;
            }
        }
    }

    private static IReadOnlyList<RegisteredViewInstance> DiscoverRegisteredViewInstances(Dictionary<Type, ViewConfigBase> configs)
    {
        var registeredViews = new List<RegisteredViewInstance>();
        DockViewManager manager = DockViewManager.GetInstance();
        foreach (Control view in manager.Views)
        {
            ViewConfigBase? config = ResolveViewConfig(view, configs);
            if (config == null)
                continue;

            configs.TryAdd(config.GetType(), config);
            string title = manager.ViewTitles.TryGetValue(view, out string? registeredTitle)
                ? registeredTitle
                : view.GetType().Name;
            string identity = $"{view.GetType().FullName}:{RuntimeHelpers.GetHashCode(view)}";
            registeredViews.Add(new RegisteredViewInstance(config, identity, title));
        }

        return registeredViews;
    }

    private static void AddRegisteredViewInstances(
        Dictionary<Type, Dictionary<string, string>> instances,
        IEnumerable<RegisteredViewInstance> registeredViews)
    {
        foreach (RegisteredViewInstance view in registeredViews)
        {
            Type configType = view.Config.GetType();
            if (instances.TryGetValue(configType, out Dictionary<string, string>? existingInstances)
                && existingInstances.Count > 0)
            {
                continue;
            }

            AddInstance(instances, configType, view.Identity, view.DisplayName);
        }
    }

    internal static void AddInstance(
        Dictionary<Type, Dictionary<string, string>> instances,
        Type configType,
        string identity,
        string displayName)
    {
        if (!instances.TryGetValue(configType, out Dictionary<string, string>? instanceNames))
        {
            instanceNames = new Dictionary<string, string>(StringComparer.Ordinal);
            instances[configType] = instanceNames;
        }

        instanceNames[identity] = displayName;
    }

    private static ViewConfigBase? ResolveViewConfig(Control view, IReadOnlyDictionary<Type, ViewConfigBase> configs)
    {
        if (view is FrameworkElement { DataContext: ViewConfigBase dataContextConfig })
            return dataContextConfig;

        try
        {
            PropertyInfo? configProperty = view.GetType().GetProperty("Config", BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            if (configProperty?.GetValue(configProperty.GetMethod?.IsStatic == true ? null : view) is ViewConfigBase config)
                return config;
        }
        catch
        {
            // A delayed view may not be initialized yet. Fall back to naming conventions below.
        }

        string viewTypeName = view.GetType().Name;
        string expectedName = viewTypeName.StartsWith("View", StringComparison.Ordinal)
            ? $"{viewTypeName}Config"
            : viewTypeName.EndsWith("View", StringComparison.Ordinal)
                ? $"View{viewTypeName[..^4]}Config"
                : string.Empty;
        return configs.Values.FirstOrDefault(config => string.Equals(config.GetType().Name, expectedName, StringComparison.Ordinal));
    }

    private static int GetSortOrder(Type configType) => configType.Name switch
    {
        "ViewCameraConfig" => 0,
        "ViewAlgorithmConfig" => 1,
        "ViewCalibrationConfig" => 2,
        _ => 10,
    };

    private static string SplitPascalCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length + 4);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (index > 0 && char.IsUpper(current) && !char.IsUpper(value[index - 1]))
                builder.Append(' ');
            builder.Append(current);
        }
        return builder.ToString();
    }
}
