# ColorVision.UI.Tests

`ColorVision.UI.Tests` 是仓库当前主要的 .NET/xUnit 综合测试项目，不是排序专用项目。项目以 `net10.0-windows` 和 WPF 运行，覆盖 UI 基础设施、主程序协作逻辑及相关服务边界；实际范围以 [项目文件](./ColorVision.UI.Tests.csproj) 和测试发现结果为准。

测试需在 Windows 上运行，仓库默认使用 x64。请从仓库根目录执行以下 PowerShell 命令。

## 运行测试

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64
```

只验证当前通用排序回归时运行：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~UniversalSortTests"
```

## 当前排序覆盖

[UniversalSortTests.cs](./UniversalSortTests.cs) 是当前排序测试入口，验证 `SortByProperty` 的逻辑字符串顺序、可空值排序和嵌套属性路径降序。不要在说明中维护会随测试增删而漂移的完整文件清单；仓库级测试选择与其他验证链见 [测试与验证](../../docs/02-developer-guide/testing.md)。
