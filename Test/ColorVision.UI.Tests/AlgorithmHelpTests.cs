using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.UI.Desktop.Help;
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmHelpTests
{
    [Fact]
    public void MarkdownUsesTheExactIndexedTopicAndNormalizesCrLfBeforeCheckingItsIndependentHash()
    {
        using var content = new TemporaryHelpContent();
        var document = content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId);
        Assert.Equal(TemporaryHelpContent.Markdown, document.Markdown);
        Assert.Equal(TemporaryHelpContent.MarkdownHash, document.ContentHash);
        Assert.Equal(document.Topic.MarkdownSha256, document.ContentHash);
        Assert.Contains("$D=100\\frac{a-b}{b}$", document.Markdown, StringComparison.Ordinal);
        Assert.Contains("<GridDistortionDemo />", document.Markdown, StringComparison.Ordinal);

        File.WriteAllText(Path.Combine(content.RootDirectory, "guide.md"), TemporaryHelpContent.Markdown.Replace("\n", "\r\n"), new UTF8Encoding(false));
        Assert.Equal(document, content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
        Assert.Throws<KeyNotFoundException>(() => content.Catalog.ReadMarkdown("../guide.md"));
        Assert.Throws<KeyNotFoundException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId.ToUpperInvariant()));
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("C:\\outside.md")]
    [InlineData("guide.html")]
    [InlineData("guide.md:unexpected.md")]
    [InlineData("")]
    public void MarkdownCannotResolveAnUnindexedOrEscapingContentPath(string path)
    {
        using var content = new TemporaryHelpContent();
        content.WriteIndex(markdownRelativePath: path);
        Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    public void MissingMalformedOrMismatchedMarkdownIntegrityMetadataCannotBecomeUsableHelp(string hash)
    {
        using var content = new TemporaryHelpContent();
        content.WriteIndex(markdownSha256: hash);
        Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n\t")]
    public void EmptyMarkdownIsInvalidEvenWhenItsHashMatches(string markdown)
    {
        using var content = new TemporaryHelpContent();
        File.WriteAllText(Path.Combine(content.RootDirectory, "guide.md"), markdown, new UTF8Encoding(false));
        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown.Replace("\r\n", "\n"))));
        content.WriteIndex(markdownSha256: hash);
        Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
    }

    [Theory]
    [InlineData("markdown-utf8")]
    [InlineData("index-utf8")]
    [InlineData("markdown-oversize")]
    [InlineData("index-oversize")]
    [InlineData("index-json")]
    [InlineData("index-duplicate")]
    public void InvalidOrUnboundedDeployedContentFailsClosed(string boundary)
    {
        using var content = new TemporaryHelpContent();
        string file = Path.Combine(content.RootDirectory, boundary.StartsWith("markdown-", StringComparison.Ordinal) ? "guide.md" : "index.json");
        if (boundary.EndsWith("-utf8", StringComparison.Ordinal)) File.WriteAllBytes(file, [0xc3, 0x28]);
        else if (boundary.EndsWith("-oversize", StringComparison.Ordinal)) File.WriteAllBytes(file, new byte[AlgorithmHelpCatalog.MaximumFileBytes + 1]);
        else if (boundary == "index-json") File.WriteAllText(file, "{invalid-json");
        else
        {
            var topic = content.Catalog.Get(TemporaryHelpContent.TopicId);
            File.WriteAllText(file, JsonSerializer.Serialize(new[] { topic, topic }));
        }
        Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
    }

    [Theory]
    [InlineData("markdown")]
    [InlineData("index")]
    [InlineData("directory")]
    public void MarkdownAndIndexCannotTraverseReparsePointsOutsideTheDeliveredFolder(string boundary)
    {
        using var content = new TemporaryHelpContent();
        using var outside = new TemporaryHelpContent();
        string link = Path.Combine(content.RootDirectory, boundary == "directory" ? "linked" : boundary == "index" ? "index.json" : "guide.md");
        try
        {
            if (boundary == "directory")
            {
                Directory.CreateSymbolicLink(link, outside.RootDirectory);
                content.WriteIndex(markdownRelativePath: "linked/guide.md");
            }
            else
            {
                File.Delete(link);
                File.CreateSymbolicLink(link, Path.Combine(outside.RootDirectory, Path.GetFileName(link)));
            }
            Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
        }
        finally
        {
            Assert.Equal(content.RootDirectory, Path.GetDirectoryName(Path.GetFullPath(link)), ignoreCase: true);
            if (boundary == "directory")
            {
                if (Directory.Exists(link)) Directory.Delete(link);
            }
            else if (File.Exists(link)) File.Delete(link);
        }
        Assert.True(File.Exists(Path.Combine(outside.RootDirectory, "guide.md")));
    }

    [Fact]
    public void FourParameterTopicMetadataStillSupportsHtmlWithoutMarkdownDelivery()
    {
        using var content = new TemporaryHelpContent();
        content.WriteIndex(markdownRelativePath: "", markdownSha256: "");
        Assert.Equal(TemporaryHelpContent.TopicId, content.Catalog.Get(TemporaryHelpContent.TopicId).Id);
        Assert.Throws<InvalidDataException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));
    }

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

        File.Delete(Path.Combine(content.RootDirectory, "guide.md"));
        Assert.Throws<FileNotFoundException>(() => content.Catalog.ReadMarkdown(TemporaryHelpContent.TopicId));

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
            AlgorithmHelpDocument document = catalog.ReadMarkdown(entry.Id);
            Assert.Equal(topic, document.Topic);
            Assert.Equal(topic.MarkdownSha256, document.ContentHash);
            Assert.Contains("algorithm-principles", document.Markdown, StringComparison.Ordinal);
            Assert.DoesNotContain("\r", document.Markdown, StringComparison.Ordinal);
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
        public const string Markdown = "## Example {#algorithm-principles}\n\n$D=100\\frac{a-b}{b}$\n\n<GridDistortionDemo />\n";
        public static string MarkdownHash { get; } = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Markdown))).ToLowerInvariant();
        public string RootDirectory { get; } = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "ColorVision.AlgorithmHelp." + Guid.NewGuid().ToString("N")));
        public AlgorithmHelpCatalog Catalog { get; }

        public TemporaryHelpContent()
        {
            Directory.CreateDirectory(RootDirectory);
            Catalog = new AlgorithmHelpCatalog(RootDirectory);
            File.WriteAllText(Path.Combine(RootDirectory, "guide.html"), "<h1>Example help</h1>");
            File.WriteAllText(Path.Combine(RootDirectory, "guide.md"), Markdown, new UTF8Encoding(false));
            WriteIndex();
        }

        public void WriteIndex(string relativePath = "guide.html", string onlineUrl = "https://example.com/guide",
            string markdownRelativePath = "guide.md", string? markdownSha256 = null)
        {
            var topic = new AlgorithmHelpTopic(TopicId, "Example", relativePath, onlineUrl)
            {
                MarkdownRelativePath = markdownRelativePath, MarkdownSha256 = markdownSha256 ?? MarkdownHash,
            };
            File.WriteAllText(Path.Combine(RootDirectory, "index.json"), JsonSerializer.Serialize(new[] { topic }));
        }

        public void Dispose()
        {
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(Path.GetFullPath(RootDirectory)), ignoreCase: true);
            Assert.StartsWith("ColorVision.AlgorithmHelp.", Path.GetFileName(RootDirectory), StringComparison.Ordinal);
            foreach (string file in Directory.EnumerateFiles(RootDirectory)) File.Delete(file);
            Directory.Delete(RootDirectory);
        }
    }
}
