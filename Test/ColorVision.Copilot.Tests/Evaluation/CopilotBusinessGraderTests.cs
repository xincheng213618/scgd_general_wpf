using System.IO;

namespace ColorVision.Copilot.Tests.Evaluation;

public sealed class CopilotBusinessGraderTests : IDisposable
{
    [Fact]
    public void ProtocolEvaluationOverridesLeaveTheSavedProfileUntouched()
    {
        var saved = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.DeepSeek, ProviderType = CopilotProviderType.AnthropicCompatible,
            BaseUrl = "https://api.deepseek.com/anthropic", Model = "existing-model",
        };
        var selected = CopilotBusinessEvaluationTests.ApplyProfileOverrides(saved, "https://api.deepseek.com/responses", "deepseek-flash", "High");
        Assert.NotSame(saved, selected);
        Assert.Equal(CopilotProviderType.AnthropicCompatible, saved.ProviderType);
        Assert.Equal("https://api.deepseek.com/anthropic", saved.BaseUrl);
        Assert.Equal("existing-model", saved.Model);
        Assert.True(CopilotOpenAiRequestPolicy.UsesResponsesApi(selected));
        Assert.Equal("deepseek-flash", selected.Model);
        Assert.Equal(CopilotReasoningMode.High, selected.ReasoningMode);
        Assert.Equal(CopilotReasoningMode.Default, saved.ReasoningMode);
    }

    [Theory]
    [InlineData("https://other.test/responses")]
    [InlineData("http://api.deepseek.com/responses")]
    [InlineData("https://api.deepseek.com:8443/responses")]
    [InlineData("https://api.deepseek.com/chat/completions")]
    [InlineData("https://api.deepseek.com/responses?token=placeholder")]
    [InlineData("https://user@api.deepseek.com/responses")]
    public void ProtocolEvaluationDoesNotRedirectSavedCredentials(string endpoint)
    {
        var saved = new CopilotProfileConfig { BaseUrl = "https://api.deepseek.com/anthropic" };
        Assert.Throws<InvalidOperationException>(() => CopilotBusinessEvaluationTests.ApplyProfileOverrides(saved, endpoint, null, null));
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Unknown")]
    [InlineData("12345")]
    public void ProtocolEvaluationRejectsUnavailableReasoningModes(string mode)
    {
        var saved = new CopilotProfileConfig { VendorType = CopilotVendorType.DeepSeek, BaseUrl = "https://api.deepseek.com/anthropic" };
        Assert.Throws<InvalidOperationException>(() => CopilotBusinessEvaluationTests.ApplyProfileOverrides(saved, null, null, mode));
        Assert.Equal(CopilotReasoningMode.Default, saved.ReasoningMode);
    }

    private readonly string _workspace = Directory.CreateTempSubdirectory("CopilotBusinessGrader-").FullName;

    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    public void SessionReplanRequiresAnActualPriorCheckpointAndRebuild(bool hasCheckpoint, bool resumed, bool replanned, bool expectedPass)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "steering-memory-replan");
        var failures = CopilotBusinessEvaluationTests.GradeSessionContinuation(scenario, hasCheckpoint, resumed, replanned);
        Assert.Equal(expectedPass, failures.Count == 0);
    }

    [Theory]
    [InlineData("absent")]
    [InlineData("plain")]
    [InlineData("escaped")]
    public void AFactRepeatedInTheBaselineAnswerCannotProveCheckpointSteeringMemory(string representation)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "steering-memory-resume-baseline");
        var hashes = WriteSources(scenario);
        var answer = representation switch
        {
            "plain" => "{\"exposure_ms\":18,\"gain\":3,\"station_id\":\"LINE-C7-913\"}",
            "escaped" => "{\"exposure_ms\":18,\"gain\":3,\"station_id\":\"\\u004cINE-C7-913\"}",
            _ => scenario.ExpectedAnswer,
        };
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, answer, ReadEvidence(scenario));
        Assert.Equal(representation != "absent", failures.Contains("deferred_steering_fact_leaked_to_visible_history"));
        if (representation == "absent") Assert.Empty(failures);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void SteeringRequiresAcceptedInputAndAReadStartedAfterTheUpdate(bool accepted, bool readAfterUpdate)
    {
        var steering = CopilotBusinessScenarios.All.Single(s => s.Id == "steered-file-refresh").SteeringAfterRead!;
        var read = new CopilotAgentStepRecord
        {
            Execution = new() { CallId = readAfterUpdate ? "after-update" : "before-update" },
            Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [Path.Combine(_workspace, "camera.json")] },
        };
        var failures = CopilotBusinessEvaluationTests.GradeSteering(steering, _workspace, accepted, ["after-update"], [read]);
        Assert.Equal(!accepted, failures.Contains("steering_not_accepted"));
        Assert.Equal(!readAfterUpdate, failures.Contains("missing_post_steering_read:camera.json"));
        if (accepted && readAfterUpdate) Assert.Empty(failures);
    }

    [Fact]
    public void SelectingAFollowUpIncludesItsActualPriorTurnExactlyOnce()
    {
        var cases = CopilotBusinessEvaluationTests.SelectCases(["follow-up-edit-save", "follow-up-edit-baseline", "follow-up-edit-save"]);
        Assert.Equal(["follow-up-edit-baseline", "follow-up-edit-save"], cases.Select(c => c.Id));
    }

    [Theory]
    [InlineData("ReadLocalFile", false, "line-a", true, true)]
    [InlineData("ReadLocalFile", true, "line-a", true, false)]
    [InlineData("ReadLocalFile", false, "line-b", true, false)]
    [InlineData("ReadLocalFile", false, "line-a", false, false)]
    [InlineData("GrepText", false, "line-a", true, false)]
    public void MissingFileIsCreatedOnlyAfterTheExpectedFailedRead(string tool, bool success, string directory, bool attempted, bool expected)
    {
        var steering = CopilotBusinessScenarios.All.Single(s => s.Id == "steered-missing-file").SteeringAfterRead!;
        var path = Path.Combine(_workspace, directory, "camera.json");
        var result = new CopilotToolResult
        {
            ToolName = tool, Success = success,
            AttemptedLocalFilePaths = attempted ? [path] : [],
            SuccessfullyReadLocalFilePaths = success ? [path] : [],
        };
        Assert.Equal(expected, CopilotBusinessEvaluationTests.ShouldTriggerSteering(steering, _workspace,
            new() { Type = CopilotAgentEventType.ToolResult, ToolResult = result }, null));
        Assert.False(CopilotBusinessEvaluationTests.ShouldTriggerSteering(steering, _workspace,
            new() { Type = CopilotAgentEventType.ToolStarted, ToolResult = result }, null));
    }

    [Theory]
    [InlineData("line-a/camera.json", true)]
    [InlineData("line-b/camera.json", false)]
    [InlineData("../line-a/camera.json", false)]
    [InlineData("line-a/\0camera.json", false)]
    [InlineData(null, false)]
    public void ResolutionFailureUsesTheActualInvocationPath(string? requestedPath, bool expected)
    {
        var steering = CopilotBusinessScenarios.All.Single(s => s.Id == "steered-missing-file").SteeringAfterRead!;
        Assert.Equal(expected, CopilotBusinessEvaluationTests.ShouldTriggerSteering(steering, _workspace,
            new() { Type = CopilotAgentEventType.ToolResult, ToolResult = new() { ToolName = "ReadLocalFile", Success = false } }, requestedPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedReadCannotCountAsEvidenceAfterTheUserRepairsAFile(bool success)
    {
        var steering = CopilotBusinessScenarios.All.Single(s => s.Id == "steered-missing-file").SteeringAfterRead!;
        var path = Path.Combine(_workspace, "line-a", "camera.json");
        var steps = new[] { new CopilotAgentStepRecord
        {
            Execution = new() { CallId = "after-update" },
            Observation = new() { Success = success, AttemptedLocalFilePaths = [path], SuccessfullyReadLocalFilePaths = success ? [path] : [] },
        } };
        var failures = CopilotBusinessEvaluationTests.GradeSteering(steering, _workspace, true, ["after-update"], steps);
        if (success) Assert.Empty(failures);
        else Assert.Equal(["missing_post_steering_read:line-a/camera.json"], failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReadBeforeApplyingThePatchDoesNotProvePostWriteVerification(bool readAfterWrite)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "follow-up-edit-save");
        var path = Path.Combine(_workspace, "camera.json");
        File.WriteAllText(path, "{\"exposure_us\":9000,\"gain\":1}");
        var hashes = WriteSources(scenario);
        File.WriteAllText(path, scenario.ExpectedWrites!["camera.json"]);
        var read = new CopilotAgentStepRecord { Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [path] } };
        var apply = new CopilotAgentStepRecord { ToolCall = new() { ToolName = "ApplyWorkspacePatchEnvelope" }, Observation = new() { Success = true } };
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer,
            readAfterWrite ? [read, apply, read] : [read, apply]);
        Assert.Equal(!readAfterWrite, failures.Contains("missing_post_write_read:camera.json"));
        if (readAfterWrite) Assert.Empty(failures);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("wrong-path")]
    [InlineData("successful")]
    [InlineData("missing")]
    public void NewFileVerificationMustIncludeTheRequestedFailureBeforeCreation(string initialRead)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "create-after-missing-read");
        var hashes = WriteSources(scenario);
        var target = Path.Combine(_workspace, "reports", "summary.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, scenario.ExpectedWrites!["reports/summary.json"]);
        var firstRead = new CopilotAgentStepRecord
        {
            ToolCall = new() { ToolName = "ReadLocalFile", ToolInput = new() { Path = initialRead == "wrong-path" ? "other/summary.json" : "reports/summary.json" } },
            Observation = new() { Success = initialRead == "successful" },
        };
        var steps = ReadEvidence(scenario).ToList();
        if (initialRead is not ("after" or "missing")) steps.Add(firstRead);
        steps.Add(new() { ToolCall = new() { ToolName = "ApplyWorkspacePatchEnvelope" }, Observation = new() { Success = true } });
        if (initialRead == "after") steps.Add(firstRead);
        steps.Add(new() { Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [target] } });
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps);
        if (initialRead == "before") Assert.Empty(failures);
        else Assert.Equal(["missing_initial_failed_read:reports/summary.json"], failures);
    }

    [Fact]
    public void CorrectAnswerWithoutReadEvidenceFails()
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "yield-count");
        var hashes = WriteSources(scenario);
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, []);
        Assert.Contains("missing_read_evidence:results.csv", failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClaimedWriteRequiresBothActualArtifactAndSuccessfulApply(bool writeCorrectArtifact)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "write-result-report");
        var hashes = WriteSources(scenario);
        if (writeCorrectArtifact) File.WriteAllText(Path.Combine(_workspace, "summary.json"), scenario.ExpectedWrites!["summary.json"]);
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, ReadEvidence(scenario));
        Assert.Contains("missing_apply_evidence", failures);
        Assert.Equal(!writeCorrectArtifact, failures.Contains("artifact_mismatch:summary.json"));
    }

    [Fact]
    public void ReadEvidenceAndAnswerDoNotExcuseSourceMutation()
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "yield-count");
        var hashes = WriteSources(scenario);
        var steps = ReadEvidence(scenario);
        Assert.Empty(CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, "```json\n" + scenario.ExpectedAnswer + "\n```\nSource: results.csv", steps));
        File.WriteAllText(Path.Combine(_workspace, "guard.txt"), "CHANGED");
        Assert.Contains("protected_source_changed:guard.txt", CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps));
    }

    [Fact]
    public void AnswerSchemaIgnoresSourceExamplesButRejectsConflictingAnswers()
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "yield-count");
        var hashes = WriteSources(scenario);
        var steps = ReadEvidence(scenario);
        var goodFinal = "Observed: {\"example\":{\"value\":1}}\n```json\n" + scenario.ExpectedAnswer + "\n```";
        Assert.Empty(CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, goodFinal, steps));
        Assert.Empty(CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, goodFinal + "\nSource example: {\"serial\":\"CV001\"}", steps));
        var badFinal = scenario.ExpectedAnswer + "\nFinal: {\"total\":0,\"ng\":0,\"yield_percent\":0}";
        Assert.Contains("answer_facts_mismatch", CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, badFinal, steps));
    }

    [Theory]
    [InlineData("prefix")]
    [InlineData("fence")]
    public void JsonOnlyRequirementRejectsOtherwiseCorrectDecoratedAnswer(string decoration)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "json-only-response");
        var hashes = WriteSources(scenario);
        var steps = ReadEvidence(scenario);
        Assert.Empty(CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps));
        var answer = decoration == "prefix" ? "Done: " + scenario.ExpectedAnswer : "```json\n" + scenario.ExpectedAnswer + "\n```";
        Assert.Contains("answer_not_json_only", CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, answer, steps));
    }

    private Dictionary<string, string> WriteSources(CopilotBusinessScenario scenario)
    {
        foreach (var (name, content) in scenario.Files)
        {
            var path = CopilotBusinessEvaluationTests.ResolveScenarioFilePath(_workspace, name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }
        File.WriteAllText(Path.Combine(_workspace, "guard.txt"), "original");
        return CopilotBusinessEvaluationTests.CaptureFileHashes(_workspace);
    }

    [Theory]
    [InlineData("unchanged")]
    [InlineData("modified")]
    [InlineData("deleted")]
    public void SameNamedNestedFilesHaveIndependentProtectedBaselines(string protectedFileState)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "nested-scoped-edit");
        var hashes = WriteSources(scenario);
        Assert.Equal(3, hashes.Count);
        Assert.NotEqual(hashes["line-a/camera.json"], hashes["line-b/camera.json"]);
        File.WriteAllText(Path.Combine(_workspace, "line-a", "camera.json"), scenario.ExpectedWrites!["line-a/camera.json"]);
        var protectedPath = Path.Combine(_workspace, "line-b", "camera.json");
        if (protectedFileState == "modified") File.WriteAllText(protectedPath, "{}");
        if (protectedFileState == "deleted") File.Delete(protectedPath);
        CopilotAgentStepRecord[] steps = [.. ReadEvidence(scenario),
            new() { ToolCall = new() { ToolName = "ApplyWorkspacePatchEnvelope" }, Observation = new() { Success = true } },
            .. ReadEvidence(scenario)];
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps);
        if (protectedFileState == "unchanged") Assert.Empty(failures);
        else Assert.Equal(["protected_source_changed:line-b/camera.json"], failures);
    }

    [Theory]
    [InlineData("line-a")]
    [InlineData("line-b")]
    public void PostWriteVerificationMustReadTheCorrectDirectory(string readDirectory)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "nested-scoped-edit");
        var hashes = WriteSources(scenario);
        File.WriteAllText(Path.Combine(_workspace, "line-a", "camera.json"), scenario.ExpectedWrites!["line-a/camera.json"]);
        CopilotAgentStepRecord[] steps = [.. ReadEvidence(scenario),
            new() { ToolCall = new() { ToolName = "ApplyWorkspacePatchEnvelope" }, Observation = new() { Success = true } },
            new() { Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [Path.Combine(_workspace, readDirectory, "camera.json")] } }];
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps);
        if (readDirectory == "line-a") Assert.Empty(failures);
        else Assert.Equal(["missing_post_write_read:line-a/camera.json"], failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NestedReportCreationAllowsOnlyTheExpectedArtifact(bool createUnexpectedFile)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "nested-report-create");
        var hashes = WriteSources(scenario);
        var directory = Path.Combine(_workspace, "reports", "current");
        Assert.False(Directory.Exists(directory));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "summary.json");
        File.WriteAllText(path, scenario.ExpectedWrites!["reports/current/summary.json"]);
        if (createUnexpectedFile) File.WriteAllText(Path.Combine(directory, "extra.json"), "{}");
        CopilotAgentStepRecord[] steps = [.. ReadEvidence(scenario),
            new() { ToolCall = new() { ToolName = "ApplyWorkspacePatchEnvelope" }, Observation = new() { Success = true } },
            new() { Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [path] } }];
        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps);
        if (createUnexpectedFile) Assert.Equal(["unexpected_file:reports/current/extra.json"], failures);
        else Assert.Empty(failures);
        Assert.Contains("reports/current/summary.json", CopilotBusinessEvaluationTests.CaptureFileHashes(_workspace).Keys);
    }

    [Theory]
    [InlineData("line-a")]
    [InlineData("line-b")]
    public void NestedSteeringEvidenceRequiresTheUpdatedFile(string readDirectory)
    {
        var steering = CopilotBusinessScenarios.All.Single(s => s.Id == "nested-steered-refresh").SteeringAfterRead!;
        var steps = new[] { new CopilotAgentStepRecord
        {
            Execution = new() { CallId = "after-update" },
            Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = [Path.Combine(_workspace, readDirectory, "camera.json")] },
        } };
        var failures = CopilotBusinessEvaluationTests.GradeSteering(steering, _workspace, true, ["after-update"], steps);
        if (readDirectory == "line-a") Assert.Empty(failures);
        else Assert.Equal(["missing_post_steering_read:line-a/camera.json"], failures);
    }

    [Theory]
    [InlineData("../outside.json")]
    [InlineData("nested/../../outside.json")]
    [InlineData("/outside.json")]
    [InlineData(".")]
    public void ScenarioFilesCannotEscapeTheIsolatedWorkspace(string name)
    {
        Assert.Throws<ArgumentException>(() => CopilotBusinessEvaluationTests.ResolveScenarioFilePath(_workspace, name));
    }

    [Theory]
    [InlineData("ReadLocalFile", true)]
    [InlineData("GrepText", false)]
    [InlineData("GrepText", true)]
    public void SearchUncertaintyAnswerRequiresTheRequestedSuccessfulTool(string toolName, bool success)
    {
        var scenario = CopilotBusinessScenarios.All.Single(s => s.Id == "damaged-search-uncertainty");
        var hashes = WriteSources(scenario);
        CopilotAgentStepRecord[] steps = [new()
        {
            ToolCall = new() { ToolName = toolName },
            Observation = new() { Success = success },
        }];

        var failures = CopilotBusinessEvaluationTests.Grade(scenario, _workspace, hashes, scenario.ExpectedAnswer, steps);
        Assert.Equal(toolName != "GrepText" || !success, failures.Contains("missing_tool_evidence:GrepText"));
    }

    private CopilotAgentStepRecord[] ReadEvidence(CopilotBusinessScenario scenario) =>
        [new() { Observation = new() { Success = true, SuccessfullyReadLocalFilePaths = scenario.Files.Keys.Select(n => Path.Combine(_workspace, n)).ToArray() } }];

    public void Dispose()
    {
        var fullPath = Path.GetFullPath(_workspace);
        Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), fullPath, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("CopilotBusinessGrader-", Path.GetFileName(fullPath));
        Directory.Delete(fullPath, recursive: true);
    }
}
