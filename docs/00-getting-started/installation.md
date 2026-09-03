---
knowledge_id: "delivery.installation"
knowledge_type: "guide"
status: "current"
summary: "区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。"
aliases: ["安装失败","如何安装","部署","安装包","缺DLL"]
code_paths: ["ColorVision/ColorVision.csproj","ColorVision/Update","Scripts/release.bat"]
test_paths: []
related: ["delivery.prerequisites","delivery.deployment","delivery.update","operations.first-run"]
---

# 安装制品与运行输出

首次部署使用完整安装包；已有安装通过更新入口升级；源码构建输出用于开发和本地验证。本页说明制品选择、安装前检查和故障定位，工具链要求见[系统要求](./prerequisites.md)，安装后的启动步骤见[首次运行](./first-steps.md)。

## 选择正确制品

| 场景 | 前提与检查 |
| --- | --- |
| 首次部署 | 从可信的项目发布或交付渠道取得完整安装包；增量更新包不替代首次安装 |
| 现有安装升级 | 先记录主程序、插件/项目包版本与配置，按[更新与恢复契约](../02-developer-guide/deployment/auto-update.md)确认包类型和回退条件 |
| 源码构建输出 | 按[环境与首次构建](./prerequisites.md)检查 .NET、C++/native、x64 与签名条件；托管编译成功不等于运行输出依赖完整 |
| 排查安装制品生成 | 查[部署链路](../02-developer-guide/deployment/overview.md)和[脚本契约](../02-developer-guide/scripts/README.md)，不要为了查看安装行为执行会上传的发布 wrapper |

安装器使用仓库外的 Advanced Installer 工程，具体组件与权限提示以本次交付包为准；本页不把未经核对的向导按钮、默认组件或升级行为当作代码契约。

主程序通过 `ColorVision/Update/ChangelogPage.cs` 打开网页变更日志。仓库根目录 `CHANGELOG.md` 保留为项目链接和网站发布原稿，不复制到主程序的构建或 publish 输出目录；已有文件不清理。安装器、更新包和共享文件清单的日志排除规则见[构建与发布脚本](../02-developer-guide/scripts/README.md#正式发布)。

## 部署前与部署后边界

- 先确认 Windows x64 环境、目标安装目录和当前交付版本，核对安装目录写权限；安装服务或提升权限需要相应授权。
- 安装、升级可能覆盖程序文件或修改系统状态；已有配置和客户数据不能作为排障默认删除目标。
- 若文件被占用，先记录占用进程与具体文件，交由获准的操作者安排停机。不要自动结束用户进程或停止现场服务。
- 源码运行输出的依赖由项目引用和复制规则决定；缺失 DLL 时查 `ColorVision/ColorVision.csproj` 及[运行时依赖](./prerequisites.md)，不要以“安装已结束”证明依赖齐全。
- 启动可能初始化模块和连接服务，不应在安装完成后无条件勾选启动。确认环境后再按[启动与最小运行验证](./first-steps.md)操作，该主题维护唯一的启动检查步骤。

## 失败定位

| 现象 | 先收集证据，再检查 |
| --- | --- |
| 安装程序无法启动 | 包来源、版本、系统错误与完整性证据；系统策略拦截需要由获准人员处理，不绕过安全策略 |
| 文件无法写入或升级覆盖失败 | 实际目标路径、权限和占用者；不要盲目切换安装目录留下并行旧版本 |
| 安装后启动失败 | 本次日志、缺失 DLL 名、native 架构、配置解析错误；进入首次运行主题核对启动阶段 |
| 升级后行为异常 | 主程序/插件/项目包版本是否匹配，实际加载位置、旧配置和恢复状态；先定位差异再决定修复或回退 |

## 验证缺口

本页没有声明安装器自动化覆盖。知识校验只验证引用；真正的安装、升级、卸载、服务变更与回退，需要在获准的目标环境记录安装日志、实际版本及结果，不能由一次构建或文档检查代替。
