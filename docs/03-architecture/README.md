# 架构设计

本章节只保留当前系统设计的主阅读路径。历史性方案、拆分草案和一次性讨论文档仍保留在目录中，但不再作为默认入口。

## 主阅读路径

1. [系统架构概览](./overview/system-overview.md)
2. [架构运行时](./overview/runtime.md)
3. [组件交互](./overview/component-interactions.md)
4. [FlowEngineLib 架构](./components/engine/flow-engine.md)
5. [Templates 架构设计](./components/templates/design.md)
6. [安全概览](./security/overview.md)
7. [RBAC 模型](./security/rbac.md)

## 目录说明

- `overview/` 关注系统级视角，例如启动、运行时和组件关系。
- `components/engine/` 关注流程引擎与执行模型。
- `components/templates/` 关注模板系统的设计与现状分析。
- `security/` 关注权限模型和安全边界。

## 建议怎么读

- 第一次接触系统时，按“系统概览 → 运行时 → 组件交互”的顺序阅读。
- 需要修改流程或模板时，再进入 `components/` 下的专题页。
- 需要接口和类型细节时，转到 [模块参考](../04-api-reference/README.md)。

## 补充阅读

- [Templates 模块分析](./components/templates/analysis.md)：适合在已经理解模板设计主线后，再回来看目录演进、注册边界和现状约束。

## 历史资料说明

- 本目录中以 `ColorVision.Engine-Refactoring-` 开头的文档属于历史设计资料，用于追溯思路，不再视为当前默认方案。
- 若历史方案与当前代码实现冲突，以代码和现行模块文档为准。
