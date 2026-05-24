# Security and Permission Control

This chapter only describes the permission and session implementations that have already been implemented in the current repository, no longer maintaining a generic security white paper that covers network, data, audit, and authentication across the entire chain for ColorVision.

## Two Layers of Permission Boundaries That Actually Exist

From the code, the current security-related capabilities are mainly divided into two layers:

- Coarse-grained `PermissionMode` under `UI/ColorVision.Common/Authorizations/`
- Local RBAC subsystem under `UI/ColorVision.Solution/Rbac/`

These two layers are not mutually exclusive but coexist.

## Layer 1: Global Coarse-Grained Permissions

`Authorization.Instance.PermissionMode` is still the first boundary for many windows and operations.

The levels it provides include:

- `SuperAdministrator`
- `Administrator`
- `PowerUser`
- `User`
- `Guest`

Many UI entry points currently directly use this layer for judgment, such as "only administrators can open user management and permission management windows."

So if you only look at the RBAC service layer, it is easy to overestimate the extent of fine-grained integration that the current system has already completed.

## Layer 2: Solution-Side Local RBAC

Finer user, role, permission, session, and audit capabilities are currently concentrated in `UI/ColorVision.Solution/Rbac/`.

This subsystem currently has the following characteristics:

- Uses a local SQLite database
- Database defaults to `%AppData%/ColorVision/Config/Rbac.db`
- Initializes table structure via SqlSugar CodeFirst
- Provides login, user management, permission management, session, and audit services

It is more like a "Solution-side local account and permission module," rather than the single master entry for all security capabilities of the entire product.

## What the Current Security Chapter Should Focus On Most

### Login and Session

Currently `AuthService` handles username-password login and automatic login recovery based on SessionToken, while `SessionService` handles creating, validating, revoking, and cleaning up sessions.

This is the most explicit authentication chain in the current code.

### Roles and Permissions

Currently `RbacManager` initializes roles, permissions, users, and role-permission mappings, and performs fine-grained permission code validation through `PermissionChecker`.

### Audit

Currently `AuditLogService` already exists, but it records local audit logs around RBAC-related actions, rather than a global audit platform covering all operations of the entire application.

## Content Not Supported by Current Evidence

The following should no longer be stated as existing capabilities in this chapter:

- Multi-factor authentication
- Global network communication encryption strategies
- Certificate validation systems
- IP whitelisting
- Firewall policies
- Unified audit and interception chain covering all modules

If these capabilities are truly implemented in the future, separate topic pages should be created based on actual code, rather than being pre-written into the architecture overview.

## Recommended Reading Order

Recommended to read along this line:

1. `UI/ColorVision.Common/Authorizations/PermissionMode.cs`
2. `UI/ColorVision.Common/Authorizations/AccessControl.cs`
3. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
4. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
6. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
7. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
8. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## Continue Reading

- [RBAC Module](./rbac.md)
- [Architecture Runtime](../overview/runtime.md)
- [Component Interactions](../overview/component-interactions.md)

## Notes

- This page only retains verifiable permission and session boundaries in the current implementation, no longer maintaining a generalized security capability list.