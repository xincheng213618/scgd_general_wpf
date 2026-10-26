using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI.Desktop.Help;
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmHelpTests
{
    [Fact]
    public void CatalogResolvesPackagedPageAndLocalOriginFromStableTopicId()
    {
        using var content = new TemporaryHelpContent();
        AlgorithmHelpTopic topic = content.Catalog.Get(TemporaryHelpContent.TopicId);

        Assert.Equal(Path.Combine(content.RootDirectory, "guide.html"), content.Catalog.ResolvePath(topic));
        Uri address = content.Catalog.GetLocalUri(topic);
        Assert.Equal(Uri.UriSchemeHttps, address.Scheme);
        Assert.Equal(AlgorithmHelpCatalog.LocalHost, address.Host);
        Assert.Equal("/guide.html", address.AbsolutePath);
        Assert.Equal(topic, content.Catalog.GetByLocalUri(new Uri(address.AbsoluteUri + "#metrics")));
    }

    [Theory]
    [InlineData("http://help.colorvision.invalid/guide.html")]
    [InlineData("https://example.com/guide.html")]
    [InlineData("guide.html")]
    public void LocalNavigationOnlyResolvesTheDeliveredHelpOrigin(string address)
    {
        using var content = new TemporaryHelpContent();
        Assert.Throws<InvalidDataException>(() => content.Catalog.GetByLocalUri(new Uri(address, UriKind.RelativeOrAbsolute)));
        Assert.Throws<KeyNotFoundException>(() => content.Catalog.GetByLocalUri(new Uri($"https://{AlgorithmHelpCatalog.LocalHost}/unknown.html")));
    }

    [Theory]
    [InlineData("../outside.html")]
    [InlineData("C:\\outside.html")]
    [InlineData("guide.md")]
    [InlineData("")]
    public void CatalogRejectsPathsOutsideTheDeliveredHtmlContract(string relativePath)
    {
        using var content = new TemporaryHelpContent();
        content.WriteIndex(relativePath: relativePath);

        Assert.Throws<InvalidDataException>(() => content.Catalog.Get(TemporaryHelpContent.TopicId));
    }

    [Theory]
    [InlineData("http://example.com/guide")]
    [InlineData("javascript:alert(1)")]
    [InlineData("guide.html")]
    public void CatalogRejectsInvalidOnlineDestinations(string onlineUrl)
    {
        using var content = new TemporaryHelpContent();
        content.WriteIndex(onlineUrl: onlineUrl);

        Assert.Throws<InvalidDataException>(() => content.Catalog.Get(TemporaryHelpContent.TopicId));
    }

    [Fact]
    public void UnknownTopicMissingPageAndMissingIndexCannotBecomeValidBlankHelp()
    {
        using var content = new TemporaryHelpContent();
        Assert.Throws<KeyNotFoundException>(() => content.Catalog.Get("unknown.topic"));

        File.Delete(Path.Combine(content.RootDirectory, "guide.html"));
        Assert.Throws<FileNotFoundException>(() => content.Catalog.Get(TemporaryHelpContent.TopicId));

        File.Delete(Path.Combine(content.RootDirectory, "index.json"));
        Assert.Throws<FileNotFoundException>(() => content.Catalog.Get(TemporaryHelpContent.TopicId));
    }

    [Fact]
    public void EveryDeliveredAlgorithmTopicIncludesItsFiguresAndLocalResources()
    {
        var catalog = new AlgorithmHelpCatalog(Path.Combine(AppContext.BaseDirectory, "Assets", "Help"));
        AlgorithmHelpTopic[] topics = JsonSerializer.Deserialize<AlgorithmHelpTopic[]>(File.ReadAllText(Path.Combine(catalog.RootDirectory, "index.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        Assert.NotEmpty(topics);
        Assert.Equal(topics.Length, topics.Select(topic => topic.Id).Distinct(StringComparer.Ordinal).Count());
        foreach (AlgorithmHelpTopic entry in topics)
        {
            AlgorithmHelpTopic topic = catalog.Get(entry.Id);
            string html = File.ReadAllText(catalog.ResolvePath(topic));
            Assert.Contains("id=\"algorithm-principles\"", html, StringComparison.Ordinal);
            Assert.True(html.Contains("<svg", StringComparison.Ordinal) || html.Contains("data-grid-distortion-demo", StringComparison.Ordinal), $"Missing algorithm figure: {topic.Id}");
            MatchCollection references = Regex.Matches(html, "<(?:script|link)\\b[^>]*\\b(?:src|href)=[\"'](?<path>[^\"']+)[\"']", RegexOptions.IgnoreCase);
            Assert.NotEmpty(references.Cast<Match>());
            foreach (Match reference in references)
            {
                string relativePath = reference.Groups["path"].Value;
                Assert.False(Uri.TryCreate(relativePath, UriKind.Absolute, out _), relativePath);
                string file = Path.GetFullPath(Path.Combine(catalog.RootDirectory, relativePath));
                Assert.StartsWith(catalog.RootDirectory + Path.DirectorySeparatorChar, file, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(file), $"Missing delivered help resource: {topic.Id}/{relativePath}");
            }
        }
    }

    [Fact]
    public void EveryLocalCalculationNodeHasResolvableHelpWithoutInitializingABrowser()
    {
        WpfTestHost.Invoke(() =>
        {
            var catalog = new AlgorithmHelpCatalog(Path.Combine(AppContext.BaseDirectory, "Assets", "Help"));
            Type[] nodes = typeof(LocalGridDistortionNode).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(LocalFlowNodeBase).IsAssignableFrom(type)
                    && type.GetCustomAttribute<STNodeAttribute>()?.Path == "Flow_LocalAlgorithms"
                    && type != typeof(CommandLineScriptNode) && type != typeof(LocalCleanupNode))
                .ToArray();
            Assert.NotEmpty(nodes);
            foreach (Type type in nodes)
            {
                var node = (STNode)Activator.CreateInstance(type)!;
                FlowNodeDocumentation documentation = FlowNodeDocumentationPresenter.GetDocumentation(node);
                Assert.False(string.IsNullOrWhiteSpace(documentation.TopicId), $"Missing help topic for {type.Name}");
                Assert.Equal(documentation.TopicId, catalog.Get(documentation.TopicId).Id);
                var panel = Assert.IsType<StackPanel>(FlowNodeDocumentationPresenter.Create(node));
                Assert.Single(panel.Children.OfType<Button>());
                Assert.DoesNotContain(panel.Children.Cast<object>(), child => child.GetType().Name == "WebView2");
            }
        });
    }

    [Fact]
    public void AlgorithmEntryIsOptionalAndKeepsExistingNodeDocumentationCompatible()
    {
        WpfTestHost.Invoke(() =>
        {
            var distortion = new LocalGridDistortionNode();
            FlowNodeDocumentation documentation = FlowNodeDocumentationPresenter.GetDocumentation(distortion);
            Assert.Equal("algorithms.grid-distortion", documentation.TopicId);
            var panel = Assert.IsType<StackPanel>(FlowNodeDocumentationPresenter.Create(distortion));
            Assert.Single(panel.Children.OfType<Button>());

            var existingNode = new NodeWithoutAlgorithmHelp();
            FlowNodeDocumentation existingDocumentation = FlowNodeDocumentationPresenter.GetDocumentation(existingNode);
            Assert.Equal("Existing node summary", existingDocumentation.Summary);
            Assert.Empty(existingDocumentation.TopicId);
            var existingPanel = Assert.IsType<StackPanel>(FlowNodeDocumentationPresenter.Create(existingNode));
            Assert.Empty(existingPanel.Children.OfType<Button>());
            Assert.DoesNotContain(panel.Children.Cast<object>(), child => child.GetType().Name == "WebView2");
        });
    }

    [FlowNodeDocumentationAttribute("Existing node summary")]
    private sealed class NodeWithoutAlgorithmHelp : STNode { }

    private sealed class TemporaryHelpContent : IDisposable
    {
        public const string TopicId = "algorithms.example";
        public string RootDirectory { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.AlgorithmHelp." + Guid.NewGuid().ToString("N")));
        public AlgorithmHelpCatalog Catalog { get; }

        public TemporaryHelpContent()
        {
            Directory.CreateDirectory(RootDirectory);
            Catalog = new AlgorithmHelpCatalog(RootDirectory);
            File.WriteAllText(Path.Combine(RootDirectory, "guide.html"), "<h1>Example help</h1>");
            WriteIndex();
        }

        public void WriteIndex(string relativePath = "guide.html", string onlineUrl = "https://example.com/guide")
        {
            var topic = new AlgorithmHelpTopic(TopicId, "Example", relativePath, onlineUrl);
            File.WriteAllText(Path.Combine(RootDirectory, "index.json"), JsonSerializer.Serialize(new[] { topic }));
        }

        public void Dispose()
        {
            foreach (string file in Directory.EnumerateFiles(RootDirectory)) File.Delete(file);
            Directory.Delete(RootDirectory);
        }
    }
}
