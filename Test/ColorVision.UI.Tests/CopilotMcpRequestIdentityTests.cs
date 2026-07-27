using ColorVision.Copilot.Mcp;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpRequestIdentityTests
{
    private const string BearerToken = "test-colorvision-mcp-session-token";
    private const string LoopbackSource = "tcp://127.0.0.1";
    private const string WorkspacePath = @"C:\ColorVision\SessionIsolation";

    [Fact]
    public async Task InitializeIssuesDistinctSessionsForClientsOnTheSameLoopbackAddress()
    {
        var sessionStore = new CopilotMcpClientSessionStore();
        var handler = CreateHandler(sessionStore);

        var firstResponse = await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None);
        var secondResponse = await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None);
        var firstSessionId = GetSessionId(firstResponse);
        var secondSessionId = GetSessionId(secondResponse);

        Assert.Equal(200, firstResponse.StatusCode);
        Assert.Equal(200, secondResponse.StatusCode);
        Assert.Equal(64, firstSessionId.Length);
        Assert.Equal(64, secondSessionId.Length);
        Assert.NotEqual(firstSessionId, secondSessionId);
        Assert.True(sessionStore.TryResolve(firstSessionId, LoopbackSource, out var firstSession));
        Assert.True(sessionStore.TryResolve(secondSessionId, LoopbackSource, out var secondSession));
        Assert.NotEqual(firstSession!.CallerIdentity, secondSession!.CallerIdentity);
        Assert.DoesNotContain(firstSessionId, firstSession.CallerIdentity, StringComparison.Ordinal);
        Assert.DoesNotContain(secondSessionId, secondSession.CallerIdentity, StringComparison.Ordinal);
        Assert.False(firstSession.ExecutionScope.MatchesAuthorizationScope(secondSession.ExecutionScope));
        Assert.DoesNotContain(firstSessionId, firstSession.ExecutionScope.SessionIdentity, StringComparison.Ordinal);
        Assert.DoesNotContain(secondSessionId, secondSession.ExecutionScope.SessionIdentity, StringComparison.Ordinal);

        var repeatedInitialize = await handler.HandleAsync(
            CreateInitializeRequest(firstSessionId),
            CancellationToken.None);
        Assert.Equal(400, repeatedInitialize.StatusCode);
        Assert.False(repeatedInitialize.Headers.ContainsKey(CopilotMcpRequestHandler.SessionHeaderName));
        Assert.Contains("initialize must not include an existing", repeatedInitialize.Body, StringComparison.Ordinal);

        var malformedInitialize = await handler.HandleAsync(
            CreateRequest(
                "POST",
                """{"jsonrpc":"2.0","id":9,"method":"initialize","params":{}}"""),
            CancellationToken.None);
        Assert.Equal(400, malformedInitialize.StatusCode);
        Assert.False(malformedInitialize.Headers.ContainsKey(CopilotMcpRequestHandler.SessionHeaderName));
        Assert.Equal(2, sessionStore.Count);
    }

    [Fact]
    public async Task ToolAndResourceRequestsRequireARegisteredSession()
    {
        var sessionStore = new CopilotMcpClientSessionStore();
        var handler = CreateHandler(sessionStore);
        var sessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
        const string resourceRequest =
            """{"jsonrpc":"2.0","id":2,"method":"resources/read","params":{"uri":"colorvision://workspace/current"}}""";

        var missingSession = await handler.HandleAsync(
            CreateRequest("POST", resourceRequest),
            CancellationToken.None);
        var unknownSession = await handler.HandleAsync(
            CreateRequest("POST", resourceRequest, new string('0', 64)),
            CancellationToken.None);
        var validSession = await handler.HandleAsync(
            CreateRequest("POST", resourceRequest, sessionId),
            CancellationToken.None);

        Assert.Equal(400, missingSession.StatusCode);
        Assert.Contains(CopilotMcpRequestHandler.SessionHeaderName, missingSession.Body, StringComparison.Ordinal);
        Assert.Equal(404, unknownSession.StatusCode);
        Assert.Equal(200, validSession.StatusCode);
        Assert.Contains("ColorVision workspace context", validSession.Body, StringComparison.Ordinal);

        Assert.True(sessionStore.TryResolve(sessionId, LoopbackSource, out var session));
        var resourceAudit = CopilotMcpAuditLogger.GetRecentEntries(200)
            .LastOrDefault(entry => string.Equals(entry.ToolName, "resources/read", StringComparison.Ordinal)
                && string.Equals(entry.CallerSource, session!.CallerIdentity, StringComparison.Ordinal));
        Assert.NotNull(resourceAudit);
        Assert.DoesNotContain(sessionId, resourceAudit!.CallerSource, StringComparison.Ordinal);

        var unsupportedProtocol = CreateRequest(
            "POST",
            resourceRequest,
            sessionId,
            protocolVersion: "2099-01-01");
        var unsupportedProtocolResponse = await handler.HandleAsync(
            unsupportedProtocol,
            CancellationToken.None);
        Assert.Equal(400, unsupportedProtocolResponse.StatusCode);
        Assert.Contains("Unsupported MCP protocol version", unsupportedProtocolResponse.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuditAndPendingActionViewsAreIsolatedPerMcpSession()
    {
        CopilotMcpAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var sessionStore = new CopilotMcpClientSessionStore();
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = WorkspacePath,
                SearchRootPaths = [WorkspacePath],
            },
            SetLanguageHandler = (_, _) => Task.FromResult(CopilotMcpToolCallResult.Ok("executed")),
        });
        var handler = CreateHandler(sessionStore, dispatcher);
        var firstSessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
        var secondSessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));

        try
        {
            await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":10,"method":"tools/call","params":{"name":"get_server_status","arguments":{}}}""",
                    firstSessionId),
                CancellationToken.None);
            await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":11,"method":"tools/call","params":{"name":"get_enabled_tools","arguments":{}}}""",
                    secondSessionId),
                CancellationToken.None);

            Assert.True(sessionStore.TryResolve(firstSessionId, LoopbackSource, out var firstSession));
            Assert.True(sessionStore.TryResolve(secondSessionId, LoopbackSource, out var secondSession));
            var firstAudit = await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":12,"method":"tools/call","params":{"name":"get_audit_log","arguments":{"max_entries":200}}}""",
                    firstSessionId),
                CancellationToken.None);
            var auditText = ReadToolText(firstAudit);
            Assert.Contains(firstSession!.CallerIdentity, auditText, StringComparison.Ordinal);
            Assert.DoesNotContain(secondSession!.CallerIdentity, auditText, StringComparison.Ordinal);

            var createAction = await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":13,"method":"tools/call","params":{"name":"set_language","arguments":{"language":"en-US"}}}""",
                    firstSessionId),
                CancellationToken.None);
            var actionId = ReadField(ReadToolText(createAction), "action_id");

            var secondStatus = await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":14,"method":"tools/call","params":{"name":"get_server_status","arguments":{}}}""",
                    secondSessionId),
                CancellationToken.None);
            var firstStatus = await handler.HandleAsync(
                CreateRequest(
                    "POST",
                    """{"jsonrpc":"2.0","id":15,"method":"tools/call","params":{"name":"get_server_status","arguments":{}}}""",
                    firstSessionId),
                CancellationToken.None);

            Assert.Contains("Pending actions: 0", ReadToolText(secondStatus), StringComparison.Ordinal);
            Assert.Contains("Pending actions: 1", ReadToolText(firstStatus), StringComparison.Ordinal);
            CopilotMcpConfirmationStore.Instance.Cancel(actionId, out _);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ClearForTests();
            CopilotMcpAuditLogger.ClearForTests();
        }
    }

    [Fact]
    public async Task ExternalMcpSessionCannotReadProcessWideAgentTaskJournal()
    {
        var sessionStore = new CopilotMcpClientSessionStore();
        var handler = CreateHandler(sessionStore);
        var sessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));

        var response = await handler.HandleAsync(
            CreateRequest(
                "POST",
                """{"jsonrpc":"2.0","id":16,"method":"resources/read","params":{"uri":"colorvision://copilot/task-events"}}""",
                sessionId),
            CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("not bound to a Copilot conversation", response.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExpiredSessionAndReplacementCannotConsumeItsApprovedAction()
    {
        CopilotMcpConfirmationStore.Instance.ClearForTests();
        var now = new DateTimeOffset(2026, 7, 27, 8, 0, 0, TimeSpan.Zero);
        var executionCount = 0;
        var sessionStore = new CopilotMcpClientSessionStore(() => now);
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = WorkspacePath,
                SearchRootPaths = [WorkspacePath],
            },
            SetLanguageHandler = (_, _) =>
            {
                Interlocked.Increment(ref executionCount);
                return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
            },
        });
        var handler = CreateHandler(sessionStore, dispatcher);
        var originalSessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
        var createAction = await handler.HandleAsync(
            CreateRequest(
                "POST",
                """{"jsonrpc":"2.0","id":17,"method":"tools/call","params":{"name":"set_language","arguments":{"language":"en-US"}}}""",
                originalSessionId),
            CancellationToken.None);
        var approvalText = ReadToolText(createAction);
        var actionId = ReadField(approvalText, "action_id");
        var argumentsDigest = ReadField(approvalText, "arguments_digest");
        var action = Assert.Single(
            CopilotMcpConfirmationStore.Instance.GetPendingActions(),
            item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));

        try
        {
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                actionId,
                new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                out _));
            now = now.Add(CopilotMcpClientSessionStore.IdleLifetime);

            var expiredAttempt = await handler.HandleAsync(
                CreateRequest("POST", CreateConfirmActionRequest(actionId, argumentsDigest), originalSessionId),
                CancellationToken.None);
            Assert.Equal(404, expiredAttempt.StatusCode);

            var replacementSessionId = GetSessionId(
                await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
            var replacementAttempt = await handler.HandleAsync(
                CreateRequest("POST", CreateConfirmActionRequest(actionId, argumentsDigest), replacementSessionId),
                CancellationToken.None);

            Assert.Equal(200, replacementAttempt.StatusCode);
            Assert.True(ReadIsError(replacementAttempt));
            Assert.Contains("different MCP caller/source", ReadToolText(replacementAttempt), StringComparison.Ordinal);
            Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
            Assert.Equal(0, Volatile.Read(ref executionCount));
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ClearForTests();
        }
    }

    [Fact]
    public async Task ApprovedActionCannotBeConfirmedByAnotherSessionOnTheSameLoopbackAddress()
    {
        var executionCount = 0;
        var sessionStore = new CopilotMcpClientSessionStore();
        var dispatcher = new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
        {
            WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
            {
                SolutionDirectoryPath = WorkspacePath,
                SearchRootPaths = [WorkspacePath],
            },
            SetLanguageHandler = (_, _) =>
            {
                executionCount++;
                return Task.FromResult(CopilotMcpToolCallResult.Ok("executed"));
            },
        });
        var handler = CreateHandler(sessionStore, dispatcher);
        var firstSessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
        var secondSessionId = GetSessionId(await handler.HandleAsync(CreateInitializeRequest(), CancellationToken.None));
        var createActionResponse = await handler.HandleAsync(
            CreateRequest(
                "POST",
                """{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"set_language","arguments":{"language":"en-US"}}}""",
                firstSessionId),
            CancellationToken.None);
        var approvalText = ReadToolText(createActionResponse);
        var actionId = ReadField(approvalText, "action_id");
        var argumentsDigest = ReadField(approvalText, "arguments_digest");
        var action = Assert.Single(
            CopilotMcpConfirmationStore.Instance.GetPendingActions(),
            item => string.Equals(item.ActionId, actionId, StringComparison.Ordinal));

        try
        {
            Assert.True(sessionStore.TryResolve(firstSessionId, LoopbackSource, out var firstSession));
            Assert.True(sessionStore.TryResolve(secondSessionId, LoopbackSource, out var secondSession));
            Assert.Equal(firstSession!.CallerIdentity, action.RequestContext.RequestSource);
            Assert.NotEqual(firstSession.CallerIdentity, secondSession!.CallerIdentity);
            Assert.DoesNotContain(firstSessionId, action.RequestContext.RequestSource, StringComparison.Ordinal);
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                action.ActionId,
                new CopilotConfirmationReviewContext(string.Empty, string.Empty, WorkspacePath),
                out _));

            var secondSessionAttempt = await handler.HandleAsync(
                CreateRequest("POST", CreateConfirmActionRequest(actionId, argumentsDigest), secondSessionId),
                CancellationToken.None);

            Assert.Equal(200, secondSessionAttempt.StatusCode);
            Assert.True(ReadIsError(secondSessionAttempt));
            Assert.Contains("different MCP caller/source", ReadToolText(secondSessionAttempt), StringComparison.Ordinal);
            Assert.Equal(ConfirmableActionStatus.Approved, action.Status);
            Assert.Equal(0, executionCount);

            var firstSessionAttempt = await handler.HandleAsync(
                CreateRequest("POST", CreateConfirmActionRequest(actionId, argumentsDigest), firstSessionId),
                CancellationToken.None);

            Assert.Equal(200, firstSessionAttempt.StatusCode);
            Assert.False(ReadIsError(firstSessionAttempt));
            Assert.Equal(ConfirmableActionStatus.Executed, action.Status);
            Assert.Equal(1, executionCount);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.Cancel(action.ActionId, out _);
        }
    }

    [Fact]
    public void SessionExpiresAfterItsIdleLifetimeAndCannotMoveToAnotherNetworkCaller()
    {
        var now = new DateTimeOffset(2026, 7, 27, 8, 0, 0, TimeSpan.Zero);
        var sessionStore = new CopilotMcpClientSessionStore(() => now);
        Assert.True(sessionStore.TryCreate(LoopbackSource, out var session));

        Assert.False(sessionStore.TryResolve(session!.SessionId, "tcp://127.0.0.2", out _));
        Assert.True(sessionStore.TryResolve(session.SessionId, LoopbackSource, out _));

        now = now.Add(CopilotMcpClientSessionStore.IdleLifetime);

        Assert.False(sessionStore.TryResolve(session.SessionId, LoopbackSource, out _));
    }

    [Fact]
    public void SessionCapacityFailsClosedWithoutEvictingActiveSessions()
    {
        var sessionStore = new CopilotMcpClientSessionStore();
        CopilotMcpClientSession? firstSession = null;
        for (var index = 0; index < CopilotMcpClientSessionStore.MaximumSessions; index++)
        {
            Assert.True(sessionStore.TryCreate(LoopbackSource, out var createdSession));
            firstSession ??= createdSession;
        }

        Assert.False(sessionStore.TryCreate(LoopbackSource, out var overflowSession));
        Assert.Null(overflowSession);
        Assert.Equal(CopilotMcpClientSessionStore.MaximumSessions, sessionStore.Count);
        Assert.True(sessionStore.TryResolve(firstSession!.SessionId, LoopbackSource, out _));
    }

    private static CopilotMcpRequestHandler CreateHandler(
        CopilotMcpClientSessionStore sessionStore,
        CopilotMcpToolDispatcher? dispatcher = null)
    {
        return new CopilotMcpRequestHandler(
            () => new CopilotMcpRuntimeSettings
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 38473,
                BearerToken = BearerToken,
            },
            dispatcher ?? new CopilotMcpToolDispatcher(new CopilotMcpToolEnvironment
            {
                WorkspaceSnapshotProvider = () => new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = WorkspacePath,
                    SearchRootPaths = [WorkspacePath],
                },
            }),
            sessionStore);
    }

    private static CopilotMcpHttpRequest CreateInitializeRequest(string? sessionId = null)
    {
        return CreateRequest(
            "POST",
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"session-test","version":"1.0"}}}""",
            sessionId);
    }

    private static CopilotMcpHttpRequest CreateRequest(
        string method,
        string body,
        string? sessionId = null,
        string? protocolVersion = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = "Bearer " + BearerToken,
        };
        if (!string.IsNullOrWhiteSpace(sessionId))
            headers[CopilotMcpRequestHandler.SessionHeaderName] = sessionId;
        if (!string.IsNullOrWhiteSpace(protocolVersion))
            headers[CopilotMcpRequestHandler.ProtocolVersionHeaderName] = protocolVersion;

        return new CopilotMcpHttpRequest
        {
            Method = method,
            Path = "/mcp",
            Headers = headers,
            Body = body,
            CallerSource = LoopbackSource,
        };
    }

    private static string CreateConfirmActionRequest(string actionId, string argumentsDigest)
    {
        return JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 4,
            method = "tools/call",
            @params = new
            {
                name = "confirm_action",
                arguments = new
                {
                    action_id = actionId,
                    tool_name = "set_language",
                    arguments_digest = argumentsDigest,
                },
            },
        });
    }

    private static string GetSessionId(CopilotMcpHttpResponse response)
    {
        Assert.True(response.Headers.TryGetValue(CopilotMcpRequestHandler.SessionHeaderName, out var sessionId));
        return Assert.IsType<string>(sessionId);
    }

    private static string ReadToolText(CopilotMcpHttpResponse response)
    {
        using var document = JsonDocument.Parse(response.Body);
        return document.RootElement
            .GetProperty("result")
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private static bool ReadIsError(CopilotMcpHttpResponse response)
    {
        using var document = JsonDocument.Parse(response.Body);
        return document.RootElement.GetProperty("result").GetProperty("isError").GetBoolean();
    }

    private static string ReadField(string text, string fieldName)
    {
        var prefix = fieldName + ":";
        var line = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .First(item => item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return line[(line.IndexOf(':') + 1)..].Trim();
    }
}
