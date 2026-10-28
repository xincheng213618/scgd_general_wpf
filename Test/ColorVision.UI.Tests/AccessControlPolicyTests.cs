using ColorVision.Common.ThirdPartyApps;
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System.Reflection;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class AccessControlPolicyTests
{
    [Theory]
    [InlineData(PermissionMode.SuperAdministrator, true)]
    [InlineData(PermissionMode.Administrator, true)]
    [InlineData(PermissionMode.PowerUser, false)]
    [InlineData(PermissionMode.User, false)]
    [InlineData(PermissionMode.Guest, false)]
    public void DefaultPolicyPreservesDefinedRoleHierarchy(PermissionMode role, bool allowed)
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = role };

        AccessDecision decision = AccessControl.Evaluate("test.protected", PermissionMode.Administrator, "device-A");

        Assert.Equal(allowed, decision.Allowed);
        Assert.Equal(allowed ? "allowed" : "permission_denied", decision.ReasonCode);
        Assert.Equal(allowed, AccessControl.Check(PermissionMode.Administrator));
    }

    [Fact]
    public void MissingContextDeniesProtectedActionsWithoutChangingStandaloneDefaults()
    {
        using var scope = new AuthorizationScope();
        Assert.Equal(PermissionMode.Administrator, new Authorization().PermissionMode);
        Authorization.Instance = null!;

        AccessDecision decision = AccessControl.Evaluate("test.protected", PermissionMode.Administrator);
        Assert.False(decision.Allowed);
        Assert.Equal("identity_unavailable", decision.ReasonCode);
        Assert.Null(AccessControl.CurrentSubject.Role);
        Assert.True(AccessControl.Check(() => { })); // Unattributed legacy actions remain public.
        AccessDeniedException error = Assert.Throws<AccessDeniedException>(() =>
            AccessControl.Demand("test.protected", PermissionMode.Administrator, "device-A"));
        Assert.Equal("identity_unavailable", error.ReasonCode);
        Assert.Equal("device-A", error.Request.ResourceId);
    }

    [Theory]
    [InlineData(-999, 0)]
    [InlineData(0, 999)]
    public void UndefinedEnumValuesCannotGainAccessByNumericOrdering(int role, int requirement)
    {
        using var scope = new AuthorizationScope();
        AccessDecision decision = AccessControl.Evaluate("test.protected", (PermissionMode)requirement,
            subject: new AccessSubject(null, (PermissionMode)role));
        Assert.False(decision.Allowed);
        Assert.Equal("invalid_permission", decision.ReasonCode);
    }

    [Fact]
    public void IdentityIsCapturedWithoutPersistingSessionMetadata()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { SubjectId = "42", PermissionMode = PermissionMode.User };
        AccessSubject subject = AccessControl.CurrentSubject;
        Authorization.Instance.SubjectId = "43";
        Authorization.Instance.PermissionMode = PermissionMode.Guest;

        Assert.Equal("42", subject.Id);
        Assert.Equal(PermissionMode.User, subject.Role);
        Assert.DoesNotContain("SubjectId", JsonConvert.SerializeObject(Authorization.Instance));
        Assert.DoesNotContain("SubjectId", System.Text.Json.JsonSerializer.Serialize(Authorization.Instance));
    }

    [Fact]
    public void LegacyActionChecksAndExecutionShareThePolicyAndHierarchicalRequirement()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = PermissionMode.SuperAdministrator };
        ProtectedAction action = new();
        Assert.True(AccessControl.Check(action.Execute));
        AccessControl.ExecuteWithPermissionCheck(action.Execute, PermissionMode.SuperAdministrator);
        Assert.Equal(1, action.Executions);

        RecordingPolicy policy = new(_ => new(true, "host_allowed"));
        AccessControl.Policy = policy;
        Assert.True(AccessControl.Check(action.Execute));
        AccessControl.ExecuteWithPermissionCheck(action.Execute, PermissionMode.User);
        Assert.Equal(2, action.Executions);
        Assert.All(policy.Requests, request =>
        {
            Assert.Equal("test.action.execute", request.ActionId);
            Assert.Equal("action-A", request.ResourceId);
        });
        Assert.Equal(PermissionMode.User, policy.Requests.Last().Subject.Role);
    }

    [Fact]
    public void DemandUsesTheCurrentPolicyWithActionResourceAndSubject()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { SubjectId = "42", PermissionMode = PermissionMode.Administrator };
        RecordingPolicy policy = new(_ => new(false, "resource_denied"));
        AccessControl.Policy = policy;

        AccessDeniedException error = Assert.Throws<AccessDeniedException>(() =>
            AccessControl.Demand("device.configure", PermissionMode.Administrator, "camera-A"));
        AccessRequest request = Assert.Single(policy.Requests);
        Assert.Equal("device.configure", request.ActionId);
        Assert.Equal("camera-A", request.ResourceId);
        Assert.Equal(new AccessSubject("42", PermissionMode.Administrator), request.Subject);
        Assert.Same(request, error.Request);
        Assert.Equal("resource_denied", error.ReasonCode);
    }

    [Fact]
    public void PolicyFailuresAndInvalidActionIdsFailClosed()
    {
        using var scope = new AuthorizationScope();
        AccessControl.Policy = new RecordingPolicy(_ => throw new InvalidOperationException("Unavailable provider"));
        Assert.Equal("policy_unavailable", AccessControl.Evaluate("test.action", PermissionMode.Guest).ReasonCode);
        AccessDeniedException error = Assert.Throws<AccessDeniedException>(() =>
            AccessControl.Demand("test.action", PermissionMode.Guest));
        Assert.Equal("policy_unavailable", error.ReasonCode);
        Assert.Equal("invalid_action", AccessControl.Evaluate(" ", PermissionMode.Guest).ReasonCode);
    }

    [Fact]
    public void MenuCommandRechecksPermissionsWhenExecutedDirectlyAfterRevocation()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
        ProtectedMenu<int> menu = new();
        var command = menu.Command!;
        Assert.True(command.CanExecute(null));

        Authorization.Instance.PermissionMode = PermissionMode.Guest;
        command.Execute(null);
        Assert.Equal(0, menu.Executions);

        Authorization.Instance.PermissionMode = PermissionMode.Administrator;
        command.Execute(null);
        Assert.Equal(1, menu.Executions);
    }

    [Fact]
    public void MenuDisplayAndExecutionUseTheSameDeclaredResourcePolicy()
    {
        using var scope = new AuthorizationScope();
        RecordingPolicy policy = new(request => new(request.ActionId == "test.menu.execute" && request.ResourceId == "menu-A", "resource_policy"));
        AccessControl.Policy = policy;
        ProtectedMenu<int> menu = new();
        var command = menu.Command!;
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, menu.Executions);
        Assert.All(policy.Requests, request => Assert.Equal("menu-A", request.ResourceId));
    }

    [Fact]
    public void ThirdPartyToolsUseStableContextAndCannotBypassAChangedPolicy()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
        bool allowed = true;
        RecordingPolicy policy = new(_ => new(allowed, allowed ? "allowed" : "resource_denied"));
        AccessControl.Policy = policy;
        bool launched = false;
        ThirdPartyAppInfo app = new()
        {
            Name = "Display label", ActionId = "tools.test.open", ResourceId = "tool-A",
            RequiredPermission = PermissionMode.Administrator, LaunchAction = () => launched = true
        };
        var command = app.DoubleClickCommand;
        Assert.True(command.CanExecute(null));
        allowed = false;
        app.Name = "Renamed display label";
        command.Execute(null);
        Assert.False(launched);
        Assert.False(app.IsAuthorizedFor(PermissionMode.SuperAdministrator));
        Assert.All(policy.Requests, request =>
        {
            Assert.Equal("tools.test.open", request.ActionId);
            Assert.Equal("tool-A", request.ResourceId);
        });
    }

    [Fact]
    public void LegacyToolIdentityUsesLaunchPathInsteadOfItsMutableLabel()
    {
        using var scope = new AuthorizationScope();
        RecordingPolicy policy = new(_ => new(true, "allowed"));
        AccessControl.Policy = policy;
        ThirdPartyAppInfo app = new() { Name = "Label", LaunchPath = "tool.exe" };
        Assert.True(app.IsAuthorizedFor(PermissionMode.Guest));
        app.Name = "Changed";
        Assert.True(app.IsAuthorizedFor(PermissionMode.Guest));
        Assert.All(policy.Requests, request =>
        {
            Assert.Equal("third-party.launch", request.ActionId);
            Assert.Equal("tool.exe", request.ResourceId);
        });
    }

    [Fact]
    public void LazyMenuChecksMetadataAndCurrentResourcePolicyWithoutConstructingTheContribution()
    {
        using var scope = new AuthorizationScope();
        LazyProtectedAction.Constructions = LazyProtectedAction.Executions = 0;
        bool allowed = true;
        RecordingPolicy policy = new(request => new(allowed && request.ActionId == "test.lazy.execute"
            && request.ResourceId == "lazy-A", "resource_policy"));
        AccessControl.Policy = policy;
        var command = CreateLazyMenu<LazyProtectedAction>().Command!;
        Assert.True(command.CanExecute(null));
        Assert.Equal(0, LazyProtectedAction.Constructions);
        allowed = false;
        command.Execute(null);
        Assert.Equal(0, LazyProtectedAction.Constructions);
        allowed = true;
        command.Execute(null);
        Assert.Equal(1, LazyProtectedAction.Constructions);
        Assert.Equal(1, LazyProtectedAction.Executions);
    }

    [Fact]
    public void LazyMenuRespectsTheConstructedContributionsCommandCanExecute()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
        LazyCommandMenu<int>.Constructions = LazyCommandMenu<int>.Executions = 0;
        LazyCommandMenu<int>.CanRun = false;
        var command = CreateLazyMenu<LazyCommandMenu<int>>().Command!;
        Assert.True(command.CanExecute(null));
        Assert.Equal(0, LazyCommandMenu<int>.Constructions);
        command.Execute(null);
        Assert.Equal(1, LazyCommandMenu<int>.Constructions);
        Assert.Equal(0, LazyCommandMenu<int>.Executions);
        LazyCommandMenu<int>.CanRun = true;
        command.Execute(null);
        Assert.Equal(2, LazyCommandMenu<int>.Constructions);
        Assert.Equal(1, LazyCommandMenu<int>.Executions);
    }

    [Fact]
    public void LazyReflectionExecutionRechecksAfterConstructionChangesTheIdentity()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
        RevokingConstructorAction.Executions = 0;
        var command = CreateLazyMenu<RevokingConstructorAction>().Command!;
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(0, RevokingConstructorAction.Executions);
    }

    [Fact]
    public void UnattributedLazyMenusRetainTheirPublicLegacyBehavior()
    {
        using var scope = new AuthorizationScope();
        Authorization.Instance = null!;
        PublicLazyAction.Executions = 0;
        var command = CreateLazyMenu<PublicLazyAction>().Command!;
        Assert.True(command.CanExecute(null));
        command.Execute(null);
        Assert.Equal(1, PublicLazyAction.Executions);
    }

    private static IMenuItem CreateLazyMenu<T>() => (IMenuItem)Activator.CreateInstance(
        typeof(MenuManager).GetNestedType("LazyMenuItemAdapter", BindingFlags.NonPublic)!,
        BindingFlags.Instance | BindingFlags.NonPublic, null, [new MenuItemAttribute("Test"), typeof(T)], null)!;

    public sealed class LazyProtectedAction
    {
        public static int Constructions { get; set; }
        public static int Executions { get; set; }
        public LazyProtectedAction() => Constructions++;
        [RequiresPermission(PermissionMode.Administrator, ActionId = "test.lazy.execute", ResourceId = "lazy-A")]
        public void Execute() => Executions++;
    }

    [RequiresPermission(PermissionMode.Administrator, ActionId = "test.lazy.command", ResourceId = "lazy-B")]
    public sealed class LazyCommandMenu<T> : MenuItemBase
    {
        public static int Constructions { get; set; }
        public static int Executions { get; set; }
        public static bool CanRun { get; set; }
        public LazyCommandMenu() => Constructions++;
        public override string Header => "Test";
        public override ICommand? Command => new RelayCommand(_ => Executions++, _ => CanRun);
    }

    public sealed class RevokingConstructorAction
    {
        public static int Executions { get; set; }
        public RevokingConstructorAction() => Authorization.Instance.PermissionMode = PermissionMode.Guest;
        [RequiresPermission(PermissionMode.Administrator)]
        public void Execute() => Executions++;
    }

    public sealed class PublicLazyAction
    {
        public static int Executions { get; set; }
        public void Execute() => Executions++;
    }

    private sealed class ProtectedAction
    {
        public int Executions { get; private set; }
        [RequiresPermission(PermissionMode.Administrator, ActionId = "test.action.execute", ResourceId = "action-A")]
        public void Execute() => Executions++;
    }

    private sealed class ProtectedMenu<T> : MenuItemBase
    {
        public override string Header => "Test";
        public int Executions { get; private set; }
        [RequiresPermission(PermissionMode.Administrator, ActionId = "test.menu.execute", ResourceId = "menu-A")]
        public override void Execute() => Executions++;
    }

    private sealed class RecordingPolicy(Func<AccessRequest, AccessDecision> evaluate) : IAccessPolicy
    {
        public List<AccessRequest> Requests { get; } = [];
        public AccessDecision Evaluate(AccessRequest request)
        {
            Requests.Add(request);
            return evaluate(request);
        }
    }

    private sealed class AuthorizationScope : IDisposable
    {
        private readonly Authorization previous = Authorization.Instance;
        private readonly IAccessPolicy previousPolicy = AccessControl.Policy;
        public void Dispose()
        {
            AccessControl.Policy = previousPolicy;
            Authorization.Instance = previous;
        }
    }
}
