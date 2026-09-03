---
knowledge_id: "platform.developer-tools"
knowledge_type: "topic"
status: "current"
summary: "开发工具管理的Python/Node检测、当前应用与新终端命令路径、官方版本选择和安装校验；下载等待30分钟，关窗停止后续安装但不取消下载或终止安装器。"
aliases: ["开发工具管理", "系统开发工具", "Python安装", "Node.js安装", "npm版本", "命令路径详情", "当前应用", "新终端参考", "随附包管理器", "刷新检测", "获取可安装版本", "下载并安装", "npmmirror", "NVM_HOME", "FNM_DIR", "VOLTA_HOME", "DeveloperToolsAppProvider", "DeveloperToolsWindow", "DeveloperToolPageModel", "DeveloperToolDiscoveryService", "DeveloperToolCatalogService", "DeveloperToolInstallerService", "DeveloperToolRelease", "ResolvePathCommand", "PrepareInstaller", "VerifiedInstaller", "官网SHA256", "下载服务返回了意外的文件路径", "安装向导已退出"]
code_paths: ["ColorVision/ToolPlugins/DeveloperTools", "Engine/ColorVision.Engine/Services/DeveloperTools", "UI/ColorVision.Common/ThirdPartyApps/ThirdPartyAppManager.cs", "UI/ColorVision.UI/Download/IDownloadService.cs", "UI/ColorVision.UI/Environments.cs", "UI/ColorVision.UI.Desktop/Download/Aria2cDownloadService.cs", "UI/ColorVision.UI.Desktop/Download/Aria2cDownloadManager.cs", "UI/ColorVision.UI.Desktop/Download/DownloadWindow.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DeveloperToolSafetyTests.cs", "Test/ColorVision.UI.Tests/DeveloperToolsWindowTests.cs"]
related: ["ui.common", "ui.desktop", "ui.storage-maintenance"]
---

# 开发工具管理：检测与安装 Python、Node.js

“开发工具管理”用于查看 Windows 环境中的 Python、Node.js 与随附包管理器，并由用户选择版本、校验安装包后启动官方交互式向导。安装的软件由系统和其他应用共享；本窗口不托管项目环境，不选择项目解释器，也不安装项目依赖。

## 查看安装与命令路径

1. 在“第三方应用”的“常用工具”中打开“开发工具管理”。同一应用复用已打开的窗口；再次打开会还原最小化窗口并激活。
2. 选择 **Python** 或 **Node.js / npm** 页，查看“系统中发现的安装”：版本、随附包管理器、解释器路径和发现方式。路径省略时可悬停查看完整值。
3. 展开“命令路径详情”，对比“当前应用”和“新终端参考”。发现多处安装不代表其中最高版本就是默认命令。
4. 安装或修改环境后点击“刷新检测”。窗口加载、激活时也会检测；可见且未最小化时每 15 秒检测一次，关闭后停止。检测不运行解释器、launcher、shell profile 或 npm 命令，也不写 PATH。

| 显示项 | 来源与含义 |
| --- | --- |
| 安装列表中的版本 | exe 的 ProductVersion，缺失时取 FileVersion；这是文件元数据，不是实际运行版本测试 |
| Python“随附包管理器” | 同目录 `Lib/site-packages` 下第一个 `pip-*.dist-info` 的目录名；不执行 pip，也不在多个残留目录中选择最高版本 |
| Node“随附包管理器” | 同目录 `node_modules/npm/package.json` 的 version；文件大于 1 MiB 或不存在时显示未检测到随附 npm |
| 当前应用 | 当前 ColorVision 进程 PATH 中第一个存在的 `python.exe` / `node.exe` |
| 新终端参考 | 系统登记 PATH 后接用户登记 PATH，以相同规则找第一个文件；不会修改当前进程环境 |
| npm 命令路径 | 系统登记 PATH 后接用户登记 PATH 中第一个 `npm.cmd`，显示在详情说明中；不保证属于上方某一行的 Node 安装 |

命令定位只处理完全限定路径，展开环境变量、去掉外层引号并保留 PATH 顺序；忽略 `.` 和相对目录，不模拟 shell alias/profile、当前目录搜索、PATHEXT 或版本管理器的选择逻辑。因此“新终端参考”不是对所有终端实际命令的保证。新开终端或重启应用后再核对；重复刷新不会把登记 PATH 写回当前进程。

### 检测范围与未发现的解释器

候选依次来自进程 PATH、系统/用户 PATH，再加以下位置；相同完整路径忽略大小写去重，保留首次发现方式：

- Python：HKCU/HKLM 的 32/64-bit `SOFTWARE\Python` 注册信息（PEP 514 的 InstallPath 布局）；每个 company/tag 的 InstallPath 优先用 ExecutablePath，否则拼接 python.exe。还检查 `%LocalAppData%\Programs\Python` 的直接子目录。
- Node：Program Files / Program Files (x86) 下的 nodejs，以及 HKCU/HKLM 的 32/64-bit `SOFTWARE\Node.js` InstallPath。

注册表每个根最多读取 32 个 Python company、每个 company 最多 64 个 tag；常见 Python 根最多 64 个子目录。最终最多检查前 128 个去重候选，不枚举全盘、所有便携目录或虚拟环境，也不验证候选的 CPU 架构。**安装目录存在、列表非空或版本可读，都不证明解释器和第三方库运行正常。**

命令路径可以显示 WindowsApps 应用执行别名，但该类路径从安装列表中排除，并提示“别名存在不代表解释器已安装”。权限、文件或 npm JSON 读取失败时，整个候选可能被跳过；“未检测到解释器”不等于确定没有安装。

Node 页只通过当前进程的 `NVM_HOME`、`FNM_DIR`、`VOLTA_HOME` 是否非空提示版本管理器，不完整枚举 nvm/fnm/Volta。窗口不会切换这些管理器的版本；已由管理器维护的环境应继续按其方式管理。

## 选择可安装版本

初次打开没有版本选择，不能开始安装。用户点击“获取可安装版本”才请求官网目录；自动检测不会获取在线版本。

| 页签 | 目录与当前过滤规则 |
| --- | --- |
| Python | `https://www.python.org/downloads/windows/`；提取官网 `python-3.x.y-amd64.exe` 链接，目录与文件版本必须一致，minor ≥ 12；按版本降序、每个 minor 取最新一项，最多三个系列 |
| Node.js / npm | `https://nodejs.org/dist/index.json`；版本须为三段、major ≥ 22，lts 字段为字符串，files 包含 win-x64-msi；每个 major 取最新一项，最多两个系列，显示目录所报 npm 版本 |

这些是代码中的选择规则，不是固定的最新版本清单或上游生命周期承诺。窗口只提供 Windows x64 的 EXE/MSI，不提供预发布版本、解释器 ZIP、独立 npm 升级或自定义安装参数。两种目录请求均限时 30 秒、响应缓冲最多 4 MiB；Python 链接解析还受 1 秒正则超时限制。

重新获取成功时，保留仍在列表中的原选择，否则选择第一项；空目录清空选择。请求或解析失败时保留旧列表/选择并显示错误，不会自动改用镜像目录。选择“国内镜像 · npmmirror”只改变安装包来源，**版本目录和预期 SHA256 始终来自官网**，不能用它绕过官网不可达的问题。

## 下载、校验并启动向导

此操作会联网、写安装包缓存、启动外部进程，并可能改变系统软件和 PATH；应在允许安装或升级软件的环境中执行。官方向导负责安装位置、PATH、用户/系统范围及必要 UAC。Node MSI 可能升级或替换现有安装，不能把这个窗口当成独立项目环境的版本隔离工具。

1. 选好版本与“安装包下载源”，点击“下载并安装…”。默认是国内镜像 `https://cdn.npmmirror.com/binaries/`，也可选择官方网站。
2. 窗口先从官网 HTTPS 获取文件摘要：Python 读取同名 `.sigstore` 的 `messageSignature.messageDigest`，要求 SHA2_256 和 32 字节 Base64 digest；Node 精确匹配 `SHASUMS256.txt` 的文件名及 64 位十六进制摘要。任何一步失败都不会开始安装。
3. 通过 `IDownloadService` 打开下载管理器并提交任务。每次使用 `Environments.DirToolPackageCache\DeveloperTools\<GUID>` 独立目录；默认位于应用数据目录的 `PackageCache\Tools` 下，不使用下载管理器的普通默认目录。请求的 authorization 为 null，不向公共源附带后台凭据。
4. 收到成功回调后，要求规范化后的完整路径与本次目录、所选文件名一致；后台校验 SHA256、Windows Authenticode 信任及发布者。
5. 校验通过且窗口尚未关闭时，在 UI 线程启动官方向导。Python 直接通过 shell 启动 EXE；Node 通过系统目录的 msiexec.exe `/i <文件>` 启动。没有静默安装开关、强制提权 verb 或自定义路径参数。
6. 完成向导后返回窗口，读取退出提示并重新核对安装列表和命令路径。

获取版本、下载和等待安装器期间，两页共用一个操作门禁，页签及安装控件禁用；顶部“刷新检测”和“关闭”仍可用。安装包来源不会修改 pip index/npm registry，不安装 cnpm；窗口也不自动卸载旧版本或安装项目库。

### 信任校验的具体边界

`PrepareInstaller` 以 FileShare.Read 打开文件，禁止写入和替换，检查文件名、SHA256，再调用 WinVerifyTrust 校验整条链及吊销状态。签名必须可信，证书 SimpleName 还须精确等于 Python Software Foundation 或 OpenJS Foundation；校验失败会释放句柄且不启动。

Python 的 `.sigstore` 在这里仅提供通过官网 HTTPS 取得的摘要，代码不是完整 Sigstore 验证器；Node 也不是使用镜像自报的摘要。Windows 返回非零信任结果、发布者不符或摘要不符都会阻止启动，没有降低校验等级的自动回退。排查网络、系统时间、证书状态或下载内容后再试。

正常等待路径会保持文件句柄到所启动进程退出。**关闭窗口会取消等待并释放该句柄，但不终止已启动的安装器**；因此不能保证关窗后仍持有文件锁，也不能把所监视进程的退出理解为所有安装子进程或系统更改已完成。

### 暂停、超时和关闭窗口

下载进度、暂停、恢复及取消由下载管理器处理。开发工具窗口只等待回调，最多 30 分钟：

| 情况 | 后续行为 |
| --- | --- |
| 下载完成且窗口仍在等待 | 进入路径、摘要和签名校验，通过后才启动 |
| 下载失败回调 | 显示“安装未完成”，可以检查下载任务或切换来源重试 |
| 在下载管理器暂停、取消或删除任务 | 当前管理器路径不立即完成安装回调；开发工具页可能继续处于忙碌状态，直到任务完成、30 分钟超时或关闭窗口 |
| 30 分钟等待超时 | 结束本次自动安装等待；不取消下载管理器任务，迟到的完成回调不会重新启动本次安装 |
| 下载或校验期间关闭开发工具窗口 | 取消后续启动；已提交下载仍由下载管理器管理，后台校验不提供中途强制终止 |
| 安装器已经启动后关闭 | 停止等待和刷新，安装器继续由用户操作，不做卸载或回滚 |

重新打开窗口不会恢复上一次安装等待；如需重试，由用户重新选择并启动。当前带专用回调的下载任务跳过下载管理器通用“下载完成自动运行文件”逻辑，不能据此省略本窗口的校验。安装包和下载记录不会因本窗口关闭或安装器退出而自动删除；删除下载记录与删除文件是下载管理器中分别处理的选择。其它清理范围见[存储清理](../../04-api-reference/ui-components/storage-maintenance.md)。

## 判断安装结果与排障

窗口等待的是 `Process.Start` 返回的进程。退出码为 3010 时提示自行保存工作并重启 Windows；其它退出码连同是否发现所选版本一起显示，**没有以退出码 0 自动认定安装成功**。

“已检测到所选版本”仅比较文件元数据版本与所选 `Version.ToString()` 是否完全相等，不运行解释器，也不确认默认 PATH。元数据含额外后缀时可能匹配不到；原来已存在该版本时也可能显示已检测到。安装完成后的刷新若与已有检测重叠，会跳过本次刷新并立即用既有列表生成提示，已有检测可能稍后才更新列表；该提示并非原子安装验收。

| 现象 / 提示 | 检查 |
| --- | --- |
| 已安装但“当前应用”没变 | 当前进程仍继承旧 PATH；对照“新终端参考”，新开终端或重启应用后核对 |
| npm 路径和列表版本不一致 | 列表读取各 Node 目录的随附 npm，命令路径来自登记 PATH；两者可能指向不同安装 |
| 没有可安装版本 / 获取版本失败 | 官网连接、30 秒请求时限、响应格式及筛选条件；切换安装包镜像不会改变目录来源 |
| “下载服务不可用” | 检查 Desktop 下载服务实现及宿主装载；尚未提交安装任务 |
| “下载服务返回了意外的文件路径” | 回调必须指向本次 GUID 目录内的精确文件；下载器自动重命名的文件也会被拒绝 |
| SHA256 不一致 / 发布者不符 / Windows 信任错误 | 检查所选版本、文件内容、官方摘要及证书状态；不要绕过校验直接沿用失败文件 |
| 安装页一直忙碌 | 检查下载是否暂停/取消、等待是否超时，或安装向导是否仍打开；刷新检测不会取消安装等待 |
| 向导退出但未检测到版本 | 查看退出码、官方向导结果、发现范围和版本字符串；退出不等于安装/环境更新完成 |

## 实现与验证入口

`DeveloperToolsAppProvider` 以 Guest 所需级别向第三方应用链贡献入口；授权与 provider 装载遵循[共享工具契约](../../04-api-reference/ui-components/ColorVision.Common.md)。主程序 ToolPlugins 负责窗口与等待生命周期，Engine Services 负责发现、版本目录和安装包校验，Desktop 下载器负责文件任务；这些能力没有后台环境管理服务。

`DeveloperToolSafetyTests` 使用合成目录/元数据检查版本筛选、精确文件名、缺失或不支持的摘要、篡改/未签名文件拒绝、失败后句柄释放以及 PATH 首项与不执行文件。`DeveloperToolsWindowTests` 检查明暗主题下两页初始无版本选择、不可安装；它不证明已加载窗口的在线检测、自动刷新或真实安装流程。

现有测试未覆盖完整成功签名/发布者链、官网格式变化、暂停/超时/关窗与回调竞争、安装器子进程及真实 UAC、系统 PATH、版本管理器共存。产品验收需要允许改变环境的测试机；文档和检索校验不执行这些操作。
