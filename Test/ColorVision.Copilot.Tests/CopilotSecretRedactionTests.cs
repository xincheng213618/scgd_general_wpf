using ColorVision.Copilot.Mcp;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSecretRedactionTests
{
    [Fact]
    public async Task ToolExceptionIsRedactedBeforeTerminalResultIsPublished()
    {
        const string credential = "raw-mcp-secret-1234567890";
        var events = new List<CopilotAgentEvent>();
        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "secret-bearing-tool-failure",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = new SecretBearingFailureTool(credential),
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "exercise exception normalization",
                },
            },
            events.Add,
            CancellationToken.None);

        Assert.False(outcome.Result.Success);
        Assert.Contains("token=<redacted>", outcome.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, outcome.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.True(outcome.Result.ErrorMessage.Length <= CopilotUserFacingErrorFormatter.MaximumMessageLength);

        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.Result.ErrorMessage, terminal.ToolResult?.ErrorMessage);
        Assert.DoesNotContain(credential, terminal.ToolResult?.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReturnedToolFailureDiagnosticsAreRedactedBeforePublication()
    {
        const string credential = "raw-returned-secret-1234567890";
        var events = new List<CopilotAgentEvent>();
        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "secret-bearing-tool-result",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = new SecretBearingResultTool(credential),
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "exercise result normalization",
                },
            },
            events.Add,
            CancellationToken.None);

        Assert.False(outcome.Result.Success);
        Assert.Equal("Remote token=<redacted>, request rejected.", outcome.Result.Summary);
        Assert.Equal("Authorization token=<redacted>; access denied.", outcome.Result.ErrorMessage);
        Assert.Equal("Diagnostic content remains available.", outcome.Result.Content);

        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.Result.Summary, terminal.Text);
        Assert.Equal(outcome.Result.ErrorMessage, terminal.ToolResult?.ErrorMessage);
        Assert.DoesNotContain(credential, terminal.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(credential, terminal.ToolResult?.ErrorMessage ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyToolObservationUsesTheSameDiagnosticRedactionBoundary()
    {
        const string credential = "raw-legacy-secret-1234567890";
        var observation = CopilotToolObservation.FromResult(new CopilotToolResult
        {
            ToolName = "LegacyResultTool",
            Success = false,
            Summary = $"Remote token={credential}, request rejected.",
            ErrorMessage = $"Authorization token={credential}; access denied.",
            FailureKind = CopilotToolFailureKind.Authorization,
        });

        Assert.Equal("Remote token=<redacted>, request rejected.", observation.Summary);
        Assert.Equal("Authorization token=<redacted>; access denied.", observation.ErrorMessage);
    }

    [Theory]
    [InlineData(
        "rg sk-abcdefghijklmnopqrstuvwxyz123456",
        "rg <redacted>")]
    [InlineData(
        "echo AKIAABCDEFGHIJKLMNOP",
        "echo <redacted>")]
    public void RecognizableStandaloneCredentialsAreRedacted(
        string source,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
    }

    [Theory]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9", "<redacted>")]
    [InlineData("sk-0123456789abcdefghi", "sk-0123456789abcdefghi")]
    [InlineData("SK-0123456789abcdefghij", "SK-0123456789abcdefghij")]
    [InlineData("sk-0123456789abcdefghi界", "sk-0123456789abcdefghi界")]
    [InlineData("AKIA0123456789ABCDEF", "<redacted>")]
    [InlineData("AKIA0123456789ABCDE", "AKIA0123456789ABCDE")]
    [InlineData("AKIA0123456789ABCDEFG", "AKIA0123456789ABCDEFG")]
    [InlineData("xAKIA0123456789ABCDEF", "xAKIA0123456789ABCDEF")]
    [InlineData("AKIA0123456789ABCDEF_", "AKIA0123456789ABCDEF_")]
    [InlineData("AKIA0123456789ABCDE界", "AKIA0123456789ABCDE界")]
    [InlineData("界AKIA0123456789ABCDEF", "界AKIA0123456789ABCDEF")]
    public void OutputArchiveRedactsStandaloneCredentialsAcrossAppendAndPageBoundaries(
        string candidate,
        string redactedCandidate)
    {
        var source = $" trace-before[{candidate}]; trace-after\r\n";
        var expected = $" trace-before[{redactedCandidate}]; trace-after\r\n";
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in source)
            archive!.Append(character.ToString());
        archive!.Complete();

        var combined = new StringBuilder();
        var offset = 0;
        while (true)
        {
            var page = archive.Read(offset, maximumCharacters: 7, CancellationToken.None);
            Assert.True(page.Available, page.ErrorMessage);
            Assert.Equal(offset, page.OffsetCharacters);
            Assert.Equal(page.Content.Length, page.ReturnedCharacters);
            Assert.InRange(page.ReturnedCharacters, 1, 7);
            Assert.Equal(offset + page.ReturnedCharacters, page.NextOffsetCharacters);
            Assert.Equal(archive.ArchivedCharacters, page.ArchivedCharacters);
            Assert.Equal(page.NextOffsetCharacters == page.ArchivedCharacters, page.EndOfAvailableOutput);
            combined.Append(page.Content);
            offset = page.NextOffsetCharacters;
            if (page.EndOfAvailableOutput)
                break;
        }

        Assert.Equal(expected, combined.ToString());
        Assert.Equal(source.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.Equal(expected.Length, offset);
        Assert.False(archive.IsTruncated);
    }

    [Theory]
    [InlineData("aws-eof")]
    [InlineData("sk-continuation")]
    [InlineData("rejected-sk-assignment")]
    public void OutputArchiveStandaloneCredentialDetectionPreservesEofAndAssignmentBoundaries(string kind)
    {
        var chunks = kind switch
        {
            "aws-eof" => new[] { "trace-before AKIA0123456789ABCD", "EF" },
            "sk-continuation" => new[] { "trace-before sk-A0b1C2d3E4f5G6h7I8j9", new string('A', 40), "Z9-tail; trace-after" },
            _ => new[] { "trace-before sk-abcpass", "word=secret; trace-after" },
        };
        var expected = kind switch
        {
            "aws-eof" => "trace-before <redacted>",
            "sk-continuation" => "trace-before <redacted>-tail; trace-after",
            _ => "trace-before sk-abcpassword=<redacted>; trace-after",
        };
        var source = string.Concat(chunks);
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        for (var index = 0; index < chunks.Length; index++)
        {
            archive!.Append(chunks[index]);
            if (kind == "sk-continuation" && index == 1)
            {
                var livePage = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);
                Assert.True(livePage.Available, livePage.ErrorMessage);
                Assert.Equal("trace-before <redacted>", livePage.Content);
            }
        }
        archive!.Complete();
        var page = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);

        Assert.True(page.Available, page.ErrorMessage);
        Assert.Equal(expected, page.Content);
        Assert.Equal(source.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.Equal(expected.Length, page.ReturnedCharacters);
        Assert.Equal(expected.Length, page.NextOffsetCharacters);
        Assert.True(page.EndOfAvailableOutput);
        Assert.False(page.ArchiveTruncated);
    }

    [Theory]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9pass", "word=plain-value;tail", "<redacted>=<redacted>;tail")]
    [InlineData("AKIA12345678PASS", "WORD=plain-value;tail", "<redacted>=<redacted>;tail")]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9bea", "rer plain-value;tail", "<redacted> <redacted>;tail")]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9api", "_key=plain-value;tail", "<redacted>_key=<redacted>;tail")]
    [InlineData("AKIA123456789PRIVATE", "-key=plain-value;tail", "<redacted>-key=<redacted>;tail")]
    public void OutputArchiveStandaloneCredentialSuffixPreservesExistingValueRedaction(
        string firstChunk,
        string secondChunk,
        string redactedContent)
    {
        const string prefix = "trace-before ";
        var expected = prefix + redactedContent;
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        archive!.Append(prefix + firstChunk);
        archive.Append(secondChunk);
        archive.Complete();
        var page = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);

        Assert.True(page.Available, page.ErrorMessage);
        Assert.DoesNotContain("plain-value", page.Content, StringComparison.Ordinal);
        Assert.Equal(expected, page.Content);
        Assert.Equal(prefix.Length + firstChunk.Length + secondChunk.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.Equal(expected.Length, page.ReturnedCharacters);
        Assert.Equal(expected.Length, page.NextOffsetCharacters);
        Assert.True(page.EndOfAvailableOutput);
        Assert.False(page.ArchiveTruncated);
    }

    [Theory]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9")]
    [InlineData("AKIA0123456789ABCDEF")]
    public void OutputArchiveExactlyFilledByStandaloneCredentialMaskDoesNotReportTruncation(string credential)
    {
        const string expected = "<redacted>";
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
            "BackgroundOutput", "stdout", maximumCharacters: expected.Length);
        Assert.NotNull(archive);
        foreach (var character in credential)
            archive!.Append(character.ToString());
        archive!.Complete();
        var page = archive.Read(0, expected.Length, CancellationToken.None);

        Assert.True(page.Available, page.ErrorMessage);
        Assert.Equal(expected, page.Content);
        Assert.Equal(credential.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.Equal(0, page.OffsetCharacters);
        Assert.Equal(expected.Length, page.ReturnedCharacters);
        Assert.Equal(expected.Length, page.NextOffsetCharacters);
        Assert.Equal(expected.Length, page.ArchivedCharacters);
        Assert.True(page.EndOfAvailableOutput);
        Assert.False(archive.IsTruncated);
        Assert.False(page.ArchiveTruncated);
    }

    [Theory]
    [InlineData("sk-A0b1C2d3E4f5G6h7I8j9")]
    [InlineData("AKIA0123456789ABCDEF")]
    public void OutputArchivePublishedRedactedPrefixAndSearchStayStableAfterAppend(string credential)
    {
        var initialSource = $"trace-before[{credential}]; {new string('x', 64)}";
        var initialExpected = initialSource.Replace(credential, "<redacted>", StringComparison.Ordinal);
        const string suffix = " trace-after\r\n";
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);
        foreach (var character in initialSource)
            archive!.Append(character.ToString());

        var livePage = archive!.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);
        Assert.True(livePage.Available, livePage.ErrorMessage);
        Assert.DoesNotContain(credential, livePage.Content, StringComparison.Ordinal);
        Assert.StartsWith("trace-before[<redacted>]; ", livePage.Content, StringComparison.Ordinal);
        Assert.InRange(livePage.NextOffsetCharacters, 1, initialExpected.Length);
        Assert.Equal(initialExpected[..livePage.NextOffsetCharacters], livePage.Content);
        Assert.Equal(livePage.Content.Length, livePage.ReturnedCharacters);
        Assert.Equal(livePage.ReturnedCharacters, livePage.NextOffsetCharacters);
        var credentialSearch = archive.Search(credential, 0, CancellationToken.None);
        Assert.True(credentialSearch.Available, credentialSearch.ErrorMessage);
        Assert.False(credentialSearch.Matched);
        var redactedSearch = archive.Search("<redacted>", 0, CancellationToken.None);
        Assert.True(redactedSearch.Available, redactedSearch.ErrorMessage);
        Assert.True(redactedSearch.Matched);

        archive.Append(suffix);
        archive.Complete();
        var expected = initialExpected + suffix;
        var continuation = archive.Read(livePage.NextOffsetCharacters,
            CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);
        Assert.True(continuation.Available, continuation.ErrorMessage);
        Assert.Equal(livePage.NextOffsetCharacters, continuation.OffsetCharacters);
        Assert.Equal(expected[livePage.NextOffsetCharacters..], continuation.Content);
        Assert.Equal(expected, livePage.Content + continuation.Content);
        Assert.Equal(continuation.Content.Length, continuation.ReturnedCharacters);
        Assert.Equal(expected.Length, continuation.NextOffsetCharacters);
        Assert.True(continuation.EndOfAvailableOutput);
        Assert.Equal(initialSource.Length + suffix.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.False(archive.IsTruncated);
    }

    [Theory]
    [InlineData(
        "Bearer abcde+fghijklmnopqrstuvwxyz012345",
        "Bearer <redacted>")]
    [InlineData(
        "bEaReR\tabcdefghijklmnop",
        "Bearer <redacted>")]
    [InlineData(
        "Bearer   AbcdefghijklMN09._~+/-==; echo done",
        "Bearer <redacted>; echo done")]
    [InlineData(
        "Bearer abcdefghijklmnop\")",
        "Bearer <redacted>\")")]
    public void SupportedBearerCredentialsAreFullyRedactedWithoutConsumingDelimiters(
        string source,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
    }

    [Theory]
    [InlineData(
        "Authorization: Bearer shortsecret",
        "Authorization: <redacted>")]
    [InlineData(
        "authorization: Basic dXNlcjpwYXNz",
        "authorization: <redacted>")]
    [InlineData(
        "Authorization=Basic dXNlcjpwYXNz; retry=true",
        "Authorization=<redacted>; retry=true")]
    [InlineData(
        "prefix Authorization=Digest username=\"Mufasa\", realm=\"private\", nonce=\"deadbeef\"; retry=true",
        "prefix Authorization=<redacted>; retry=true")]
    [InlineData(
        "{\"Authorization\":\"Bearer shortsecret\",\"trace\":\"visible\"}",
        "{\"Authorization\":\"<redacted>\",\"trace\":\"visible\"}")]
    [InlineData(
        "{\"Authorization\":\"Digest username=\\\"Mufasa\\\", realm=\\\"testrealm@host.com\\\", nonce=\\\"deadbeef\\\"\",\"trace\":\"visible\"}",
        "{\"Authorization\":\"<redacted>\",\"trace\":\"visible\"}")]
    [InlineData(
        "Authorization: Bearer shortsecret\r\nX-Trace: visible",
        "Authorization: <redacted>\r\nX-Trace: visible")]
    [InlineData(
        "Authorization: Digest username=\"Mufasa\", response=\"secret\"",
        "Authorization: <redacted>")]
    [InlineData(
        "Proxy-Authorization: Basic dXNlcjpwYXNz",
        "Proxy-Authorization: <redacted>")]
    public void AuthorizationHeaderValuesAreFullyRedactedWithoutCrossingBoundaries(
        string source,
        string expected)
    {
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));
    }

    [Theory]
    [InlineData("Authorization: Digest username=\"alpha-private\", response=\"omega-private\"; realm=\"gamma-private\"\r\ntrace-visible")]
    [InlineData("prefix Authorization=Digest username=\"alpha-private\", response=\"omega-private\"; trace-visible")]
    [InlineData(" \tProxy-Authorization: Digest username=\"alpha-private\", response=\"omega-private\"\r\ntrace-visible")]
    [InlineData("Authorization: \"alpha-private\"; opaque-field=\"omega-private\"\r\ntrace-visible; detail-field=visible")]
    [InlineData("prefix Authorization=\"alpha-private;escaped\\\"omega-private\"; trace-visible")]
    public void OutputArchiveRedactsAuthorizationHeadersBeforePublishingCanonicalPages(string source)
    {
        var expected = CopilotMcpAuditLogger.RedactText(source);
        var privateValues = new[] { "alpha-private", "omega-private", "gamma-private" };
        Assert.Contains("trace-visible", expected, StringComparison.Ordinal);
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("BackgroundOutput", "stdout");
        Assert.NotNull(archive);

        foreach (var character in source)
        {
            archive!.Append(character.ToString());
            var livePage = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);
            Assert.True(livePage.Available, livePage.ErrorMessage);
            Assert.True(expected.StartsWith(livePage.Content, StringComparison.Ordinal),
                "Published archive characters must remain a continuous prefix of the canonical redacted output.");
            Assert.Equal(livePage.Content.Length, livePage.ReturnedCharacters);
            Assert.Equal(livePage.ReturnedCharacters, livePage.NextOffsetCharacters);
            foreach (var value in privateValues)
            {
                Assert.DoesNotContain(value, livePage.Content, StringComparison.Ordinal);
                var search = archive.Search(value, 0, CancellationToken.None);
                Assert.True(search.Available, search.ErrorMessage);
                Assert.False(search.Matched);
            }
        }

        archive!.Complete();
        var combined = new StringBuilder();
        var offset = 0;
        while (true)
        {
            var page = archive.Read(offset, maximumCharacters: 7, CancellationToken.None);
            Assert.True(page.Available, page.ErrorMessage);
            Assert.Equal(offset, page.OffsetCharacters);
            Assert.True(page.NextOffsetCharacters > offset);
            Assert.InRange(page.NextOffsetCharacters, offset + 1, expected.Length);
            Assert.Equal(page.Content.Length, page.ReturnedCharacters);
            Assert.Equal(offset + page.ReturnedCharacters, page.NextOffsetCharacters);
            Assert.Equal(expected[offset..page.NextOffsetCharacters], page.Content);
            Assert.Equal(expected.Length, page.ArchivedCharacters);
            foreach (var value in privateValues)
                Assert.DoesNotContain(value, page.Content, StringComparison.Ordinal);
            combined.Append(page.Content);
            offset = page.NextOffsetCharacters;
            if (page.EndOfAvailableOutput)
                break;
        }

        Assert.Equal(expected, combined.ToString());
        Assert.Equal(source.Length, archive.ObservedCharacters);
        Assert.Equal(expected.Length, archive.ArchivedCharacters);
        Assert.False(archive.IsTruncated);
    }

    [Theory]
    [InlineData("comma", false)]
    [InlineData("comma", true)]
    [InlineData("semicolon", false)]
    [InlineData("semicolon", true)]
    [InlineData("escaped-quote", false)]
    [InlineData("escaped-quote", true)]
    [InlineData("single-quote", false)]
    [InlineData("single-quote", true)]
    [InlineData("leading-separator", true)]
    public void OutputArchiveRedactsCompleteQuotedCredentialsAcrossAppendBoundaries(string kind, bool splitAppend)
    {
        var credential = kind switch
        {
            "leading-separator" => ",archive-before;archive-after",
            "semicolon" => "archive-before;archive-after",
            "escaped-quote" => "archive-before\\\"archive-after",
            _ => "archive-before,archive-after",
        };
        var quote = kind == "single-quote" ? "'" : "\"";
        var source = $"{{{quote}Authorization{quote}:{quote}{credential}{quote},{quote}trace{quote}:{quote}visible{quote}}}";
        var expected = source.Replace(credential, "<redacted>", StringComparison.Ordinal);
        Assert.Equal(expected, CopilotMcpAuditLogger.RedactText(source));

        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("ToolOutput", "content");
        Assert.NotNull(archive);
        if (splitAppend)
        {
            var splitAt = kind == "escaped-quote"
                ? source.IndexOf('\\') + 1
                : source.IndexOf(kind == "semicolon" ? ';' : ',');
            archive!.Append(source[..splitAt]);
            archive.Append(source[splitAt..]);
        }
        else
        {
            archive!.Append(source);
        }
        archive.Complete();
        var page = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);

        Assert.True(page.Available, page.ErrorMessage);
        Assert.DoesNotContain("archive-before", page.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("archive-after", page.Content, StringComparison.Ordinal);
        Assert.Equal(expected, page.Content);
        Assert.True(page.EndOfAvailableOutput);
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("'")]
    public void OutputArchivePreservesEmptyQuotedCredentialsAndFollowingText(string quote)
    {
        var source = $"{{{quote}Authorization{quote}:{quote}{quote},{quote}trace{quote}:{quote}visible{quote}}}";
        using var archive = CopilotTemporaryRedactedOutputArchive.TryCreate("ToolOutput", "content");
        Assert.NotNull(archive);
        archive!.Append(source);
        archive.Complete();
        var page = archive.Read(0, CopilotOutputArchiveLimits.DefaultReadCharacters, CancellationToken.None);

        Assert.True(page.Available, page.ErrorMessage);
        Assert.Equal(source, page.Content);
        Assert.True(page.EndOfAvailableOutput);
    }

    [Theory]
    [InlineData("Bearer of good news")]
    [InlineData("Bearer shortsecret")]
    [InlineData("A bearer shortsecret appears in ordinary prose.")]
    [InlineData("Bearer abcdefghijklmno")]
    [InlineData("NotABearer abcdefghijklmnop")]
    [InlineData("Bearerabcdefghijklmnop")]
    [InlineData("Bearer\nabcdefghijklmnop")]
    [InlineData("Bearer\u00a0abcdefghijklmnop")]
    [InlineData("Bearer abcdefghijklmno\u212a")]
    public void BearerLikeTextOutsideCredentialBoundariesIsPreserved(string source)
    {
        Assert.Equal(source, CopilotMcpAuditLogger.RedactText(source));
    }

    [Fact]
    public async Task BackgroundCommandPreviewRedactsCredentialButPreservesRawDigest()
    {
        const string credential = "sk-abcdefghijklmnopqrstuvwxyz123456";
        var command = $"echo {credential}";
        var request = new CopilotAgentRequest
        {
            ConversationId = "secret-redaction-test",
            TaskId = "task",
            Profile = CopilotProfileConfig.CreateDefault(),
            PreferredShell = CopilotShellKind.CommandPrompt,
        };
        var input = new CopilotAgentToolInput
        {
            Arguments = new Dictionary<string, object?>
            {
                ["command"] = command,
                ["shell"] = "cmd",
                ["lifetimeSeconds"] =
                    CopilotBackgroundShellCommandRegistry.MinimumLifetimeSeconds,
            },
        };

        var registry = new CopilotBackgroundShellCommandRegistry();
        try
        {
            var started = await registry.StartAsync(
                request,
                input,
                CancellationToken.None);

            Assert.True(started.Success, started.ErrorMessage);
            var snapshot = Assert.IsType<CopilotBackgroundShellCommandSnapshot>(
                started.Snapshot);
            Assert.Equal("echo <redacted>", snapshot.CommandPreview);
            Assert.DoesNotContain(
                credential,
                snapshot.CommandPreview,
                StringComparison.Ordinal);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command)))
                    .ToLowerInvariant(),
                snapshot.CommandSha256);
        }
        finally
        {
            await registry.ShutdownAsync();
        }
    }

    [Fact]
    public async Task McpToolAuditKeepsFieldNamesAndFailureCodeWithoutPayloadValuesOrWorkspacePath()
    {
        const string rawSessionToken = "raw-mcp-session-token-that-must-not-be-audited";
        const string rawWorkspacePath = @"C:\Customers\SensitiveWorkspace";
        const string argumentValue = "private-locale-value-that-must-not-be-audited";
        var executionScope = CopilotExecutionScope.ForExternalMcpSession(
            rawSessionToken,
            "test-caller",
            rawWorkspacePath);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = rawWorkspacePath,
                SearchRootPaths = [rawWorkspacePath],
            },
        });
        var arguments = new Dictionary<string, JsonElement>
        {
            ["language"] = JsonSerializer.SerializeToElement(argumentValue),
        };

        CopilotMcpAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        try
        {
            var result = await dispatcher.CallExternalAsync(
                "set_language",
                arguments,
                executionScope,
                CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal("confirmation_required", result.ErrorCode);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(200);
            var toolEntry = Assert.Single(entries.Where(entry => entry.ToolName == "set_language"));
            Assert.Equal("fields=language", toolEntry.ArgumentSummary);
            Assert.Equal("confirmation_required", toolEntry.ErrorMessage);
            Assert.Equal(executionScope.WorkspaceIdentity, toolEntry.WorkspaceIdentity);
            Assert.DoesNotContain(rawWorkspacePath, toolEntry.WorkspaceIdentity, StringComparison.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                var retainedText = string.Join('|',
                    entry.ArgumentSummary,
                    entry.ErrorMessage,
                    entry.WorkspaceIdentity,
                    entry.ApprovalDecisionReason);
                Assert.DoesNotContain(argumentValue, retainedText, StringComparison.Ordinal);
                Assert.DoesNotContain(rawWorkspacePath, retainedText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(rawSessionToken, retainedText, StringComparison.Ordinal);
            }
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ClearForTests();
            CopilotMcpAuditLogger.ClearForTests();
        }
    }

    [Fact]
    public void McpActionAuditProjectsArgumentsApprovalReasonAndWorkspace()
    {
        const string sensitiveDetail = "private-review-detail-that-must-not-be-audited";
        const string rawWorkspacePath = @"C:\Customers\SensitiveWorkspace";
        var executionScope = CopilotExecutionScope.ForExternalMcpSession(
            "raw-session-token",
            "test-caller",
            rawWorkspacePath);
        var action = new ConfirmableAction
        {
            ActionId = "action-1",
            ToolName = "set_language",
            ArgumentsSummary = "language=" + sensitiveDetail,
            ApprovalDecisionSource = "automatic-review",
            ApprovalDecisionReason = sensitiveDetail,
            RequestContext = new CopilotConfirmationRequestContext
            {
                Scope = executionScope,
                SourceKind = CopilotApprovalSourceKind.ExternalMcp,
                RequestSource = "test-caller",
                WorkspacePath = rawWorkspacePath,
            },
        };

        CopilotMcpAuditLogger.ClearForTests();
        try
        {
            CopilotMcpAuditLogger.ActionApproved(action);

            var entry = Assert.Single(CopilotMcpAuditLogger.GetRecentEntries(1));
            Assert.Equal("details-withheld", entry.ArgumentSummary);
            Assert.Equal("details-withheld", entry.ApprovalDecisionReason);
            Assert.Equal(executionScope.WorkspaceIdentity, entry.WorkspaceIdentity);
            Assert.DoesNotContain(sensitiveDetail, entry.ArgumentSummary, StringComparison.Ordinal);
            Assert.DoesNotContain(sensitiveDetail, entry.ApprovalDecisionReason, StringComparison.Ordinal);
            Assert.DoesNotContain(rawWorkspacePath, entry.WorkspaceIdentity, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CopilotMcpAuditLogger.ClearForTests();
        }
    }

    private sealed class SecretBearingFailureTool(string credential) : ICopilotTool
    {
        public string Name => "SecretBearingFailureTool";

        public string Description => "Throws a test exception containing a credential.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromException<CopilotToolResult>(new InvalidOperationException(
                $"Remote tool failed with token={credential}. {new string('x', 1_000)}"));
        }
    }

    private sealed class SecretBearingResultTool(string credential) : ICopilotTool
    {
        public string Name => "SecretBearingResultTool";

        public string Description => "Returns a test failure containing a credential.";

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = false,
                Summary = $"Remote token={credential}, request rejected.",
                Content = "Diagnostic content remains available.",
                ErrorMessage = $"Authorization token={credential}; access denied.",
                FailureKind = CopilotToolFailureKind.Authorization,
            });
        }
    }
}
