using System.IO;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Help;

public sealed record AlgorithmHelpTopic(string Id, string Title, string RelativePath, string OnlineUrl);

/// <summary>Resolves stable topic IDs to files within the delivered offline help folder.</summary>
public sealed class AlgorithmHelpCatalog
{
    public const string LocalHost = "help.colorvision.invalid";
    public string RootDirectory { get; }

    public AlgorithmHelpCatalog(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public AlgorithmHelpTopic Get(string topicId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicId);
        AlgorithmHelpTopic topic = ReadTopics().SingleOrDefault(item => string.Equals(item.Id, topicId, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Algorithm help topic not found: {topicId}");
        return Validate(topic);
    }

    public AlgorithmHelpTopic GetByLocalUri(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (!address.IsAbsoluteUri || address.Scheme != Uri.UriSchemeHttps || address.Host != LocalHost)
            throw new InvalidDataException("The algorithm help address must use the local help origin.");
        string relativePath = Uri.UnescapeDataString(address.AbsolutePath).TrimStart('/');
        AlgorithmHelpTopic topic = ReadTopics().SingleOrDefault(item => string.Equals(item.RelativePath.Replace('\\', '/'), relativePath, StringComparison.Ordinal))
            ?? throw new KeyNotFoundException($"Algorithm help page not found: {relativePath}");
        return Validate(topic);
    }

    private AlgorithmHelpTopic[] ReadTopics() => JsonSerializer.Deserialize<AlgorithmHelpTopic[]>(File.ReadAllText(Path.Combine(RootDirectory, "index.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("The algorithm help index is empty.");

    private AlgorithmHelpTopic Validate(AlgorithmHelpTopic topic)
    {
        string path = ResolvePath(topic);
        if (!File.Exists(path)) throw new FileNotFoundException("The offline algorithm help page is missing.", path);
        if (!Uri.TryCreate(topic.OnlineUrl, UriKind.Absolute, out Uri? online) || online.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The online algorithm help address must use HTTPS.");
        return topic;
    }

    public string ResolvePath(AlgorithmHelpTopic topic)
    {
        if (string.IsNullOrWhiteSpace(topic.RelativePath) || Path.IsPathRooted(topic.RelativePath) || !topic.RelativePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid offline algorithm help path.");
        string path = Path.GetFullPath(Path.Combine(RootDirectory, topic.RelativePath));
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(RootDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The algorithm help path must stay inside its content folder.");
        return path;
    }

    public Uri GetLocalUri(AlgorithmHelpTopic topic) => new($"https://{LocalHost}/" + string.Join('/', topic.RelativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString)));
}
