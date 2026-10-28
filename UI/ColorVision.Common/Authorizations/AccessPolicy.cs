using System;

namespace ColorVision.UI.Authorizations;

/// <summary>A captured application identity; a cached ID does not prove session validity.</summary>
public sealed record AccessSubject(string? Id, PermissionMode? Role);

public sealed record AccessRequest(string ActionId, PermissionMode? RequiredPermission, AccessSubject Subject, string? ResourceId = null);

public sealed record AccessDecision(bool Allowed, string ReasonCode);

public interface IAccessPolicy
{
    AccessDecision Evaluate(AccessRequest request);
}

public sealed class AccessDeniedException : UnauthorizedAccessException
{
    public AccessDeniedException(AccessRequest request, AccessDecision decision)
        : base($"Access denied for '{request.ActionId}': {decision.ReasonCode}.")
    {
        Request = request;
        Decision = decision;
    }

    public AccessRequest Request { get; }
    public AccessDecision Decision { get; }
    public string ReasonCode => Decision.ReasonCode;
}

internal sealed class RoleAccessPolicy : IAccessPolicy
{
    public AccessDecision Evaluate(AccessRequest request)
    {
        if (request.RequiredPermission is not { } required)
            return new(true, "allowed");
        if (!Enum.IsDefined(required))
            return new(false, "invalid_permission");
        if (request.Subject.Role is not { } current)
            return new(false, "identity_unavailable");
        if (!Enum.IsDefined(current))
            return new(false, "invalid_permission");
        return current <= required ? new(true, "allowed") : new(false, "permission_denied");
    }
}
