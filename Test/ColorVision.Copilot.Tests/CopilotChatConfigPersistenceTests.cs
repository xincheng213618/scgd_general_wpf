using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatConfigPersistenceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(
        Path.GetTempPath(),
        $"colorvision-copilot-chat-config-{Guid.NewGuid():N}");

    public CopilotChatConfigPersistenceTests()
    {
        Directory.CreateDirectory(_rootDirectory);
    }

    [Fact]
    public void ReasoningModeSaveFailureLeavesLiveConfigAndDiskUnchanged()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var profile = CreateProfile();
        var config = CreateConfig(profile);
        var configHandler = new ConfigHandler
        {
            ConfigFilePath = configFilePath,
            IsAutoSave = false,
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(config, configHandler);

        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            viewModel.SetSelectedProfileReasoningMode(CopilotReasoningMode.High);

        Assert.Same(profile, Assert.Single(config.Profiles));
        Assert.Equal(CopilotReasoningMode.Default, profile.ReasoningMode);
        Assert.Equal(CopilotReasoningMode.Default, viewModel.SelectedProfile?.ReasoningMode);
        Assert.Contains("未更改", viewModel.PendingActionFeedbackText, StringComparison.Ordinal);
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SkillPathOverrideSaveFailureLeavesLiveConfigAndDiskUnchanged()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var config = CreateConfig(CreateProfile());
        var configHandler = new ConfigHandler
        {
            ConfigFilePath = configFilePath,
            IsAutoSave = false,
        };
        var skill = new CopilotAgentSkillCatalogItem("demo-skill", "Demo skill")
        {
            SkillFilePath = Path.Combine(_rootDirectory, "skills", "demo-skill", "SKILL.md"),
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(config, configHandler);

        ConfigSavePublicationStatus status;
        bool changed;
        string errorMessage;
        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            status = viewModel.TrySetAgentSkillPathState(
                skill,
                CopilotAgentSkillOverrideState.Off,
                out changed,
                out errorMessage);
        }

        Assert.Equal(ConfigSavePublicationStatus.NotPersisted, status);
        Assert.True(changed);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        Assert.Empty(config.AgentDefaults.SkillOverrides);
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void SkillMcpDependencySaveFailureDoesNotPublishTheCandidateServers()
    {
        string configFilePath = CreateExistingConfigFile();
        byte[] originalBytes = File.ReadAllBytes(configFilePath);
        var config = CreateConfig(CreateProfile());
        var configHandler = new ConfigHandler
        {
            ConfigFilePath = configFilePath,
            IsAutoSave = false,
        };
        var plan = new CopilotAgentSkillMcpDependencyInstallPlan(
            [new CopilotMcpClientServerConfig
            {
                Name = "docs",
                Endpoint = "https://example.test/mcp",
                AccessPolicy = CopilotMcpClientAccessPolicy.RequireApproval,
            }],
            []);
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(config, configHandler);

        ConfigSavePublicationStatus status;
        IReadOnlyList<CopilotMcpClientServerConfig> addedServers;
        string errorMessage;
        using (new FileStream(configFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            status = viewModel.TryPersistSkillMcpDependencyPlan(
                plan,
                out addedServers,
                out errorMessage);
        }

        Assert.Equal(ConfigSavePublicationStatus.NotPersisted, status);
        Assert.Empty(addedServers);
        Assert.False(string.IsNullOrWhiteSpace(errorMessage));
        Assert.Empty(config.ExternalMcpServers);
        Assert.Equal(originalBytes, File.ReadAllBytes(configFilePath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void ReasoningModeSuccessUsesInjectedHandlerAndRebindsTheSelectedProfile()
    {
        string configFilePath = CreateExistingConfigFile();
        var profile = CreateProfile();
        var config = CreateConfig(profile);
        var configHandler = new ConfigHandler
        {
            ConfigFilePath = configFilePath,
            IsAutoSave = false,
        };
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateViewModel(config, configHandler);

        viewModel.SetSelectedProfileReasoningMode(CopilotReasoningMode.High);

        var publishedProfile = Assert.Single(config.Profiles);
        Assert.NotSame(profile, publishedProfile);
        Assert.Same(publishedProfile, viewModel.SelectedProfile);
        Assert.Equal(CopilotReasoningMode.High, publishedProfile.ReasoningMode);
        var persisted = JObject.Parse(File.ReadAllText(configFilePath));
        Assert.Equal(
            (int)CopilotReasoningMode.High,
            (int)persisted[nameof(CopilotConfig)]![nameof(CopilotConfig.Profiles)]![0]![nameof(CopilotProfileConfig.ReasoningMode)]!);
        AssertNoTemporaryFiles();
    }

    [Fact]
    public void FiveParameterConstructorKeepsConfigChangesInMemory()
    {
        var config = CreateConfig(CreateProfile());
        using var solutionManagerScope = new IsolatedSolutionManagerScope();
        using var viewModel = CreateInMemoryViewModel(config);

        var configHandlerField = typeof(CopilotChatViewModel).GetField(
            "_configHandler",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(configHandlerField);
        Assert.Null(configHandlerField.GetValue(viewModel));

        viewModel.SetSelectedProfileReasoningMode(CopilotReasoningMode.High);

        Assert.Equal(CopilotReasoningMode.High, Assert.Single(config.Profiles).ReasoningMode);
        Assert.Equal(CopilotReasoningMode.High, viewModel.SelectedProfile?.ReasoningMode);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
            Directory.Delete(_rootDirectory, recursive: true);
    }

    private string CreateExistingConfigFile()
    {
        string configFilePath = Path.Combine(_rootDirectory, "ColorVisionConfig.json");
        File.WriteAllText(
            configFilePath,
            new JObject
            {
                ["UnrelatedConfig"] = JObject.FromObject(new { Value = "preserve-me" }),
            }.ToString());
        return configFilePath;
    }

    private static CopilotConfig CreateConfig(CopilotProfileConfig profile) => new()
    {
        SchemaVersion = CopilotConfig.CurrentSchemaVersion,
        McpBearerToken = "chat-config-persistence-test-token",
        Profiles = new ObservableCollection<CopilotProfileConfig> { profile },
    };

    private static CopilotProfileConfig CreateProfile() => new()
    {
        Id = "deepseek-profile",
        Name = "DeepSeek",
        VendorType = CopilotVendorType.DeepSeek,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "chat-config-persistence-test-key",
        BaseUrl = "https://api.deepseek.com/v1",
        Model = "deepseek-chat",
        ReasoningMode = CopilotReasoningMode.Default,
    };

    private static CopilotChatViewModel CreateViewModel(
        CopilotConfig config,
        ConfigHandler configHandler)
    {
        return new CopilotChatViewModel(
            new CopilotChatService(),
            new InMemoryStateStore(CreateState(config)),
            config,
            new NoOpTurnRuntime(),
            new CopilotAgentTaskHost(),
            configHandler);
    }

    private static CopilotChatViewModel CreateInMemoryViewModel(CopilotConfig config) => new(
        new CopilotChatService(),
        new InMemoryStateStore(CreateState(config)),
        config,
        new NoOpTurnRuntime(),
        new CopilotAgentTaskHost());

    private static CopilotChatState CreateState(CopilotConfig config)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(
            config.Profiles[0].Id,
            config.Profiles[0].DisplayLabel);
        conversation.Id = "chat-config-persistence-conversation";
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = config.Profiles[0].Id,
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        return state;
    }

    private void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.GetFiles(_rootDirectory, "*.tmp", SearchOption.TopDirectoryOnly));

    private sealed class InMemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;

        public CopilotChatState Load() => state;

        public void Save(CopilotChatState value)
        {
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());

        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";

        public string Serialize(CopilotChatState value) => "{}";

        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class NoOpTurnRuntime : ICopilotTurnRuntime
    {
        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) =>
            new(CopilotSteeringAdmissionReason.RuntimeUnavailable);

        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;

        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }

    private sealed class IsolatedSolutionManagerScope : IDisposable
    {
        private static readonly FieldInfo InstanceField = typeof(SolutionManager).GetField(
            "_instance",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("SolutionManager singleton field was not found.");

        private readonly object? _previousInstance = InstanceField.GetValue(null);
        private readonly SolutionManager _testInstance =
            (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));

        public IsolatedSolutionManagerScope()
        {
            InstanceField.SetValue(null, _testInstance);
        }

        public void Dispose()
        {
            if (ReferenceEquals(InstanceField.GetValue(null), _testInstance))
                InstanceField.SetValue(null, _previousInstance);
        }
    }
}
