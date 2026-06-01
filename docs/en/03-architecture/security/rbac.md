# RBAC Module

This page describes the RBAC subsystem already implemented in the current repository, no longer maintaining the draft document that described it as a complete enterprise security platform.

## Module Location

The current RBAC implementation is concentrated in `UI/ColorVision.Solution/Rbac/`, not `Engine/`.

This is important because it shows that the current permission system is closer to a desktop Solution-side local user and permission module, rather than a unified engine-layer security kernel.

## What Happens During Initialization

`RbacManager` is the master entry point for this subsystem. The current initialization chain is roughly:

1. Create the `%AppData%/ColorVision/Config/` directory.
2. Open or create the local SQLite database `Rbac.db`.
3. Initialize entity tables using SqlSugar CodeFirst.
4. Initialize `AuthService`, `UserService`, `RoleService`, `PermissionService`, `AuditLogService`, `SessionService`, `PermissionChecker`, `TenantService`.
5. Create the default administrator role and admin user.
6. Write preset permissions, and assign all permissions to the `admin` role.
7. If there is a cached login, sync the current user's `PermissionMode` back to the global authorization state.

This chain shows that the current RBAC startup is local and self-bootstrapping, not dependent on an external authentication server.

## Core Entities That Actually Exist

From the `Entity/` directory, the most important table models currently include:

- `UserEntity` maps to `sys_user`
- `UserDetailEntity` maps to `sys_user_detail`
- `RoleEntity` maps to `sys_role`
- `PermissionEntity` maps to `sys_permission`
- `RolePermissionEntity` maps to `sys_role_permission`
- `SessionEntity` maps to `sys_session`
- `AuditLogEntity` maps to `sys_audit_log`

Additionally, there are `TenantEntity` and `UserTenantEntity`, indicating that this implementation has already reserved the tenant dimension, but this is not the primary reading focus of the current desktop permission entry pages.

## What These Entities Currently Handle

### Users and User Details

`UserEntity` stores username, password hash, enabled status, and soft-delete status.

`UserDetailEntity` additionally stores:

- `PermissionMode`
- Email, phone, address, company, department, position
- User avatar and notes

Important note: the current global coarse-grained permission level is not derived in real-time from the role table but is directly stored in `UserDetailEntity.PermissionMode` and synced to `Authorization.Instance.PermissionMode` after login.

### Roles and Permissions

`RoleEntity` manages basic role information, `PermissionEntity` manages permission codes, `RolePermissionEntity` manages role-to-permission associations.

The current permission service already has a set of preset permission codes, for example:

- `user.create`
- `user.edit`
- `role.assign_permissions`
- `permission.manage`
- `audit.view`

This shows that current fine-grained permissions have already moved beyond simple "admin/guest" tiers and are starting to implement action-code-based control.

### Sessions

`SessionEntity` stores:

- `SessionToken`
- User ID
- Device info and IP
- Creation time, expiration time, last active time
- Whether revoked

`SessionService` handles generating 64-byte random tokens, validating sessions, updating active time, revoking sessions, and cleaning up expired sessions.

### Audit

`AuditLogEntity` currently records:

- User ID / Username
- Action code
- Detail description
- Time
- IP

`AuditLogService` currently provides local audit logging oriented toward the RBAC side, not a unified audit bus covering all business modules.

## How the Current Login Chain Works

The current authentication chain is roughly:

1. User enters username and password in `LoginWindow`.
2. `AuthService.LoginAndGetDetailAsync(...)` queries enabled and non-soft-deleted users.
3. Password is verified via `PasswordHasher`; old-format passwords are upgraded during login.
4. System ensures `UserDetailEntity` exists and loads role list.
5. Login result is written to `LoginResultDto`.
6. `SessionService` can further create and maintain SessionToken.
7. After successful login, `UserDetailEntity.PermissionMode` is synced to `Authorization.Instance.PermissionMode`.

Therefore, the current login result affects both the internal state of the RBAC subsystem and directly affects the global UI coarse-grained permission judgment.

## How Current Permission Checking Works

Current permission checking has two layers:

### Coarse-Grained Entry Judgment

Many windows first directly check:

- `Authorization.Instance.PermissionMode > PermissionMode.Administrator`

For example, both user management and permission management windows first block at this step.

### Fine-Grained Permission Code Judgment

Finer permission validation is handled by `PermissionChecker`. It will:

- Query role IDs associated with the user
- Join tables to find corresponding permission codes
- Cache results using a cache with expiration time and LRU eviction

So the current system is not "only RBAC" or "only PermissionMode," but rather both coarse and fine layers coexisting.

## Currently Visible Management Interfaces

From the module directory, RBAC currently has a set of clear desktop windows:

- `LoginWindow`
- `RegisterWindow`
- `ChangePasswordWindow`
- `UserManagerWindow`
- `PermissionManagerWindow`
- `RbacManagerWindow`

Among them:

- `UserManagerWindow` handles management actions like user list, role viewing, enable/disable, delete, and password reset
- `PermissionManagerWindow` handles assigning permissions by role and invalidating permission cache after saving

## Most Important Boundaries to Note in the Current Design

### It Is a Local Permission System, Not a Unified Remote Identity Platform

The current implementation depends on local SQLite and local windows, not an external authentication center.

### Coarse-Grained `PermissionMode` Has Not Been Fully Replaced

Many key entry points still first check `PermissionMode` before entering RBAC management logic.

### Fine-Grained Permission Integration Is Partial

The currently confirmable fine-grained permission capabilities are mainly concentrated in RBAC's own management windows and service layers, and the entire product cannot yet be described as having fully integrated permission code control.

## What This Page No Longer Does

This page no longer maintains these contents inconsistent with the current implementation:

- Fictional generic `AuthService`/`AuditService` platform layering diagrams
- Assumptions that all business modules are uniformly intercepted by RBAC
- Multi-factor authentication, network certificates, IP whitelisting, and other unimplemented security capabilities

If expansion to full-system security architecture is needed later, separate topic pages should be written based on real integration points.

## Recommended Reading Order

1. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
2. `UI/ColorVision.Solution/Rbac/Entity/`
3. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
4. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
6. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
7. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## Continue Reading

- [Security and Permission Control](./overview.md)
- [Architecture Runtime](../overview/runtime.md)
- [Component Interactions](../overview/component-interactions.md)