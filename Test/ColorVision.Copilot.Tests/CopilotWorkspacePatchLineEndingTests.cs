using System.IO;

namespace ColorVision.Copilot.Tests;

// Read results hide line endings, so an edit copied from them must survive files that mix endings.
public sealed class CopilotWorkspacePatchLineEndingTests : IDisposable
{
    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotPatchLineEndings-").FullName;
    private readonly CopilotWorkspacePatchStore _store = new();

    [Theory]
    [InlineData("one\r\ntwo\r\nthree\nfour\nfive\r\n", "three\nfour", "THREE\nFOUR", "one\r\ntwo\r\nTHREE\nFOUR\nfive\r\n")]
    [InlineData("one\r\ntwo\r\nthree\nfour\nfive\r\n", "three\r\nfour", "THREE\r\nFOUR", "one\r\ntwo\r\nTHREE\nFOUR\nfive\r\n")]
    [InlineData("one\r\ntwo\nthree\r\n", "one\ntwo\nthree", "1\n2", "1\r\n2\r\n")]
    [InlineData("one\r\ntwo\nthree\r\n", "two", "2\n2b", "one\r\n2\n2b\nthree\r\n")]
    [InlineData("alpha\nbeta\n", "beta", "b\r\nc", "alpha\nb\nc\n")]
    public async Task EditMatchesAcrossLineEndingsAndKeepsTheLocalStyle(string original, string oldText, string newText, string expected)
    {
        var path = Write("mixed.txt", original);
        var preview = await PreviewAsync(new { operation = "update", path, oldText, newText });
        Assert.True(preview.Success, preview.ErrorMessage);
        var input = ChangeSet(preview);

        Assert.True((await _store.ApplyPatchEnvelopeAsync(Request(), input, CancellationToken.None)).Success);
        Assert.Equal(expected, File.ReadAllText(path));

        Assert.True((await _store.RollbackPatchEnvelopeAsync(Request(), input, CancellationToken.None)).Success);
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public async Task SameTextInDifferentLineEndingsIsAmbiguous()
    {
        var path = Write("twice.txt", "a\nb\nsep\r\na\r\nb\r\n");
        var preview = await PreviewAsync(new { operation = "update", path, oldText = "a\nb", newText = "c" });
        Assert.False(preview.Success);
        Assert.Equal(CopilotToolFailureKind.Conflict, preview.FailureKind);
        Assert.Contains("2 locations", preview.ErrorMessage);
    }

    [Fact]
    public async Task EmptyFileCanBeFilledByTheDeleteThenAddItsErrorDescribes()
    {
        var path = Write("empty.txt", string.Empty);
        var rejected = await PreviewAsync(new { operation = "add", path, content = "hello" });
        Assert.False(rejected.Success);
        Assert.Contains("delete it", rejected.ErrorMessage);

        var delete = await PreviewAsync(new { operation = "delete", path });
        Assert.True(delete.Success, delete.ErrorMessage);
        Assert.True((await _store.ApplyPatchEnvelopeAsync(Request(), ChangeSet(delete), CancellationToken.None)).Success);
        var add = await PreviewAsync(new { operation = "add", path, content = "hello" });
        Assert.True(add.Success, add.ErrorMessage);
        Assert.True((await _store.ApplyPatchEnvelopeAsync(Request(), ChangeSet(add), CancellationToken.None)).Success);
        Assert.Equal("hello", File.ReadAllText(path));
    }

    public void Dispose() => Directory.Delete(_workspace, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_workspace, name);
        File.WriteAllText(path, content);
        return path;
    }

    private Task<CopilotToolResult> PreviewAsync(object operation) => _store.PreviewPatchEnvelopeAsync(Request(), new()
    {
        Arguments = new Dictionary<string, object?> { ["operations"] = new[] { operation } },
    }, CancellationToken.None);

    private static CopilotAgentToolInput ChangeSet(CopilotToolResult preview) => new()
    {
        Arguments = new Dictionary<string, object?>
        {
            ["changeSetId"] = preview.Content.Split('\n').Single(line => line.StartsWith("change_set_id:", StringComparison.Ordinal))["change_set_id:".Length..].Trim(),
        },
    };

    private CopilotAgentRequest Request() => new()
    {
        ConversationId = "line-endings", TaskId = "patch", WorkspacePath = _workspace,
        Mode = CopilotAgentMode.Code, UserText = "Apply the patch.", TaskIntentText = "Apply the patch.",
        WritableLocalRootPaths = [_workspace], SearchRootPaths = [_workspace], CodexHooksEnabled = false,
    };
}
