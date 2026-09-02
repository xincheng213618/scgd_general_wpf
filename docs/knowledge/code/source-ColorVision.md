---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# ColorVision 源码知识

> 自动生成的源码目录。修改主题 Markdown 的 `code_paths` 后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

返回[知识总入口](../index.md)。只读与当前模块有关的主题，再核对其中的源码、测试和状态；`规划`、`历史`不代表当前能力。

以下是已声明源码路径的关联，不是完整调用图或完整模块清单。跨模块主题可出现在多处；根目录概览只列在根目录项，不自动覆盖所有子模块。

## ColorVision/ 根目录与跨模块关联 {#module-436f6c6f72566973696f6e}

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。

- [安装、构建与运行入口](../../00-getting-started/README.md) — `delivery.start`
  克隆代码后的源码问答、本地构建、安装和运行分流；只问Codex不需要先启动程序。

- [架构设计](../../03-architecture/README.md) — `platform.architecture`
  按启动、跨模块调用、流程、模板与权限问题定位架构契约。

- [Copilot 输入、命令与活动呈现](../../02-developer-guide/core-concepts/copilot-local-interactions.md) — `copilot.interactions`
  Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。

- [Backend Operations 中继与只读概览](../../02-developer-guide/backend/operations-relay.md) — `delivery.backend-operations`
  Backend Operations 的 Bearer 与设备签名中继、任务回执和管理员只读投影；在线、排队、验签与真实动作完成各有边界。

- [更新扫描保护：临时排除项与清理所有权](../../02-developer-guide/deployment/update-scan-protection.md) — `delivery.update-scan-protection`
  ServiceHost提供的主程序增量更新临时Defender排除项、目录准入和清理所有权；启用失败不阻断更新，服务停止或保护超时不保证排除项立即恢复。

- [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) — `engine.cv-image-export`
  CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。

- [RC 注册、服务快照与连接测试](../../04-api-reference/engine-components/rc-registration.md) — `engine.rc-registration`
  RC注册、服务目录同步、状态快照与连接测试；远端删除不清本地令牌和收发主题，更新可能部分生效，连接或测试成功不等于设备就绪。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。

- [RBAC：登录缓存、会话与权限边界](../../03-architecture/security/rbac.md) — `platform.rbac`
  本地RBAC的登录缓存、会话校验和权限同步限制，以及自动登录失败、登出撤销和用户中心统计的实际边界。

- [架构运行时](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动分支、配置初始化、插件装载和恢复流程的运行时顺序。

- [启动失败上报与缺依赖告警](../../03-architecture/components/startup-integrity.md) — `platform.startup-integrity`
  主程序启动失败识别、状态上报和后台缺依赖告警；十秒观察不强杀进程，已处理终态抑制重复弹窗，无告警不证明安装完整。

- [系统职责与跨模块边界](../../03-architecture/overview/system-overview.md) — `platform.system`
  宿主、UI、Engine、插件与项目的职责及调用边界：UI操作不必经过Engine，程序集依赖不是统一执行顺序，构建产物不等于交付制品。

- [插件装载、依赖门禁与扩展发现](../../02-developer-guide/plugin-development/overview.md) — `plugins.model`
  PluginLoader的manifest/依赖门禁、禁用缓存、程序集发现和失败边界；载入不等于provider可用，也不支持隔离卸载。

- [WindowsServicePlugin：选包、本机安装与恢复](../../04-api-reference/plugins/standard-plugins/windows-service.md) — `plugins.windows-service`
  WindowsServicePlugin的在线选包与缓存、本机完整安装、数据库版本切换和恢复边界；下载、日志完成、备份与实际服务状态不能互相替代。

- [桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) — `ui.desktop-pet`
  桌面宠物的启用、选择、Codex 创建、本地素材导入、精灵表规格、配置与故障定位；创建结果由设置页限时发现。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [TCP 监听、协议分发与消息记录](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) — `ui.socket-protocol`
  TCP网络通信的监听快照、窗口关闭与服务停止、JSON/Text分发及消息记录；Sent不证明对端执行，重发可能换客户端并追加记录。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  ThemeManager的主题选择、资源追加、系统跟随和窗口外观契约；选择不等于应用成功，预览不等于配置落盘。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [安装制品与运行输出](../../00-getting-started/installation.md) — `delivery.installation`
  区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。

- [系统要求与首次构建](../../00-getting-started/prerequisites.md) — `delivery.prerequisites`
  Windows x64 运行与源码构建前提：Desktop Runtime、SDK、C++ 工具集及已有 native DLL 的选择。

- [主程序启动与最小图像验证](../../00-getting-started/first-steps.md) — `operations.first-run`
  主程序启动的配置、实例和服务副作用，以及隔离测试环境中的最小本地图像验证。

- [ColorVision 概览](../../00-getting-started/what-is-colorvision.md) — `platform.product`
  ColorVision 的设备、流程、图像分析、结果、插件与客户项目能力，以及从任务进入文档的方法。

## ColorVision/Copilot {#module-436f6c6f72566973696f6e2f436f70696c6f74}

- [Copilot Agent Runtime](../../02-developer-guide/core-concepts/copilot-agent-runtime.md) — `copilot.runtime`
  ColorVision Copilot 的 Agent Framework 执行层、宿主策略边界和按任务检索的专题路由。

- [统一图像算法平台 V1](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md) — `algorithms.platform`
  统一图像算法Catalog、Invocation和Runner；普通像素预览、应用/取消、所有权与发布门禁；ONNX仅设计。

- [Copilot 设置、持久化与连接诊断](../../02-developer-guide/core-concepts/copilot-configuration.md) — `copilot.configuration`
  ColorVision内置Copilot的设置草稿、配置保存与运行态发布、模型选择和联网诊断；保存失败可能已落盘，Local MCP测试核验会话握手与只读状态调用。

- [Copilot Agent 执行链](../../02-developer-guide/core-concepts/copilot-agent-execution.md) — `copilot.execution`
  Copilot 请求调度、工具筛选、审批、只读委派与执行证据闭环。

- [Copilot 扩展、MCP 与 Hook](../../02-developer-guide/core-concepts/copilot-agent-extensions.md) — `copilot.extensions`
  业务模块动态上下文、外部 MCP client 和 Hook 如何进入统一宿主权限与生命周期。

- [Copilot 输入、命令与活动呈现](../../02-developer-guide/core-concepts/copilot-local-interactions.md) — `copilot.interactions`
  Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。

- [Copilot 生命周期、预算与 Skills](../../02-developer-guide/core-concepts/copilot-agent-lifecycle.md) — `copilot.lifecycle`
  Copilot 任务生命周期、恢复预算、项目指令发现和 Skill 渐进加载的契约。

- [Copilot 任务、恢复与内置工具](../../02-developer-guide/core-concepts/copilot-agent-session-and-tools.md) — `copilot.session-tools`
  Copilot 会话检查点、任务呈现、重试和内置工具的状态恢复与安全边界。

- [Copilot 工具契约](../../02-developer-guide/core-concepts/copilot-agent-tool-contracts.md) — `copilot.tool-contracts`
  Copilot 工具结果、事件、审批恢复和 Flow 编辑必须遵守的执行契约。

- [Copilot ViewModel 维护地图](../../02-developer-guide/core-concepts/copilot-view-model-architecture.md) — `copilot.view-model`
  CopilotChatViewModel 的状态所有权、请求边界、会话与输入状态拆分和测试入口。

- [CVRAW / CVCIE 图像导出](../../04-api-reference/engine-components/cv-image-export.md) — `engine.cv-image-export`
  CVRAW/CVCIE 原生导出的窗口、命令行参数、通道和命名规则，以及覆盖、部分失败和退出码边界。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [ColorVision 本地 MCP](../../02-developer-guide/core-concepts/colorvision-mcp.md) — `copilot.mcp-server`
  ColorVision 入站本地 MCP 的 loopback 认证、会话、能力白名单与二次确认契约。

## ColorVision/FloatingBall {#module-436f6c6f72566973696f6e2f466c6f6174696e6742616c6c}

- [Copilot 输入、命令与活动呈现](../../02-developer-guide/core-concepts/copilot-local-interactions.md) — `copilot.interactions`
  Copilot 命令目录、输入与引用、会话导航及消息/桌宠呈现；本地入口不等于无副作用。

- [桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) — `ui.desktop-pet`
  桌面宠物的启用、选择、Codex 创建、本地素材导入、精灵表规格、配置与故障定位；创建结果由设置页限时发现。

## ColorVision/NativeLogging {#module-436f6c6f72566973696f6e2f4e61746976654c6f6767696e67}

- [日志来源、历史读取与筛选](../../01-user-guide/interface/log-viewer.md) — `operations.logs`
  区分log4net输出、历史文件读取与UI筛选，说明刷新、截断和原生日志采集边界；没有显示不等于动作未发生。

## ColorVision/Recovery {#module-436f6c6f72566973696f6e2f5265636f76657279}

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。

- [检查更新、重新安装与程序备份](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  检查更新、重新安装与程序备份入口，以及主程序和插件的检查复用、下载安装、失败回退与启动恢复。

- [跨模块运行问题定位](../../01-user-guide/README.md) — `operations.index`
  从启动、配置、日志、设备、流程和结果现象定位代码责任，区分已完成阶段与待验证阶段，避免用重启或改数据代替诊断。

- [架构运行时](../../03-architecture/overview/runtime.md) — `platform.runtime`
  启动分支、配置初始化、插件装载和恢复流程的运行时顺序。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

## ColorVision/ServiceHost {#module-436f6c6f72566973696f6e2f53657276696365486f7374}

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [ColorVisionServiceHost：本机权限代理与生命周期](../../03-architecture/components/service-host.md) — `platform.service-host`
  ColorVisionServiceHost本机权限代理的身份、票据与就绪：客户端超时不取消命令，服务停止超过两分钟仍等待排空，服务启动成功日志不证明后台清理和启动完整性检查完成。

## ColorVision/Settings {#module-436f6c6f72566973696f6e2f53657474696e6773}

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

## ColorVision/Themes {#module-436f6c6f72566973696f6e2f5468656d6573}

- [主窗口与入口装配](../../01-user-guide/interface/main-window.md) — `operations.main-window`
  主窗口如何挂接菜单、搜索、状态栏和工作区，以及现代停靠外观的主题覆盖与交互边界。

## ColorVision/ToolPlugins {#module-436f6c6f72566973696f6e2f546f6f6c506c7567696e73}

- [系统开发工具管理](../../02-developer-guide/core-concepts/developer-tools-manager.md) — `platform.developer-tools`
  独立开发工具窗口发现系统 Python、Node.js/npm，并由用户选择校验后启动官方安装向导；不托管项目环境，不自动改默认版本。

## ColorVision/Update {#module-436f6c6f72566973696f6e2f557064617465}

- [桌面交付制品与责任路由](../../02-developer-guide/deployment/overview.md) — `delivery.deployment`
  按源码输出、完整安装器、主程序更新包及插件项目包定位交付责任；安装、更新与启动恢复各有完成边界，旧ColorVisionSetup不是当前入口。

- [检查更新、重新安装与程序备份](../../02-developer-guide/deployment/auto-update.md) — `delivery.update`
  检查更新、重新安装与程序备份入口，以及主程序和插件的检查复用、下载安装、失败回退与启动恢复。

- [更新扫描保护：临时排除项与清理所有权](../../02-developer-guide/deployment/update-scan-protection.md) — `delivery.update-scan-protection`
  ServiceHost提供的主程序增量更新临时Defender排除项、目录准入和清理所有权；启用失败不阻断更新，服务停止或保护超时不保证排除项立即恢复。

- [Explorer 缩略图读取与 COM 注册](../../04-api-reference/engine-components/ColorVision.ShellExtension.md) — `engine.shell-extension`
  Explorer 的 CVRAW/CVCIE COM provider 如何读取像素、生成非测量用途缩略图，以及源码脚本与 ServiceHost 注册的不同副作用和失败边界。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

- [安装制品与运行输出](../../00-getting-started/installation.md) — `delivery.installation`
  区分完整安装制品、增量更新和源码输出，定位安装后缺依赖、配置与启动问题。

## ColorVision/Wizards {#module-436f6c6f72566973696f6e2f57697a61726473}

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。
