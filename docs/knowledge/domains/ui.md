---
generated_knowledge_index: true
search: false
editLink: false
prev: false
next: false
---

# UI 与图像交互

> 自动生成的领域目录。修改主题 Markdown 元数据后运行 `node docs/.vitepress/scripts/knowledge.mjs generate`；不要手工编辑。

属性编辑器、窗口组件、图像交互和绘制扩展。 返回[知识总入口](../index.md)。

只读与当前问题相关的主题，再核对源码和测试。`规划`、`历史`不代表当前能力。

- [UI 组件目录](../../04-api-reference/ui-components/control-catalog.md) — `ui.control-catalog`
  按控件、窗口和扩展接口定位对应 UI 源码与专题。

- [ColorVision.UI 壳层责任与知识入口](../../04-api-reference/ui-components/ColorVision.UI.md) — `ui.framework`
  ColorVision.UI壳层责任入口：按配置、插件、菜单、热键、搜索、语言、状态栏、属性编辑和日志定位规范主题，业务行为仍归所属模块。

- [UI 知识入口](../../04-api-reference/ui-components/README.md) — `ui.index`
  按问题路由到 UI 模块、属性编辑契约、运行时发现与 DLL 发布证据。

- [共享接口、属性通知与粗粒度权限](../../04-api-reference/ui-components/ColorVision.Common.md) — `ui.common`
  共享接口的宿主接入、属性通知与命令的同步执行限制、粗粒度权限判据，以及第三方工具发现和启动边界。

- [配置持久化、重载与对象所有权](../../04-api-reference/ui-components/configuration.md) — `ui.configuration`
  ConfigHandler的配置路径、延迟实例、文件合并保存和重载契约；单文件替换不等于内存发布成功，重载会使旧配置引用失效。

- [数据库连接、DAO 与旧插件兼容](../../04-api-reference/ui-components/ColorVision.Database.md) — `ui.database`
  MySQL 连接配置、业务 DAO 与批 SQL 的失败边界，以及旧插件注册的二进制兼容。

- [通用查询、条件会话与整表操作](../../04-api-reference/ui-components/database-query.md) — `ui.database-query`
  实体驱动的通用查询窗口：条件参数化、执行时SQL预览、结果替换与进程内会话；关闭不取消查询，清空表/截断表作用于整表而非筛选结果。

- [桌面宠物](../../04-api-reference/ui-components/desktop-pet.md) — `ui.desktop-pet`
  桌面宠物的启用、选择、Codex 创建、本地素材导入、精灵表规格、配置与故障定位；创建结果由设置页限时发现。

- [UI 运行时组件](../../04-api-reference/ui-components/ui-runtime-handoff.md) — `ui.discovery`
  排查程序集加载后菜单、设置、PropertyGrid、工具和服务扩展的发现链。

- [编辑器选择、文档生命周期与停靠布局](../../04-api-reference/ui-components/editor-document-lifecycle.md) — `ui.documents`
  编辑器注册与选择、按路径和编辑器区分文档、保存重载关闭及外部变更；停靠布局不恢复未注册文件标签，重置也不预审脏文档。

- [快捷键：发现、注册、编辑与释放](../../04-api-reference/ui-components/hotkeys.md) — `ui.hotkeys`
  快捷键的发现、多组绑定、窗口/全局注册与搜索编辑；同一操作共享作用域，未分配操作保留展示，确认后立即保存，注册或持久化失败按结果补偿。

- [ColorVision.ImageEditor：打开、绘制与输出](../../04-api-reference/ui-components/ColorVision.ImageEditor.md) — `ui.image-editor`
  图像/视频打开、绘图撤销、叠加层、3D 与快照输出边界，区分渲染图、当前源像素和重读源文件的模型导出。

- [ImageEditor：上下文、工具装配与临时选区](../../04-api-reference/ui-components/image-editor-context.md) — `ui.image-editor-context`
  ImageEditor 的状态归属、扩展构造、工具刷新与临时 ROI 有效期；区分配置分类、图像版本和真实像素坐标。

- [源图像帧：租约、位图复制与缓存失效](../../04-api-reference/ui-components/image-frame-lifetime.md) — `ui.image-frames`
  位图读取时借用原图内存与复制像素的区别、租约释放责任和缓存版本；原图修改须显式失效，复制HImage不延长租约。

- [景深融合：输入、执行与结果生命周期](../../04-api-reference/ui-components/image-fusion.md) — `ui.image-fusion`
  景深融合的CPU/CUDA调用、HImage显示和计时；自动模式不做失败回退，关窗不取消计算，GPU少量图片存在未修复的越界风险。

- [多图查看、刷新与缩略图缓存](../../04-api-reference/ui-components/ColorVision.ImageTools.md) — `ui.image-tools`
  ImageTools内置注册、多图列表中的单张预览、刷新与SQLite缩略图缓存；重选不保证重载，关窗不清缓存，缓存关闭也不等于零数据库访问。

- [界面语言：资源发现、配置与重启](../../04-api-reference/ui-components/localization.md) — `ui.localization`
  界面语言的资源发现、系统语言回退、设置绑定和重启切换；语言下拉框不证明插件翻译完整，修改配置值不等于刷新窗口。

- [菜单：发现、显示、执行与管理提交](../../04-api-reference/ui-components/menus.md) — `ui.menus`
  菜单的插件 DLL 发现、类型缓存、父子树和管理提交；IHotKey 提示随运行时键位更新，隐藏不禁用快捷键，应用成功提示不保证配置落盘，菜单入口不构成统一鉴权。

- [PropertyGrid 属性编辑契约](../../04-api-reference/ui-components/property-grid.md) — `ui.property-grid`
  属性面板的字段生成、编辑器选择和 Flow 适配；区分直接修改、工作副本、关闭、重置与宿主持久化。

- [Quartz 任务定义、恢复与执行历史](../../04-api-reference/ui-components/ColorVision.Scheduler.md) — `ui.scheduler`
  Quartz 调度定义的启动恢复、JSON/SQLite 分工与执行统计；暂停不终止在途任务，重启恢复不是执行进度续跑。

- [应用搜索：入口、候选与执行](../../04-api-reference/ui-components/search.md) — `ui.search`
  应用搜索窗口的入口、关键词匹配、候选来源、缓存刷新与命令执行；Ctrl+F 按焦点执行局部查找，Ctrl+Shift+P 打开应用搜索。

- [设置窗口：发现、编辑与关闭契约](../../04-api-reference/ui-components/settings.md) — `ui.settings`
  设置窗口的元数据发现、全局搜索定位、侧栏筛选与活对象编辑；普通选项关窗不撤销，启动检查更新仍是聚合开关。

- [TCP 监听、协议分发与消息记录](../../04-api-reference/ui-components/ColorVision.SocketProtocol.md) — `ui.socket-protocol`
  TCP网络通信的监听快照、窗口关闭与服务停止、JSON/Text分发及消息记录；Sent不证明对端执行，重发可能换客户端并追加记录。

- [资源打开与单工作区切换](../../04-api-reference/ui-components/ColorVision.Solution.md) — `ui.solution`
  工作区与普通文件的打开分流、单工作区切换和取消、私有cvsln与共享配置恢复；打开和加载不保证无写入。

- [SQLite 正文存储、迁移与文件维护](../../04-api-reference/ui-components/sqlite-storage.md) — `ui.sqlite-storage`
  Socket 与 Flow 的 SQLite 正文 gzip 编解码、按ID读写、旧TEXT逐批迁移、WAL备份与VACUUM；通用工具不自动停写/备份/恢复，失败可能已有批次提交。

- [状态栏：发现、刷新与宿主生命周期](../../04-api-reference/ui-components/status-bar.md) — `ui.status-bar`
  状态栏的插件发现、活动文档通知、绑定更新、控件重建和关闭生命周期；刷新不保证发现新provider，隐藏不等于保存偏好或停止采样。

- [存储清理与选择性设置重置](../../04-api-reference/ui-components/storage-maintenance.md) — `ui.storage-maintenance`
  设置中的存储清理与选择性启动重置：先确认白名单扫描清单，保护活跃任务和业务数据；删除不回滚，重置先独立备份再在启动时应用。

- [主题选择、资源应用与窗口外观](../../04-api-reference/ui-components/ColorVision.Themes.md) — `ui.themes`
  在外观与语言中切换主题；ThemeManager 的资源应用、系统跟随、窗口外观和公共控件样式，以及即时预览与保存的区别。

- [配置向导：步骤、应用与完成边界](../../04-api-reference/ui-components/wizards.md) — `ui.wizards`
  配置向导的步骤发现、初始化时序、前进应用和完成标记；关闭不回滚，完成标记不证明组件健康或重启成功。

- [UI NuGet 包构建与发布](../../04-api-reference/ui-components/publishing.md) — `ui.publishing`
  UI NuGet整批与Algorithms单包发布、Release标签和版本预检；预检不预留版本，逐包上传没有整批回滚或逐条失败检查。

- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md) — `ui.core`
  定位 HImage 所有权、OpenCV/CUDA PInvoke、ImageCompute 融合分流、位图桥接与默认关闭的原生日志。

- [ColorVision.UI.Desktop](../../04-api-reference/ui-components/ColorVision.UI.Desktop.md) — `ui.desktop`
  桌面辅助壳层而非产品主入口：定位设置、市场下载、第三方工具、反馈和特权崩溃诊断。

- [UI 包职责与依赖边界](../../04-api-reference/ui-components/component-handbook.md) — `ui.package-boundaries`
  UI类库的职责、依赖与目标框架/版本兼容；包版本可独立于主程序，中立算法与窗口适配分层维护。
