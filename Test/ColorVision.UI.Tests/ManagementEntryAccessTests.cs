using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.Marketplace;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class ManagementEntryAccessTests
{
    [Fact]
    public void PluginManagementMenuAndDirectEntryUseTheSameRequestBeforeOpeningWindows()
    {
        IAccessPolicy previous = AccessControl.Policy;
        var policy = new DenyPolicy();
        try
        {
            AccessControl.Policy = policy;
            var menu = new MenuPluginManager();
            Assert.False(menu.Command!.CanExecute(null));
            Assert.Throws<AccessDeniedException>(() => menu.Execute());

            Assert.Equal(2, policy.Requests.Count);
            Assert.All(policy.Requests, request =>
            {
                Assert.Equal("plugins.manage", request.ActionId);
                Assert.Equal("application.plugins", request.ResourceId);
                Assert.Equal(PermissionMode.Administrator, request.RequiredPermission);
            });
        }
        finally { AccessControl.Policy = previous; }
    }

    private sealed class DenyPolicy : IAccessPolicy
    {
        public List<AccessRequest> Requests { get; } = [];
        public AccessDecision Evaluate(AccessRequest request)
        {
            Requests.Add(request);
            return new(false, "resource_denied");
        }
    }
}
