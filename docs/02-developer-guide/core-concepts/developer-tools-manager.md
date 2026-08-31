---
knowledge_id: "platform.developer-tools"
knowledge_type: "topic"
status: "current"
summary: "独立开发工具窗口发现系统 Python、Node.js/npm，并由用户选择校验后启动官方安装向导；不托管项目环境，不自动改默认版本。"
aliases: ["开发工具管理", "Python安装", "Node.js安装", "npm版本", "npmmirror", "DeveloperToolsWindow", "DeveloperToolDiscoveryService", "DeveloperToolInstallerService"]
code_paths: ["ColorVision/ToolPlugins/DeveloperTools", "Engine/ColorVision.Engine/Services/DeveloperTools", "UI/ColorVision.UI/Download/IDownloadService.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DeveloperToolSafetyTests.cs", "Test/ColorVision.UI.Tests/DeveloperToolsWindowTests.cs"]
related: ["ui.common", "ui.desktop", "delivery.artifact-delivery"]
---

# 系统开发工具管理

第三方工具入口的“常用工具 → 开发工具管理”打开一个独立窗口，包含 Python 和 Node.js / npm 两页。安装后的软件属于 Windows 用户/系统环境，可由其他软件使用；这不是 Copilot 私有运行时，不加载 Codex 配置，也不替项目选择解释器。

## 检测与默认版本

打开、激活窗口以及窗口可见且未最小化时每 15 秒刷新。窗口关闭停止刷新，不创建后台服务。检测读取进程 PATH、系统/用户注册表 PATH、Python PEP 514 和 Node.js 注册表安装路径以及常见安装目录；读取可执行文件版本、Python site-packages 中的 pip 元数据目录以及 Node 随附 npm 的 package.json，不执行解释器、脚本、shell profile 或 npm 命令。

“应用当前命令路径”和“系统登记命令路径”分别解释当前进程继承的环境与注册表中的环境。后者是新终端的参考，不保证已打开的终端、shell alias/profile 或版本管理器选择相同路径。WindowsApps 的应用执行别名单独提示，不将其作为已安装解释器。发现范围有界，不枚举全盘，也不声称列出全部虚拟环境或便携安装。

安装元数据不能证明该解释器和第三方库可以正常运行。损坏/不可读取的候选可能跳过；检测到 nvm、fnm、Volta 环境变量时提示优先由原管理器维护，窗口不切换这些管理器的版本。

## 可选安装与可信下载

用户点击“获取可安装版本”才联网：Python 使用官方 Windows 下载页，每个系列保留最新稳定 x64 EXE，最多三个系列（3.12 起）；Node 使用官方 dist/index.json，仅保留含 Windows x64 MSI 的 LTS，每个主版本最新一项，最多两个系列（22 起）。不提供预发布版、解释器 ZIP、独立 npm 全局升级或任意自定义安装参数。

用户选好版本和来源后点击“下载并安装”：

1. 先从官网 HTTPS 取得所选文件的 SHA256。Python 读取 `.sigstore` 的 SHA2_256 messageDigest；这是以官网 HTTPS 为信任来源的摘要读取，不是完整的 Sigstore 验证器。Node 精确匹配官方 `SHASUMS256.txt` 中的安装器文件名。不支持的校验元数据或官网不可达会阻止安装，不退回镜像提供的摘要。
2. 使用现有 `IDownloadService` 下载至工具缓存下独立目录。默认安装包源是 `https://cdn.npmmirror.com/binaries/`，可切换官方网站；请求不携带后台认证。进度、暂停及取消使用现有下载管理器。镜像不可达时由用户切换来源；没有添加自有后台镜像接口。
3. 核对下载结果路径、文件名、SHA256，调用 Windows `WinVerifyTrust` 验证签名及吊销状态，并要求发布者为 Python Software Foundation 或 OpenJS Foundation。任何失败均不启动安装器。校验在后台线程执行，文件在校验至安装器退出期间保持禁止写入/替换的打开句柄。
4. 回到 UI 线程，在窗口仍有效时启动官方交互式向导。Python 不强制提权或自定义路径；Node 通过系统 msiexec `/i` 启动，不使用静默安装开关。默认安装位置、PATH、作用范围及必要 UAC 由官方安装器和用户决定。Node MSI 可能升级/替换现有版本，Python PATH 选项也会影响其他软件，界面对此明确提示。
5. 向导退出后重新检测。退出码、3010 重启提示和是否发现所选版本分别呈现，不以“下载成功”“进程退出”单独断言安装完成。窗口关闭取消后续启动/等待，但不会杀死已经启动的安装器；下载管理器已经接收的任务也不会因窗口关闭自动取消。

此窗口不自动卸载旧版本、不写系统 PATH、不改变 pip index/npm registry、不安装 cnpm、不安装项目库。安装包镜像与 pip/npm 软件包源是不同配置。Windows 证书链/吊销服务不可达可能导致校验失败；不能通过取消签名检查来恢复安装。

## 实现与验证边界

`DeveloperToolsAppProvider` 通过既有 `IThirdPartyAppProvider` 发现链注册入口，同一应用内复用已打开的窗口。界面位于主程序 ToolPlugins，系统发现、目录解析及安装器信任校验位于 Engine Services；下载仍经过既有 UI 下载接口。

`DeveloperToolSafetyTests` 覆盖稳定版本过滤、架构/文件名匹配、校验信息拒绝、篡改/未签名文件拒绝、失败后释放文件以及 PATH 顺序和不执行文件。相关测试不安装、升级或卸载系统软件。实际 UAC、官方安装向导及安装后的系统 PATH 刷新仍需在允许改变环境的测试机验收。

参考：[Python Windows 下载](https://www.python.org/downloads/windows/)、[npm 与 Node.js 安装说明](https://docs.npmjs.com/downloading-and-installing-node-js-and-npm/)、[npmmirror](https://npmmirror.com/)、[Windows WinVerifyTrust](https://learn.microsoft.com/en-us/windows/win32/api/wintrust/nf-wintrust-winverifytrust)。公共镜像当前可达不等于客户网络带宽或长期可用性保证。
