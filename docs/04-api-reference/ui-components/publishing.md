---
knowledge_id: "ui.publishing"
knowledge_type: "guide"
status: "current"
summary: "UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。"
aliases: ["如何发布UI NuGet包","NuGet发布失败","NuGet版本已占用","UIProjectPackageVersion","algorithms-v","Algorithms单包发布","Release标签","Verify scoped Algorithms release","Publish to NuGet","verify_nuget_package_versions.py"]
code_paths: [".github/workflows/dotnet.yml","UI/Directory.Build.props","UI/ColorVision.Algorithms/ColorVision.Algorithms.csproj","UI/ColorVision.Themes/ColorVision.Themes.csproj","UI/ColorVision.Core/ColorVision.Core.csproj","UI/ColorVision.ImageEditor/ColorVision.ImageEditor.csproj","Engine/ColorVision.Engine/ColorVision.Engine.csproj","Scripts/verify_nuget_package_versions.py"]
test_paths: ["Scripts/tests/test_verify_nuget_package_versions.py","Scripts/tests/test_algorithm_package_contract.py"]
related: ["ui.index","ui.package-boundaries","algorithms.platform","delivery.index","delivery.scripts","engine.host"]
---

# UI NuGet 包构建与发布

本页说明 `.github/workflows/dotnet.yml` 的 NuGet 发布范围、版本预检、包内容及消费验证。安装器、主程序更新包和 `.cvxp` 是不同制品，使用[构建与发布脚本](../../02-developer-guide/scripts/README.md)中各自的入口。

本地构建会写入产物并可能还原依赖；发布 GitHub Release 会触发外部 NuGet 上传，需要明确的发布授权和仓库 `NUGET_API_KEY` secret。普通源码或文档核验不需要触发 Release。

## 选择发布范围

| 触发方式 | 此工作流的行为 |
| --- | --- |
| `master` / `develop` 的 push 或 pull request | 构建、测试与验包，没有 NuGet 上传步骤 |
| 已发布的 GitHub Release，标签不属于 `algorithms-v` 分支 | 预检并逐包上传默认批次：13 个 UI 包，加 `ColorVision.FileIO` 和 `cvColorVision` |
| 已发布的 GitHub Release，标签进入 `algorithms-v` 分支 | 验证精确标签与包身份，仅预检和上传 `ColorVision.Algorithms`；仍运行前面的共同构建与测试步骤 |

默认 UI 批次包括 Common、Themes、UI、Solution、Scheduler、Core、Algorithms、ImageEditor、SocketProtocol、Database、UI.Desktop、ImageTools、Rbac。完整上传路径和顺序以工作流为准；其中 Algorithms 在 ImageEditor 之前上传。各包职责和依赖关系见[UI 包边界](./component-handbook.md)，不按主程序版本推断所有包版本。

### Algorithms 单包标签

标签使用 `algorithms-v<包内版本>`。预检会读取精确路径 `UI/ColorVision.Algorithms/bin/x64/Release/ColorVision.Algorithms.<版本>.nupkg`，要求 nuspec 的 ID 为 `ColorVision.Algorithms`，版本与标签完全相同，然后才输出上传路径。

例如包内版本为 `1.5.8` 时，标签为 `algorithms-v1.5.8`；不能直接把四段 `VersionPrefix=1.5.8.0` 原样写成标签。实现接受三段版本、非零第四段和可选预发布后缀；`algorithms-v1.5.8.0`、首段含多余前导零、包名或包版本不符都会失败。已进入单包分支的无效标签不会回退为默认整批发布。

## 准备版本和本地产物

1. 确定本次发布的精确包集合与提交。默认批次需要所有将上传的包版本可用，单包分支只要求 Algorithms 的发布版本可用。
2. 核对根 props、`UI/Directory.Build.props` 与项目覆写后的最终版本。多数 UI 包继承 UI 公共版本，Themes 有独立版本；FileIO 和 cvColorVision 也各有自己的工程。保留存在 `ColorVision.snk` 时的强名称签名规则。
3. 满足 [Windows/x64 构建前提](../../00-getting-started/prerequisites.md)，生成本次包并检查 nuspec、目标框架资产、依赖和资源。不要把目录中遗留的多个旧版本一起当成本次产物。
4. 运行版本预检并查看每个包的结果，再在已授权范围内发布对应 GitHub Release。预检时点与上传时点不同，仍需检查上传结果。

从仓库根目录本地构建单个包的示例：

```powershell
# 生成本地产物，缺少依赖时会联网 restore；不上传
dotnet build .\UI\ColorVision.Algorithms\ColorVision.Algorithms.csproj -c Release -p:Platform=x64
```

多数 UI 项目启用 `GeneratePackageOnBuild`，可生成 `.nupkg` / `.snupkg`；实际路径和文件由项目最终属性决定。默认 CI 路径为各 UI 项目的 `bin/x64/Release/`，FileIO 使用 `bin/Release/`。项目多目标框架是兼容资产选择，不能要求每个 TFM 字符串等于主程序，见[平台与制品边界](../../02-developer-guide/README.md)。

## 版本占用预检

`Scripts/verify_nuget_package_versions.py` 是只读预检：展开传入的文件或 glob，读取每个 ZIP 中唯一 nuspec 的 ID/版本，并查询 NuGet.org flat-container 版本列表。它不构建、不签名、不上传，也不验证所有 DLL 和 runtime 是否齐全。

```powershell
# 示例只预检 Algorithms 目录匹配的包；会访问 NuGet.org
python Scripts\verify_nuget_package_versions.py "UI/ColorVision.Algorithms/bin/x64/Release/*.nupkg"
```

整批发布由工作流向同一次预检传入全部 15 个路径模式；本地只传一个目录不能证明其余包都可发布。

| 结果或条件 | 含义 |
| --- | --- |
| `available: <ID> <version>`，退出码 `0` | 当前查询未发现占用；没有预留版本，也不是上传成功 |
| `NuGet package version is already occupied`，退出码 `1` | 至少一个包版本已存在；列出占用项，本次上传步骤尚未开始 |
| `NuGet publish preflight failed`，退出码 `2` | 已捕获的文件、nuspec 身份、重复 ID/版本、ZIP 或查询错误；不能把网络失败当成版本可用 |
| 路径模式未命中文件 | 直接失败；不会静默跳过缺少的那组包 |
| 相同 ID/版本来自不同文件 | 按忽略大小写的字符串检查重复并拒绝；不根据文件名认定身份 |
| 远端 `404` | 视为该 ID 尚无版本；其他 HTTP 错误、超时或无效版本列表均不能证明可用 |

预检按包 ID 缓存本次查询，不预留远端版本，也不执行额外的 NuGet 版本规范化。未捕获的解析/文件异常仍可能以非零退出或堆栈结束，不保证所有错误都有同一种文本。输出中的“No packages were published”只说明此预检不发布，不能用于证明之前一次失败运行没有上传任何包。

## 上传和部分失败

默认批次逐条运行 `dotnet nuget push`，不用 `--skip-duplicate`，但没有跨包事务或已上传包的自动撤销。预检之后仍可能因网络、权限、版本竞争或包内容被拒绝而产生部分发布。

**当前整批 `run` 块没有逐条检查原生命令的退出码。** GitHub 的默认 PowerShell 包装在末尾读取最后退出码；在原生命令错误没有转成终止错误的默认条件下，前一次 push 失败而后一次成功可能覆盖最终退出状态。这个风险由当前工作流与 [GitHub shell 退出码规则](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#exit-codes-and-error-action-preference)、[PowerShell 原生命令错误处理](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_preference_variables#psnativecommanduseerroractionpreference)共同推断，不能用工作流整体绿色替代每个包的日志与远端确认。

同一工作流中的多命令 restore、原生验证步骤也未逐条汇总失败；单包预检对内嵌 Python 的退出码有显式检查。修复共同工作流时应单独验证中途失败传播，而不是只检查命令顺序。这里记录的是实现缺口，没有宣称已经修复。

部分发布后，先核对本次每个 ID/版本的实际上传结果及可下载状态，再决定后续版本和恢复范围。不能直接重跑并假设预检会跳过已发布项；已经占用的版本会使预检失败。单包分支也只证明 Algorithms 自身的发布，不会顺带发布消费它的 ImageEditor。

## 包内容与消费验证

| 范围 | 核对内容 |
| --- | --- |
| Algorithms | `lib/net8.0`、`lib/net10.0`、README 和中立契约；不引入 WPF/OpenCvSharp/native/Flow 依赖 |
| Core | `opencv_helper.dll`、CUDA/OpenCV runtime 等工程声明的 native 资产及其依赖；不能仅看托管 DLL |
| ImageEditor | 对 Algorithms/Core 等包的依赖、OpenCvSharp runtime、shader、CIE 数据与图标；helper 由 Core 负责，不要求 ImageEditor 自己重复打包 |
| Common、Themes、UI | 公共签名、主题/控件资源及对应包说明；资源字典可嵌入程序集，不必是输出散文件 |
| Database、SocketProtocol、Scheduler | SqlSugar/SQLite/Quartz 等实际包依赖与数据格式兼容；编译不证明连接或任务可用 |
| UI.Desktop、Solution | 工程声明的 CSS/工具文件，以及 WebView2、编辑器、工作区依赖；资产存在和对应运行链可用分别判断 |
| ImageTools、Rbac | 多图/融合及权限窗口依赖；使用对应模块的验证，不把一次启动当作所有功能通过 |

源码 checkout 中存在 UI 工程时，Engine 优先使用项目引用；因此在同一仓库重新构建主程序，不能证明刚上传的 NuGet 包可消费。`UIProjectPackageVersion` 只控制 Engine 中部分源码缺失后的包回退，默认 `*` 也不是版本锁。仍有无条件项目引用，不能删除整个 `UI/` 来模拟独立包环境，具体规则见 [Engine 条件引用](../engine-components/ColorVision.Engine.md#条件引用不等于独立构建保证)。

消费验证应明确使用哪些精确包版本与包源，检查实际还原资产和最终运行文件；公开 API 改动还需编译实际调用方。窗口、设备、数据库或服务验证分别遵守所属主题的前提，不能为了验文档启动完整产品。

## 自动化覆盖与缺口

| 验证入口 | 已定义的覆盖范围 |
| --- | --- |
| `test_verify_nuget_package_versions.py` | 临时 nuspec 包与模拟版本列表：已占用、新版本、重复身份及未命中 glob；不访问真实 NuGet，不覆盖所有网络异常 |
| `test_algorithm_package_contract.py` 的声明/工作流测试 | 项目元数据、发布分支、顺序、标签与包身份、预检失败不输出路径、restore/build 维度；不执行真实 GitHub 上传，也不验证整批 PowerShell 中途失败传播 |
| 该文件中的隔离包测试 | 在临时目录构建 native helper 和托管包，检查 Algorithms 双 TFM、ImageEditor 的 Algorithms 依赖及 helper 不重复入包；独立消费者在 .NET 8/10 验证 Algorithms 强名称与参数序列化，不代表完整 WPF 消费验证 |

隔离包测试需要 Visual Studio MSBuild、C++ 工作负载、对应 SDK 与签名条件；实际执行会构建和还原依赖。它不会用仓库残留 helper 代替隔离 native 产物。测试文件存在、源码审查和文档构建通过，都不能表述为这些包测试、NuGet 上传或现场运行已完成。
