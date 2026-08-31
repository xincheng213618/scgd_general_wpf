---
knowledge_id: "delivery.deployment"
knowledge_type: "index"
status: "current"
summary: "按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。"
aliases: ["部署","交付制品","安装器","完整安装包","源码构建输出","增量更新包","项目包交付","启动恢复入口","Advanced Installer","ColorVision.aip","ColorVisionSetup","CombinedUpdateCoordinator","StartupRecoveryWindow"]
code_paths: ["ColorVision/ColorVision.csproj","Scripts/release.bat","Scripts/build.py","ColorVision/Update/CombinedUpdateCoordinator.cs","ColorVision/Recovery/StartupRecoveryWindow.xaml.cs","src/ColorVisionSetup"]
test_paths: []
related: ["delivery.installation","delivery.prerequisites","delivery.update","delivery.scripts","plugins.getting-started","operations.first-run","delivery.backend"]
---

# 桌面交付制品与责任路由

本页回答“这次拿到或准备生成的是什么制品，后续由哪条实现负责”。源码输出、完整安装包、在线更新差异包和插件包不是可互换的交付物；安装完成、更新进程接管和主程序健康启动也不是同一个结果。

## 按制品和状态定位

| 要处理的对象 | 权威主题 | 实现责任 |
| --- | --- | --- |
| 本地源码构建输出 | [环境与构建前提](../../00-getting-started/prerequisites.md) | 项目引用、native/x64 和输出复制规则；编译命令不在部署索引重复维护 |
| 完整桌面安装包与目标安装目录 | [安装制品与运行输出](../../00-getting-started/installation.md) | 首次安装、目录权限、依赖与配置检查；完整安装工程边界见本页下一节 |
| 主程序、插件或项目包的发布制品 | [构建与发布脚本](../scripts/README.md) | `Scripts/release.bat` 及对应包脚本的构建、签名、上传与成功判定；发布入口会产生外部写入，不是文档验证命令 |
| 现有主程序安装的在线更新 | [更新与恢复](./auto-update.md) | `CombinedUpdateCoordinator` 协调主程序/插件计划，下载缓存、包校验和外部更新执行由该主题维护 |
| 插件与项目包 `.cvxp` | [插件产物、安装与交付](../plugin-development/getting-started.md) | HostCopy、manifest 身份、宿主共享依赖和安装交接；包上传仍归发布脚本 |
| 安装后的启动或未完成启动 | [启动与最小运行验证](../../00-getting-started/first-steps.md)、[更新与恢复](./auto-update.md) | 普通启动初始化与 `StartupRecoveryWindow` 的修复/插件处置分别核对；恢复窗口出现不等于问题已修复 |

网络请求复用、重试、增量复制、程序快照、插件备份和恢复交接的完整契约只在各自主题维护，不从“部署”一词推定这些阶段已经成功。客户项目的专用配置、资源与版本约束应回到对应项目包核对，不能把通用安装器当作所有项目的完整交付清单。

## 外部安装工程与历史源码

当前主程序发布 wrapper 调用 `Scripts/build.py`；后者以 `build.sln` 为解决方案，并指定仓库外的 Advanced Installer `ColorVision.aip`。拉取仓库并不同时取得该安装工程；要改安装组件、权限或文件清单，必须核对实际使用的外部工程与交付包，不能仅由托管项目编译成功推断。

`src/ColorVisionSetup/` 保留历史安装/更新程序源码，未被当前 `build.sln` 与 `Scripts/release.bat` 的主程序发布链引用，不作为新的安装器或更新入口。当前客户端更新位于 `ColorVision/Update/`，启动恢复位于 `ColorVision/Recovery/`；不要从旧目录仍存在推断其仍参与交付。

[Web 后端部署](../backend/README.md)是独立责任链。Docker、云服务或集群方式不因后端存在就成为 Windows WPF 桌面程序的默认交付方式。

## 验证边界

测试入口和最小验证方法随具体制品主题维护；本索引不声明完整安装器、远端发布或启动恢复的端到端自动化覆盖。文档和路径检查不能代替目标环境验收，也不授权启动应用、连接设备、安装/回退或执行发布。
