using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.UI.Desktop.Help;

public sealed record AlgorithmHelpTopic(string Id, string Title, string RelativePath, string OnlineUrl)
{
    public string MarkdownRelativePath { get; init; } = string.Empty;
    public string MarkdownSha256 { get; init; } = string.Empty;
}

public sealed record AlgorithmHelpDocument(AlgorithmHelpTopic Topic, string Markdown, string ContentHash);

/// <summary>Resolves stable topic IDs to files within the delivered offline help folder.</summary>
public sealed class AlgorithmHelpCatalog
{
    public const string LocalHost = "help.colorvision.invalid";
    public const int MaximumFileBytes = 1024 * 1024;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
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

    public AlgorithmHelpDocument ReadMarkdown(string exactTopicId)
    {
        RejectReparsePoints(Path.Combine(RootDirectory, "index.json"));
        AlgorithmHelpTopic topic = Get(exactTopicId);
        if (topic.MarkdownSha256 is not { Length: 64 } || !topic.MarkdownSha256.All(char.IsAsciiHexDigit))
            throw new InvalidDataException("The algorithm help Markdown hash is missing or invalid.");
        string path = ResolveContentPath(topic.MarkdownRelativePath, ".md");
        RejectReparsePoints(path);
        string markdown = ReadUtf8Text(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(markdown))
            throw new InvalidDataException("The offline algorithm help Markdown is empty.");
        string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown))).ToLowerInvariant();
        if (!string.Equals(contentHash, topic.MarkdownSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The algorithm help Markdown does not match its delivered hash.");
        return new(topic, markdown, contentHash);
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

    private AlgorithmHelpTopic[] ReadTopics()
    {
        try
        {
            string json = ReadUtf8Text(Path.Combine(RootDirectory, "index.json"));
            if (json.StartsWith('\uFEFF')) json = json[1..]; // Preserve the previous File.ReadAllText UTF-8 BOM behavior.
            var topics = JsonSerializer.Deserialize<AlgorithmHelpTopic[]>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (topics == null || topics.Length == 0 || topics.Any(topic => topic == null
                || string.IsNullOrWhiteSpace(topic.Id) || string.IsNullOrWhiteSpace(topic.Title)
                || string.IsNullOrWhiteSpace(topic.RelativePath) || string.IsNullOrWhiteSpace(topic.OnlineUrl))
                || topics.Select(topic => topic.Id).Distinct(StringComparer.Ordinal).Count() != topics.Length)
                throw new InvalidDataException("The algorithm help index is empty or contains invalid topic metadata.");
            return topics;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The algorithm help index is invalid.", exception);
        }
    }

    private AlgorithmHelpTopic Validate(AlgorithmHelpTopic topic)
    {
        string path = ResolvePath(topic);
        if (!File.Exists(path)) throw new FileNotFoundException("The offline algorithm help page is missing.", path);
        if (!Uri.TryCreate(topic.OnlineUrl, UriKind.Absolute, out Uri? online) || online.Scheme != Uri.UriSchemeHttps)
            throw new InvalidDataException("The online algorithm help address must use HTTPS.");
        return topic;
    }

    public string ResolvePath(AlgorithmHelpTopic topic)
        => ResolveContentPath(topic.RelativePath, ".html");

    private string ResolveContentPath(string relativePath, string extension)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)
            || relativePath.Contains(':') || !relativePath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Invalid offline algorithm help path.");
        string path;
        try { path = Path.GetFullPath(Path.Combine(RootDirectory, relativePath)); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidDataException("Invalid offline algorithm help path.", exception);
        }
        if (!path.StartsWith(Path.TrimEndingDirectorySeparator(RootDirectory) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The algorithm help path must stay inside its content folder.");
        return path;
    }

    private static string ReadUtf8Text(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaximumFileBytes)
                throw new InvalidDataException("The offline algorithm help file exceeds the 1 MiB read limit.");
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1)
                throw new InvalidDataException("The offline algorithm help file changed beyond its bounded length.");
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("The offline algorithm help file is not valid UTF-8.", exception);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("The offline algorithm help file changed while being read.", exception);
        }
        catch (DirectoryNotFoundException exception)
        {
            throw new FileNotFoundException("The offline algorithm help file is missing.", path, exception);
        }
    }

    private static void RejectReparsePoints(string path)
    {
        string current = Path.GetPathRoot(path)!;
        foreach (string segment in path[current.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileAttributes attributes;
            try { attributes = File.GetAttributes(current); }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException) { return; }
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("The offline algorithm help path cannot traverse a reparse point.");
        }
    }

    public Uri GetLocalUri(AlgorithmHelpTopic topic) => new($"https://{LocalHost}/" + string.Join('/', topic.RelativePath.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString)));
}
