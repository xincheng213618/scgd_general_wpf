# ColorVision.Copilot.Tests

`ColorVision.Copilot.Tests` 是 Copilot、Codex 配置、Agent、MCP、审批、Hook、Skill、会话恢复和工作区安全边界的独立 xUnit 测试项目。它不与普通 UI 回归共用测试程序集。

测试需在 Windows 上运行，仓库默认使用 x64。请从仓库根目录执行：

```powershell
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64
```

新增 Copilot 回归时放入本项目；普通 WPF/UI 基础设施测试继续放在 `ColorVision.UI.Tests`。
