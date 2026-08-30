# ColorVision.Copilot.Tests

`ColorVision.Copilot.Tests` 是 Copilot、ColorVision 配置、项目指令、Agent、MCP、审批、Hook、Skill、会话恢复和工作区安全边界的独立 xUnit 测试项目。它不与普通 UI 回归共用测试程序集。

测试需在 Windows 上运行，仓库默认使用 x64；独立测试程序集仍引用宿主与共享模块，首次构建前提见[环境与 native 依赖](../../docs/00-getting-started/prerequisites.md)。请从仓库根目录执行：

```powershell
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64
```

新增 Copilot 回归时放入本项目；普通 WPF/UI 基础设施测试继续放在 `ColorVision.UI.Tests`。

跨项目验证选择见[测试与验证](../../docs/02-developer-guide/testing.md)，配置和指令来源见[Copilot 配置契约](../../docs/02-developer-guide/core-concepts/copilot-configuration.md)。

## 测试维护边界

- 以当前产品契约为准。功能被明确移除时，同步删除依赖旧行为的测试；测试失败本身不能成为恢复已删除功能的依据。
- Copilot 不加载全局或项目 `config.toml`，模型、供应商、工具与审批设置由 ColorVision 管理。不要恢复配置加载、TOML 层合并或项目配置信任测试。
- 保留 `AGENTS.md` / `CLAUDE.md` 指令发现，以及实际请求、审批、工具执行、隔离、恢复和诊断脱敏覆盖。测试这些行为时直接构造 profile、request 或 options，不借用已停用的配置加载器搭建前置条件。
- 重复覆盖优先保留真实行为测试；不要用精确源码写法、成员全集或资源路径快照限制合理重构，除非它们本身是明确的兼容契约。
