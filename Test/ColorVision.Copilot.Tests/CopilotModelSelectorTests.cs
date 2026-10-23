using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotModelSelectorTests
{
    [Fact]
    public void BrowsingConnectionsKeepsTheActiveModelUntilAVisibleModelIsChosen()
    {
        Run((panel, viewModel) =>
        {
            Open(panel);
            var providers = (ListBox)panel.FindName("ProviderListBox");
            var models = (ListBox)panel.FindName("ProfileListBox");
            Assert.Equal(3, providers.Items.Count);
            Assert.Equal(new[] { "first", "second" }, models.Items.Cast<CopilotProfileConfig>().Select(profile => profile.Id));
            Invoke(panel, "BrowseProvider", providers.Items[1]);
            Assert.Equal("first", viewModel.SelectedProfile?.Id);
            Assert.Null(models.SelectedItem);
            var next = Assert.IsType<CopilotProfileConfig>(Assert.Single(models.Items.Cast<object>()));
            Assert.Equal("other", next.Id);
            Invoke(panel, "SelectProfile", next);
            Assert.Equal("other", viewModel.SelectedProfile?.Id);
            Assert.Equal("other", viewModel.SelectedConversation?.ProfileId);
        });
    }

    [Fact]
    public void SliderUsesCapabilityOrderAndRefreshesAfterSelectingAnotherModel()
    {
        Run((panel, viewModel) =>
        {
            Open(panel);
            var slider = (Slider)panel.FindName("ReasoningSlider");
            Assert.Equal(3d, slider.Maximum);
            slider.Value = slider.Maximum;
            Assert.Equal(CopilotReasoningMode.Max, viewModel.SelectedProfile?.ReasoningMode);
            Assert.Equal(CopilotReasoningCapabilities.GetLabel(CopilotReasoningMode.Max), ((TextBlock)panel.FindName("ReasoningValueTextBlock")).Text);
            viewModel.SelectedProfile = viewModel.Profiles.Single(profile => profile.Id == "other");
            Open(panel);
            Assert.Equal(5d, slider.Maximum);
            slider.Value = 4;
            Assert.Equal(CopilotReasoningMode.XHigh, viewModel.SelectedProfile?.ReasoningMode);
            Assert.Equal(CopilotReasoningMode.Max, viewModel.Profiles.Single(profile => profile.Id == "first").ReasoningMode);
        });
    }

    private static void Open(CopilotChatPanel panel) => Invoke(panel, "ProfileSelectorPopup_Opened", panel, EventArgs.Empty);

    private static void Invoke(CopilotChatPanel panel, string method, params object[] args) =>
        typeof(CopilotChatPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(panel, args);

    private static void Run(Action<CopilotChatPanel, CopilotChatViewModel> action)
    {
        StaTest.Run(() =>
        {
            var instanceField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            var previous = instanceField.GetValue(null);
            instanceField.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager)));
            var host = new CopilotAgentTaskHost();
            CopilotChatPanel? panel = null;
            try
            {
                var first = Profile("first", "deepseek", "deepseek-v4-pro");
                var second = Profile("second", "deepseek", "deepseek-flash");
                var other = Profile("other", "openai", "gpt-6-astra");
                other.VendorType = CopilotVendorType.OpenAI;
                other.BaseUrl = "https://api.openai.com/v1";
                var legacy = Profile("legacy", "", "deepseek-v4-pro");
                var disabled = Profile("disabled", "deepseek", "disabled");
                disabled.IsEnabled = false;
                var disabledConnection = Profile("disabled-connection", "disabled-connection", "hidden");
                disabledConnection.ConnectionEnabled = false;
                var config = new CopilotConfig
                {
                    SchemaVersion = CopilotConfig.CurrentSchemaVersion, McpBearerToken = "selector-test-token",
                    Profiles = [first, second, other, legacy, disabled, disabledConnection],
                };
                var conversation = CopilotConversationRecord.CreateEmpty(first.Id, first.Name);
                var state = new CopilotChatState { ActiveProfileId = first.Id, ActiveConversationId = conversation.Id, Conversations = [conversation] };
                var service = new CopilotChatService();
                using var viewModel = new CopilotChatViewModel(service, new MemoryStateStore(state), config, new CopilotTurnRuntime(service), host);
                panel = new CopilotChatPanel { DataContext = viewModel };
                action(panel, viewModel);
            }
            finally
            {
                if (panel != null)
                {
                    ((Popup)panel.FindName("ProfileSelectorPopup")).IsOpen = false;
                    panel.DataContext = null;
                }
                host.Shutdown();
                instanceField.SetValue(null, previous);
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
    }

    private static CopilotProfileConfig Profile(string id, string connection, string model) => new()
    {
        Id = id, Name = model, ConnectionId = connection, ConnectionName = connection == "openai" ? "OpenAI" : "DeepSeek",
        VendorType = CopilotVendorType.DeepSeek, ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "selector-test-key", BaseUrl = "https://api.deepseek.com/v1", Model = model,
    };

    private sealed class MemoryStateStore(CopilotChatState state) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => string.Empty;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }
}
