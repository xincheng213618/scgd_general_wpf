using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.MenuItemManager;
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
        public void MenuManagerIsAnAdministratorToolWithItsOwnIcon()
        {
            ThirdPartyAppInfo app = Assert.Single(new MenuItemManagerAppProvider().GetThirdPartyApps());

            Assert.Equal(ThirdPartyAppCategory.Internal, app.Category);
            Assert.Equal(PermissionMode.Administrator, app.RequiredPermission);
            Assert.Equal(ThirdPartyAppIconGlyphs.MenuManager, app.IconGlyph);
            Assert.NotNull(app.LaunchAction);
        }

        [Fact]
        public void LauncherKeepsClassicToolbarGlyphSeparateFromCardFallback()
        {
            Assert.Equal("\uE74C", ThirdPartyAppIconGlyphs.Launcher);
            Assert.NotEqual(ThirdPartyAppIconGlyphs.Default, ThirdPartyAppIconGlyphs.Launcher);
        }

        [Fact]
        public void InternalProvidersExposeDistinctSemanticIcons()
        {
            IThirdPartyAppProvider[] providers =
            [
                new MenuItemManagerAppProvider(),
                new ColorVision.ToolPlugins.ThirdPartyApps.InternalAppProvider(),
                new ColorVision.Database.DatabaseBrowserAppProvider(),
                new ColorVision.ImageEditor.EditorTools.ThreeD.ModelViewer3DAppProvider(),
                new ColorVision.UI.Desktop.TimedButtons.TimedButtonOperationStatsAppProvider(),
                new ColorVision.UI.Desktop.ThirdPartyApps.Treemap.TreemapAppProvider(),
                new WindowsServicePlugin.ServiceManager.ServiceManagerAppProvider(),
            ];

            ThirdPartyAppInfo[] apps = providers.SelectMany(provider => provider.GetThirdPartyApps()).ToArray();

            Assert.Equal(providers.Length, apps.Length);
            Assert.All(apps, app => Assert.Equal(ThirdPartyAppCategory.Internal, app.Category));
            Assert.All(apps, app => Assert.NotEqual(ThirdPartyAppIconGlyphs.Default, app.IconGlyph));
            Assert.Equal(apps.Length, apps.Select(app => app.IconGlyph).Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void PrintAndCutHaveDistinctMenuIdentities()
        {
            Assert.NotEqual(new MenuCut().GuidId, new MenuPrint().GuidId);
        }
    }
}
