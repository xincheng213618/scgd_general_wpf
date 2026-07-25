using ColorVision.Copilot;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CopilotMenuReferenceTests
{
    [Fact]
    public void MenuReferenceUsesStableIdAsExecutionSelector()
    {
        var reference = CopilotComposerReferenceCatalog.CreateMenuReference(
            "选项",
            "工具 > 选项",
            "MenuOptions",
            "low-risk-action");

        Assert.Equal(CopilotComposerReferenceKind.Menu, reference.Kind);
        Assert.Equal("MenuOptions", reference.Value);
        Assert.Equal("composer-menu:MenuOptions", reference.SourceId);
        Assert.Contains("Menu path: 工具 > 选项", reference.ContextContent, StringComparison.Ordinal);
        Assert.Contains("Menu selector: MenuOptions", reference.ContextContent, StringComparison.Ordinal);
        Assert.Contains("ExecuteMenu query: MenuOptions", reference.ContextContent, StringComparison.Ordinal);
        Assert.Contains("Do not execute it unless", reference.ContextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void MenuReferenceFallsBackToFullPathWhenIdIsUnavailable()
    {
        var reference = CopilotComposerReferenceCatalog.CreateMenuReference(
            "诊断",
            "工具 > 诊断",
            menuId: null,
            "confirmation-required");

        Assert.Equal("工具 > 诊断", reference.Value);
        Assert.Contains("ExecuteMenu query: 工具 > 诊断", reference.ContextContent, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteMenuUsesOnlyReferencedSelectorWhenQueryIsOmitted()
    {
        var reference = CopilotComposerReferenceCatalog.CreateMenuReference(
            "选项",
            "工具 > 选项",
            "MenuOptions",
            "low-risk-action");
        var request = new CopilotAgentRequest
        {
            UserText = "执行刚才关联的菜单",
            Mode = CopilotAgentMode.Auto,
            ContextItems =
            [
                new CopilotContextItem
                {
                    Id = reference.SourceId,
                    Title = reference.Title,
                    Content = reference.ContextContent,
                },
            ],
        };
        var invoker = new RecordingCapabilityInvoker();
        var result = await new CopilotExecuteMenuTool(invoker).ExecuteAsync(
            request,
            CopilotAgentToolInput.Empty,
            CancellationToken.None);

        Assert.True(CopilotExecuteMenuTool.HasReferencedMenu(request));
        Assert.True(result.Success);
        Assert.Equal("execute_menu", invoker.CapabilityName);
        Assert.Equal("MenuOptions", invoker.Arguments["query"].GetString());
        Assert.False(invoker.Arguments["dry_run"].GetBoolean());
        Assert.Equal(CopilotApplicationCapabilityCaller.InAppAgent, invoker.Caller);
    }

    [Fact]
    public void MultipleMenuReferencesDoNotChooseAnArbitrarySelector()
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            ContextItems =
            [
                MenuContext("composer-menu:first", "FirstMenu"),
                MenuContext("composer-menu:second", "SecondMenu"),
            ],
        };

        Assert.False(CopilotExecuteMenuTool.TryGetReferencedMenuSelector(request, out var selector));
        Assert.Equal(string.Empty, selector);
    }

    private static CopilotContextItem MenuContext(string id, string selector)
    {
        return new CopilotContextItem
        {
            Id = id,
            Content = $"[ColorVision menu reference]{Environment.NewLine}ExecuteMenu query: {selector}",
        };
    }

    private sealed class RecordingCapabilityInvoker : ICopilotApplicationCapabilityInvoker
    {
        public string CapabilityName { get; private set; } = string.Empty;

        public IReadOnlyDictionary<string, JsonElement> Arguments { get; private set; } =
            new Dictionary<string, JsonElement>();

        public CopilotApplicationCapabilityCaller Caller { get; private set; }

        public Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            CapabilityName = capabilityName;
            Arguments = arguments ?? new Dictionary<string, JsonElement>();
            Caller = caller;
            return Task.FromResult(new CopilotApplicationCapabilityCallResult
            {
                Success = true,
                Content = "ok",
            });
        }
    }
}
