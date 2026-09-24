using System.IO;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotUnicodeFileReadTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotUnicodeFileReadTests-");

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task ReadAndSearchAgreeOnBomEncodedText(string encodingName)
    {
        const string source = "设备=相机🔬\r\nSUMMARY serial=CV-628 result=NG\r\n结束\r\n";
        var path = Path.Combine(_directory.FullName, "capture.log");
        await File.WriteAllTextAsync(path, source, GetEncoding(encodingName));
        var before = await File.ReadAllBytesAsync(path);
        var request = new CopilotAgentRequest
        {
            UserText = "读取 capture.log 中的 SUMMARY 并解释检测结果。",
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [_directory.FullName],
        };

        var result = await new CopilotReadLocalFileTool().ExecuteAsync(request, new CopilotAgentToolInput { Path = "capture.log" }, CancellationToken.None);
        var search = CopilotGrepTextCapability.Search([_directory.FullName], "SUMMARY", null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Contains("设备=相机🔬", result.Content);
        Assert.Contains("SUMMARY serial=CV-628 result=NG", result.Content);
        Assert.Contains("结束", result.Content);
        Assert.Equal(path, Assert.Single(result.SuccessfullyReadLocalFilePaths));
        Assert.True(search.Success, search.ErrorMessage);
        Assert.Equal(2, Assert.Single(search.Matches).LineNumber);
        Assert.Equal(before, await File.ReadAllBytesAsync(path));
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task ContinuationKeepsSupplementaryCharacterIntactAcrossReadBudget(string encodingName)
    {
        var path = Path.Combine(_directory.FullName, "long.log");
        await File.WriteAllTextAsync(path, new string('x', 999) + "🔬\r\n最终 result=NG\r\n", GetEncoding(encodingName));

        var first = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, null, null, null, 1000, CancellationToken.None);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(first.WasTruncated);
        Assert.Equal(1, first.ContinuationStartLine);
        Assert.Equal(1000, first.ContinuationStartColumn);
        Assert.Contains("kept the first 999 characters", first.Content);
        Assert.DoesNotContain(first.Content, char.IsSurrogate);

        var next = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, first.ContinuationStartLine, first.ContinuationStartColumn, null, 1000, CancellationToken.None);
        Assert.True(next.Success, next.ErrorMessage);
        Assert.False(next.WasTruncated);
        Assert.Equal("🔬\r\n最终 result=NG", next.Content);
        Assert.Equal(1, next.StartLine);
        Assert.Equal(1000, next.StartColumn);
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task DecodedNulBeyondPreviewDoesNotBecomeSuccessfulTextEvidence(string encodingName)
    {
        var path = Path.Combine(_directory.FullName, "binary.log");
        await File.WriteAllTextAsync(path, new string('x', 6000) + "\0not-text", GetEncoding(encodingName));

        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Contains("NUL", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task IncompleteUnicodeCodeUnitIsRejectedInsteadOfReplacingEvidence(string encodingName)
    {
        var path = Path.Combine(_directory.FullName, "incomplete.log");
        var encoding = GetEncoding(encodingName);
        await File.WriteAllBytesAsync(path, [.. encoding.GetPreamble(), .. encoding.GetBytes("result=NG"), 0xff]);

        var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Contains("encoding", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static Encoding GetEncoding(string name) => name switch
    {
        "utf8" => new UTF8Encoding(false),
        "utf8-bom" => new UTF8Encoding(true),
        "utf16-le" => Encoding.Unicode,
        "utf16-be" => Encoding.BigEndianUnicode,
        "utf32-le" => Encoding.UTF32,
        "utf32-be" => new UTF32Encoding(true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFileReadRetainsItsReasonAndOtherFilesEvidenceThroughExecutor(bool includeValidFile)
    {
        var bad = Path.Combine(_directory.FullName, "damaged.log");
        await File.WriteAllTextAsync(bad, "damaged\0data");
        var valid = Path.Combine(_directory.FullName, "valid.log");
        await File.WriteAllTextAsync(valid, "SUMMARY result=OK");
        var outcome = await new CopilotToolExecutor(hooks: []).ExecuteAsync(new CopilotToolInvocation
        {
            CallId = "read-invalid-file", RuntimeName = "unicode-read-test", Round = 1,
            Tool = new CopilotReadLocalFileTool(),
            AgentRequest = new()
            {
                Mode = CopilotAgentMode.Auto, UserText = "读取所选日志文件",
                SearchRootPaths = [_directory.FullName],
                ReadableLocalFilePaths = includeValidFile ? [bad, valid] : [bad],
            },
        }, _ => { }, CancellationToken.None);

        Assert.Equal(includeValidFile, outcome.Result.Success);
        Assert.NotEqual(CopilotToolResultContract.InvalidOutputFailureCode, outcome.Result.FailureCode);
        Assert.Contains("NUL", outcome.Result.Content);
        Assert.DoesNotContain(bad, outcome.Result.SuccessfullyReadLocalFilePaths);
        if (includeValidFile)
        {
            Assert.Empty(outcome.Result.ErrorMessage);
            Assert.Equal(valid, Assert.Single(outcome.Result.SuccessfullyReadLocalFilePaths));
            Assert.Contains("SUMMARY result=OK", outcome.Result.Content);
        }
        else
        {
            Assert.Contains("NUL", outcome.Result.ErrorMessage);
            Assert.NotEqual(CopilotToolFailureKind.None, outcome.Result.FailureKind);
            Assert.Empty(outcome.Result.SuccessfullyReadLocalFilePaths);
        }
    }

    public void Dispose()
    {
        var resolved = Path.GetFullPath(_directory.FullName);
        if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(resolved).StartsWith("CopilotUnicodeFileReadTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture directory is outside its temporary root.");
        Directory.Delete(resolved, recursive: true);
    }
}
