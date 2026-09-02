# ColorVision.UI.Tests

`ColorVision.UI.Tests` 是普通 UI 与主程序基础设施的 .NET/xUnit 测试项目。项目以 `net10.0-windows` 和 WPF 运行，覆盖 UI 基础设施、日志、Marketplace、PropertyGrid、终端缓冲、STNode、排序和编辑器辅助逻辑；Copilot、Agent 与 MCP 回归位于 `ColorVision.Copilot.Tests`。

测试需在 Windows 上运行，仓库默认使用 x64；项目仍引用宿主与共享模块，首次构建前提见[环境与 native 依赖](../../docs/00-getting-started/prerequisites.md)。请从仓库根目录执行以下 PowerShell 命令。

## 运行测试

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64
```

这是未筛选的本地入口；CI 将普通回归与 `PerformanceProbe` 分进程运行，部分大型探针另需显式启用。筛选与结果解释见[测试与验证](../../docs/02-developer-guide/testing.md)，不能由整体通过推断所有性能测量都执行过。

Copilot 回归：

```powershell
dotnet test .\Test\ColorVision.Copilot.Tests\ColorVision.Copilot.Tests.csproj -p:Platform=x64
```

只验证当前通用排序回归时运行：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~UniversalSortTests"
```

## 当前排序覆盖

[UniversalSortTests.cs](./UniversalSortTests.cs) 是当前排序测试入口，验证 `SortByProperty` 的逻辑字符串顺序、可空值排序和嵌套属性路径降序。不要在说明中维护会随测试增删而漂移的完整文件清单；仓库级测试选择与其他验证链见 [测试与验证](../../docs/02-developer-guide/testing.md)。
