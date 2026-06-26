# 安全与权限控制

本章只描述当前仓库里已经落地的权限与会话实现，不再继续维护把 ColorVision 写成一套覆盖网络、数据、审计、认证全链路的通用安全白皮书。

## 当前真正存在的两层权限边界

从代码看，当前安全相关能力主要分成两层：

- `UI/ColorVision.Common/Authorizations/` 下的粗粒度 `PermissionMode`
- `UI/ColorVision.Solution/Rbac/` 下的本地 RBAC 子系统

这两层不是互斥关系，而是并存。

## 第一层：全局粗粒度权限

`Authorization.Instance.PermissionMode` 仍然是当前很多窗口和操作的第一道边界。

它提供的级别包括：

- `SuperAdministrator`
- `Administrator`
- `PowerUser`
- `User`
- `Guest`

很多 UI 入口当前直接用这层做判断，例如“只有管理员可以打开用户管理和权限管理窗口”。

所以如果只看 RBAC 服务层，很容易高估当前系统已经完成的细粒度接入范围。

## 第二层：Solution 侧本地 RBAC

更细的用户、角色、权限、会话和审计能力，当前集中在 `UI/ColorVision.Solution/Rbac/`。

这个子系统当前的特点是：

- 使用本地 SQLite 数据库
- 数据库默认位于 `%AppData%/ColorVision/Config/Rbac.db`
- 通过 SqlSugar CodeFirst 初始化表结构
- 提供登录、用户管理、权限管理、会话和审计服务

它更像“Solution 侧本地账户与权限模块”，而不是整个产品所有安全能力的唯一总入口。

## 当前安全章最该关注什么

### 登录与会话

当前 `AuthService` 负责用户名密码登录和基于 SessionToken 的自动登录恢复，`SessionService` 负责创建、校验、撤销和清理会话。

这部分是当前代码里最明确的认证链。

### 角色与权限

当前 `RbacManager` 会初始化角色、权限、用户和角色-权限映射，并通过 `PermissionChecker` 做细粒度权限码校验。

### 审计

当前 `AuditLogService` 已经存在，但它是围绕 RBAC 相关动作记录本地审计日志，而不是一套覆盖整个应用所有操作的全局审计平台。

## 当前没有证据支撑的内容

以下内容不应继续在本章里作为既有能力陈述：

- 多因素认证
- 全局网络通信加密策略
- 证书验证体系
- IP 白名单
- 防火墙策略
- 覆盖所有模块的统一审计与拦截链

如果将来这些能力真的落地，应基于实际代码另开专题页，而不是提前写进架构概览。

## 推荐阅读顺序

推荐按这条线读：

1. `UI/ColorVision.Common/Authorizations/PermissionMode.cs`
2. `UI/ColorVision.Common/Authorizations/AccessControl.cs`
3. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
4. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
6. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
7. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
8. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 说明

- 本页只保留当前实现里可验证的权限与会话边界，不再继续维护泛化安全能力清单。