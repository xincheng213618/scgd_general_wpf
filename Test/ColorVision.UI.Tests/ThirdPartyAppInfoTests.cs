using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.MenuItemManager;
using ColorVision.UI.Menus;
using ColorVision.UI.Menus.Base.Edit;
using ColorVision.UI.Menus.Base.File;

namespace ColorVision.UI.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class AuthorizationStateTestGroup
    {
        public const string CollectionName = "Authorization state";
    }

    [Collection(AuthorizationStateTestGroup.CollectionName)]
    public sealed class ThirdPartyAppInfoTests
    {
        [Fact]
        public void DefaultsKeepExistingProvidersExternalAndAvailableToGuests()
        {
            var app = new ThirdPartyAppInfo();

            Assert.Equal(ThirdPartyAppCategory.External, app.Category);
            Assert.Equal(PermissionMode.Guest, app.RequiredPermission);
            Assert.True(app.IsAuthorizedFor(PermissionMode.Guest));
        }

        [Theory]
        [InlineData(PermissionMode.SuperAdministrator, true)]
        [InlineData(PermissionMode.Administrator, true)]
        [InlineData(PermissionMode.PowerUser, false)]
        [InlineData(PermissionMode.User, false)]
        [InlineData(PermissionMode.Guest, false)]
        public void AdministratorToolUsesExistingPermissionHierarchy(PermissionMode currentPermission, bool expected)
        {
            var app = new ThirdPartyAppInfo
            {
                Category = ThirdPartyAppCategory.Internal,
                RequiredPermission = PermissionMode.Administrator,
            };

            Assert.Equal(expected, app.IsAuthorizedFor(currentPermission));
        }

        [Fact]
        public void DirectCommandExecutionCannotBypassRequiredPermission()
        {
            Authorization previousAuthorization = Authorization.Instance;
            try
            {
                var authorization = new Authorization { PermissionMode = PermissionMode.User };
                Authorization.Instance = authorization;
                bool launched = false;
                var app = new ThirdPartyAppInfo
                {
                    RequiredPermission = PermissionMode.Administrator,
                    LaunchAction = () => launched = true,
                };

                app.DoubleClickCommand.Execute(null);
                Assert.False(launched);

                authorization.PermissionMode = PermissionMode.Administrator;
                app.DoubleClickCommand.Execute(null);
                Assert.True(launched);
            }
            finally
            {
                Authorization.Instance = previousAuthorization;
            }
        }

        [Fact]
        public void ServiceManagerEntriesAreAvailableToGuests()
        {
            Authorization previousAuthorization = Authorization.Instance;
            try
            {
                var authorization = new Authorization { PermissionMode = PermissionMode.Guest };
                Authorization.Instance = authorization;
                var menu = new WindowsServicePlugin.ServiceManager.MenuServiceManager();

                Assert.Equal(MenuItemConstants.Help, menu.OwnerGuid);
                Assert.Equal("ServiceManager", menu.GuidId);
                Assert.Equal(0, menu.Order);
                Assert.True(menu.Command!.CanExecute(null));

                var app = Assert.Single(new WindowsServicePlugin.ServiceManager.ServiceManagerAppProvider().GetThirdPartyApps());
                Assert.Equal(PermissionMode.Guest, app.RequiredPermission);
                Assert.True(app.IsAuthorized);
                Assert.True(app.DoubleClickCommand.CanExecute(null));
            }
            finally
            {
                Authorization.Instance = previousAuthorization;
            }
        }

        [Fact]
        public void PrintAndCutHaveDistinctMenuIdentities()
        {
            Assert.NotEqual(new MenuCut().GuidId, new MenuPrint().GuidId);
        }
    }
}
