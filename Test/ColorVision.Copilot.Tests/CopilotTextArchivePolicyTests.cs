using ColorVision.UI;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTextArchivePolicyTests
{
    [Fact]
    public void SharedStandardMarkersStayRedactedAcrossAppendAndPageBoundaries()
    {
        var markers = new[] { "license", "密码", "SN", "api key", "private.key", "serial number" };
        const string quotedValue = "\"alpha, beta; escaped\\\"omega\"";
        var source = string.Join("\r\n", markers.Select(marker => marker + (marker == "密码" ? "：" : "=") + quotedValue + "; Mode=Run"))
            + "\r\nSnapshotId=visible; IsNew=true; trace=complete";
        var expected = string.Join("\r\n", markers.Select(marker => marker + (marker == "密码" ? "：" : "=") + "\"<redacted>\"; Mode=Run"))
            + "\r\nSnapshotId=visible; IsNew=true; trace=complete";
        var expectedWholeText = string.Join("\r\n", markers.Select(marker => marker + (marker == "密码" ? "：" : "=") + "<redacted>; Mode=Run"))
            + "\r\nSnapshotId=visible; IsNew=true; trace=complete";
        Assert.Equal(expectedWholeText, CopilotSensitiveTextRedactor.Redact(source));

        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in source) archive.Append(character.ToString());
        archive.Complete();

        Assert.Equal(expected, ReadAll(archive));
        Assert.Equal(source.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.False(archive.IsTruncated);
    }

    [Fact]
    public void AClosingQuoteCannotExposeAnAdjacentCredentialRemainder()
    {
        const string source = "password=\"alpha\"omega; Mode=Run";
        const string expected = "password=\"<redacted>\"; Mode=Run";
        Assert.Equal("password=<redacted>; Mode=Run", CopilotSensitiveTextRedactor.Redact(source));
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in source) archive.Append(character.ToString());
        archive.Complete();
        Assert.Equal(expected, ReadAll(archive));
    }

    [Fact]
    public void MaskingAStandaloneKeyCannotHideTheFollowingSensitiveAssignmentMarker()
    {
        const string source = "sk-0123456789ABCDEFGHIJpassword=alpha; Mode=Run";
        const string expected = "<redacted>=<redacted>; Mode=Run";
        Assert.Equal(expected, CopilotSensitiveTextRedactor.Redact(source));
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in source) archive.Append(character.ToString());
        archive.Complete();
        Assert.Equal(expected, ReadAll(archive));
    }

    [Fact]
    public void AnUnclosedLocalizedCredentialCannotExposeItsTailOnLaterPages()
    {
        const string source = "密钥='alpha; beta, omega\r\nprivate tail";
        const string expected = "密钥='<redacted>";
        Assert.Equal("密钥=<redacted>", CopilotSensitiveTextRedactor.Redact(source));
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in source) archive.Append(character.ToString());
        archive.Complete();
        Assert.Equal(expected, ReadAll(archive));
    }

    private static string ReadAll(CopilotTemporaryRedactedOutputArchive archive)
    {
        var result = new StringBuilder();
        var offset = 0;
        while (true)
        {
            var page = archive.Read(offset, maximumCharacters: 7, CancellationToken.None);
            Assert.True(page.Available, page.ErrorMessage);
            Assert.Equal(offset, page.OffsetCharacters);
            Assert.Equal(offset + page.Content.Length, page.NextOffsetCharacters);
            Assert.InRange(page.ReturnedCharacters, 1, 7);
            foreach (var secret in new[] { "alpha", "beta", "omega", "private tail" })
                Assert.DoesNotContain(secret, page.Content, StringComparison.Ordinal);
            result.Append(page.Content);
            if (page.EndOfAvailableOutput) break;
            Assert.True(page.NextOffsetCharacters > offset);
            offset = page.NextOffsetCharacters;
        }
        return result.ToString();
    }
}
