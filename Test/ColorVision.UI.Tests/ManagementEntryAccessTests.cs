using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Menus;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class ManagementEntryAccessTests
{
    [Theory]
    [InlineData(typeof(MenuPluginManager), "plugins.manage", "application.plugins")]
    [InlineData(typeof(MenuServiceManager), "maintenance.services.open", "windows-services")]
    public void ManagementMenusAndDirectEntryUseTheSameRequestBeforeOpeningWindows(Type menuType, string actionId, string resourceId)
    {
        IAccessPolicy previous = AccessControl.Policy;
        var policy = new DenyPolicy();
        try
        {
            AccessControl.Policy = policy;
            var menu = Assert.IsAssignableFrom<MenuItemBase>(Activator.CreateInstance(menuType));
            Assert.False(menu.Command!.CanExecute(null));
            Assert.Throws<AccessDeniedException>(() => menu.Execute());

            Assert.Equal(2, policy.Requests.Count);
            Assert.All(policy.Requests, request =>
            {
                Assert.Equal(actionId, request.ActionId);
                Assert.Equal(resourceId, request.ResourceId);
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
