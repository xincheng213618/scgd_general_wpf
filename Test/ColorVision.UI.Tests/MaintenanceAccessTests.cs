using ColorVision.Recovery;
using ColorVision.Settings.Maintenance;
using ColorVision.UI.Authorizations;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class MaintenanceAccessTests
{
    [Theory]
    [InlineData(MaintenanceActions.OpenSetup)]
    [InlineData(MaintenanceActions.OpenRecovery)]
    public void MaintenancePreservesTheRequestedActionAndApplicationResource(string actionId)
    {
        IAccessPolicy previous = AccessControl.Policy;
        AccessRequest? captured = null;
        try
        {
            AccessControl.Policy = new TestPolicy(request =>
            {
                captured = request;
                return new(request.ActionId == MaintenanceActions.OpenSetup, "maintenance_policy");
            });

            Assert.Equal(actionId == MaintenanceActions.OpenSetup, MaintenanceAccess.Evaluate(actionId).Allowed);
            Assert.NotNull(captured);
            Assert.Equal(actionId, captured.ActionId);
            Assert.Equal(PermissionMode.Administrator, captured.RequiredPermission);
            Assert.Equal("application", captured.ResourceId);
        }
        finally { AccessControl.Policy = previous; }
    }

    [Fact]
    public void PermissionRevokedDuringConfirmationPreventsTheMaintenanceRestart()
    {
        IAccessPolicy previous = AccessControl.Policy;
        bool allowed = true;
        bool restarted = false;
        try
        {
            AccessControl.Policy = new TestPolicy(_ => new(allowed, allowed ? "allowed" : "permission_denied"));
            var exception = Assert.Throws<AccessDeniedException>(() => StartupMaintenanceController.RunRequest(
                () => MaintenanceAccess.Demand(MaintenanceActions.OpenRecovery),
                () => { allowed = false; return true; },
                () => restarted = true));

            Assert.False(restarted);
            Assert.Equal(MaintenanceActions.OpenRecovery, exception.Request.ActionId);
            Assert.Equal("permission_denied", exception.ReasonCode);
        }
        finally { AccessControl.Policy = previous; }
    }

    private sealed class TestPolicy(Func<AccessRequest, AccessDecision> evaluate) : IAccessPolicy
    {
        public AccessDecision Evaluate(AccessRequest request) => evaluate(request);
    }
}
