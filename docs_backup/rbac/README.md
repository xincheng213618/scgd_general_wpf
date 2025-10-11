# RBAC (Role-Based Access Control) Documentation

基于角色的访问控制系统文档，涵盖用户管理、权限控制和安全策略。

## 目录结构

- [RBAC Model](rbac-model.md) - RBAC 实体模型、关系图和授权流程
- [Permission Names](permission-names.md) - 权限名称枚举和标准格式

## 概述

ColorVision 的 RBAC 系统提供以下核心功能：

- **用户管理**: 用户创建、认证和生命周期管理
- **角色管理**: 角色定义和权限分配
- **权限控制**: 细粒度权限控制和访问拦截
- **审计日志**: 用户行为记录和安全审计

## 核心实体

- **User (用户)**: 系统使用者
- **Role (角色)**: 权限集合的逻辑分组
- **Permission (权限)**: 具体的操作授权
- **AuditLog (审计日志)**: 操作记录和安全追踪

## 相关组件

- `UI/ColorVision.Solution/Rbac/RbacManager.cs` - RBAC 管理器
- `Engine/ColorVision.Engine/Rbac/` - 权限验证组件

## 相关文档

- [安全与权限控制](../security/README.md)
- [用户界面指南](../user-interface-guide/README.md)

---

*最后更新: 2024-09-28*