using ColorVision.UI.Controls;
using ColorVision.UI.Languages;
using ColorVision.ImageEditor.BatchProcessing;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UiResources = ColorVision.UI.Properties.Resources;

namespace ColorVision.UI.Tests;

public sealed class UiLocalizationConsistencyTests
{
    [Fact]
    public void CompiledEngineTraditionalResourcesCoverEnglishDynamicLookupKeys()
    {
        var manager = ColorVision.Engine.Properties.Resources.ResourceManager;
        var traditional = manager.GetResourceSet(CultureInfo.GetCultureInfo("zh-Hant"), true, false)!;
        var english = manager.GetResourceSet(CultureInfo.GetCultureInfo("en"), true, false)!;
        foreach (System.Collections.DictionaryEntry item in english)
            Assert.NotNull(traditional.GetString((string)item.Key));
        using var culture = new CultureScope("zh-Hant");
        Assert.Equal("開始", ST.Library.UI.Lang.Get("开始"));
        Assert.Equal("類別", ST.Library.UI.Lang.Get("类别"));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    public void RegionalLanguageNamesUseTheirParentTranslationAndUnknownLanguagesRemainVisible(string uiCulture)
    {
        using var culture = new CultureScope(uiCulture);
        Assert.Equal(UiResources.ResourceManager.GetString("en", CultureInfo.CurrentUICulture), LanguageManager.GetDisplayName("en-US"));
        Assert.Equal(UiResources.ResourceManager.GetString("zh-Hans", CultureInfo.CurrentUICulture), LanguageManager.GetDisplayName("zh-CN"));
        Assert.Equal(UiResources.ResourceManager.GetString("zh-Hant", CultureInfo.CurrentUICulture), LanguageManager.GetDisplayName("zh-TW"));
        Assert.Equal(CultureInfo.GetCultureInfo("de-DE").NativeName, LanguageManager.GetDisplayName("de-DE"));
        Assert.False(string.IsNullOrWhiteSpace(LanguageManager.GetDisplayName("en-GB")));
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    public void FlowStatusLocalizesKnownStatesAndRetainsCallerNamesAndDiagnosticCodes(string uiCulture)
    {
        using var culture = new CultureScope(uiCulture);
        Assert.Equal(UiResources.FlowStatusIdle, FlowExecutionStatusInfo.Idle.Label);
        var running = FlowExecutionStatusInfo.Running("客户 Flow A", "Node 17", 1200, 2500);
        Assert.Equal(FlowExecutionStatusKind.Running, running.Kind);
        Assert.Equal(UiResources.FlowStatusRunning, running.Label);
        Assert.Equal("1200 ms", running.ElapsedText);
        Assert.Contains("客户 Flow A", running.Details);
        Assert.Contains("Node 17", running.Details);
        Assert.Contains(1300.ToString("N0", CultureInfo.CurrentCulture), running.Details);

        var failed = FlowExecutionStatusInfo.Finished("Flow A", "Canceled", "PictureSwitchFailed", 1200);
        Assert.Equal(FlowExecutionStatusKind.Canceled, failed.Kind);
        Assert.Equal(UiResources.FlowStatusPictureSwitchFailed, failed.Message);
        Assert.Contains("PictureSwitchFailed", failed.Details);
        Assert.Contains("Canceled", failed.Details);
        var unknown = FlowExecutionStatusInfo.Finished("Flow A", "Failed", "vendor_error_42", 1200);
        Assert.Equal("vendor_error_42", unknown.Message);
    }

    [Fact]
    public void IdleStatusIsResolvedForTheCultureAtTheTimeItIsRequested()
    {
        using var chinese = new CultureScope("zh-CN");
        string initial = FlowExecutionStatusInfo.Idle.Label;
        using var english = new CultureScope("en-US");
        Assert.NotEqual(initial, FlowExecutionStatusInfo.Idle.Label);
        Assert.Equal("Idle", FlowExecutionStatusInfo.Idle.Label);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    public void LocalizedGroupNamesDoNotChangeStoredNamesOrSerializedConfiguration(string uiCulture)
    {
        using var culture = new CultureScope(uiCulture);
        var group = new DisPlayGroupConfig { Id = DisPlayManagerConfig.DefaultGroupId, Name = "默认分组" };
        Assert.Equal(UiResources.DisplayControlDefaultGroup, group.DisplayName);
        Assert.Equal("默认分组", group.Name);
        Assert.DoesNotContain("DisplayName", Newtonsoft.Json.JsonConvert.SerializeObject(group));
        Assert.DoesNotContain("DisplayName", System.Text.Json.JsonSerializer.Serialize(group));
        group.Id = "customer-group";
        group.Name = "客户 A";
        Assert.Equal("客户 A", group.DisplayName);
        List<string?> changed = [];
        group.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        group.Name = "Customer B";
        Assert.Contains(nameof(DisPlayGroupConfig.DisplayName), changed);
        Assert.Equal("Customer B", group.DisplayName);
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    [InlineData("zh-Hant")]
    public void BatchAlgorithmLabelsKeepCatalogIdentityAndOutputSuffix(string uiCulture)
    {
        using var culture = new CultureScope(uiCulture);
        foreach (var algorithm in BatchImageAlgorithms.CreateAll())
        {
            Assert.False(string.IsNullOrWhiteSpace(algorithm.DisplayName));
            if (algorithm.Descriptor != null)
            {
                Assert.Equal(algorithm.Descriptor.Name, algorithm.Name);
                Assert.Equal(algorithm.Descriptor.OutputSuffix, algorithm.Suffix);
            }
            if (uiCulture == "en-US") Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", algorithm.DisplayName);
        }
    }

    [Fact]
    public void RetainedResourceCulturesCoverNeutralKeysAndPreserveFormatArguments()
    {
        string root = FindRepositoryRoot();
        int families = 0;
        foreach (string directory in new[] { "ColorVision", "UI", "Engine", "Plugins", "Projects" })
        foreach (string neutralPath in Directory.EnumerateFiles(Path.Combine(root, directory), "*.resx", SearchOption.AllDirectories))
        {
            if (neutralPath.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj")) continue;
            string englishPath = Path.ChangeExtension(neutralPath, ".en.resx");
            if (!File.Exists(englishPath)) continue;
            families++;
            var neutral = ReadResources(neutralPath);
            string traditionalPath = Path.ChangeExtension(neutralPath, ".zh-Hant.resx");
            Assert.True(File.Exists(traditionalPath), $"Missing Traditional Chinese resource family: {neutralPath}");
            var keys = neutral.Keys.Union(ReadResources(englishPath).Keys).Union(ReadResources(traditionalPath).Keys).ToArray();
            foreach (string localizedPath in new[] { englishPath, traditionalPath })
            {
                var localized = ReadResources(localizedPath);
                foreach (string key in keys)
                {
                    // Dynamic Lang lookups can use the literal Chinese key as the neutral display text.
                    string value = neutral.GetValueOrDefault(key, key);
                    Assert.True(localized.TryGetValue(key, out string? translation), $"{localizedPath}: missing {key}");
                    if (string.IsNullOrWhiteSpace(value)) continue; // Empty legacy placeholder resources are not display text.
                    Assert.False(string.IsNullOrWhiteSpace(translation), $"{localizedPath}: empty {key}");
                    Assert.Equal(FormatArguments(value), FormatArguments(translation!));
                    if (localizedPath == englishPath)
                        Assert.DoesNotMatch(@"\p{IsCJKUnifiedIdeographs}", translation!);
                }
            }
        }
        Assert.True(families > 0);
    }

    [Fact]
    public void TraditionalResourcesForMaintenanceSearchAndHotkeysArePresentInBuiltSatellites()
    {
        var assemblies = new[]
        {
            (typeof(ColorVision.App).Assembly, "ColorVision.Recovery.StartupMaintenanceResources"),
            (typeof(ColorVision.App).Assembly, "ColorVision.Settings.Maintenance.MaintenanceResources"),
            (typeof(UiResources).Assembly, "ColorVision.UI.Serach.SearchPaletteResources"),
            (typeof(UiResources).Assembly, "ColorVision.UI.HotKey.FileHotkeyResources"),
            (typeof(UiResources).Assembly, "ColorVision.UI.HotKey.HotkeyEditorResources"),
            (typeof(UiResources).Assembly, "ColorVision.UI.HotKey.HotkeyPresentationResources"),
        };
        foreach (var (assembly, name) in assemblies)
        {
            var manager = new ResourceManager(name, assembly);
            try
            {
                Assert.NotNull(manager.GetResourceSet(CultureInfo.GetCultureInfo("zh-Hant"), true, false));
            }
            finally { manager.ReleaseAllResources(); }
        }
    }

    private static Dictionary<string, string> ReadResources(string path)
    {
        var items = XDocument.Load(path).Root!.Elements("data").ToArray();
        Assert.Equal(items.Length, items.Select(item => item.Attribute("name")!.Value).Distinct().Count());
        // GenerateResource treats case-only key differences as duplicates and silently drops one label.
        Assert.Equal(items.Length, items.Select(item => item.Attribute("name")!.Value).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        return items.ToDictionary(item => item.Attribute("name")!.Value, item => item.Element("value")?.Value ?? "");
    }

    private static string[] FormatArguments(string value) => Regex.Matches(value, @"(?<!\{)\{\d+(?:[^{}]*)\}(?!\})")
        .Select(match => match.Value).Order(StringComparer.Ordinal).ToArray();

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props"))) return directory.FullName;
        throw new DirectoryNotFoundException("ColorVision repository root was not found.");
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;
        public CultureScope(string culture) => CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
    }
}
