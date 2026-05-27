# i18n Main UI Audit Table

This document tracks the internationalization progress for the ColorVision main application UI.

## Summary

- **Processed Projects**: ColorVision/ (main app - Copilot XAML files, FloatingBall, Update, App)
- **New Resource Keys Added**: 200+
- **XAML Files Updated**: 6
- **C# Files Updated**: 10+
- **Build Status**: 0 errors

## Audit Table

| Project | File | Location | Resource Key | Chinese | English | Status |
|---|---|---|---|---|---|---|
| ColorVision | CopilotChatPanel.xaml | FallbackValue/TargetNullValue | CopilotNewConversation | 新会话 | New Conversation | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (expand) | CopilotExpandConversationList | 展开会话列表 | Expand Conversation List | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (new) | CopilotNewConversation | 新会话 | New Conversation | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (config) | CopilotConfigureCopilot | 配置 Copilot | Configure Copilot | Replaced |
| ColorVision | CopilotChatPanel.xaml | Content (copy) | Copy | 复制 | Copy | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (copy) | CopilotCopyMessage | 复制当前消息 | Copy Current Message | Replaced |
| ColorVision | CopilotChatPanel.xaml | Content (retry) | CopilotRetry | 重试 | Retry | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (retry) | CopilotRegenerateAnswer | 重新生成当前轮回答 | Regenerate Current Answer | Replaced |
| ColorVision | CopilotChatPanel.xaml | Content (refresh) | Refresh | 刷新 | Refresh | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (refresh) | CopilotRefreshWebContext | 重新抓取网页上下文并生成回答 | Re-fetch Web Context and Generate Answer | Replaced |
| ColorVision | CopilotChatPanel.xaml | Text (context) | CopilotCurrentWindowContext | 当前窗口上下文 | Current Window Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (attach) | CopilotAttachTooltip | 挂载文件、图片或上下文 | Attach Files, Images, or Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (paste) | CopilotPasteImage | 粘贴图片 | Paste Image | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (web) | CopilotSpecifyWebPage | 指定网页 | Specify Web Page | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (file) | CopilotAddFile | 添加文件 | Add File | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (context) | CopilotAddContext | 添加上下文 | Add Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (collapse) | CopilotCollapseConversationList | 收起会话列表 | Collapse Conversation List | Replaced |
| ColorVision | CopilotChatPanel.xaml | Text (conversations) | CopilotConversations | 会话 | Conversations | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (rename) | Rename | 重命名 | Rename | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (delete) | Delete | 删除 | Delete | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Title | CopilotLanguageModel | 语言模型 | Language Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (title) | CopilotLanguageModel | 语言模型 | Language Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (desc) | CopilotModelConfigDescription | 可同时保存多个模型配置；保存后，右侧 AI 面板会直接使用这里的模型列表。 | Save multiple model configurations; the AI panel on the right will use this model list directly. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (add) | CopilotAddModel | + 添加模型 | + Add Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (copy) | Copy | 复制 | Copy | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (delete) | Delete | 删除 | Delete | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (hint) | CopilotVendorHint | 先选厂商再添加；右侧仍可继续修改兼容性、地址、模型和系统提示词。 | Select a vendor first, then add; you can continue to modify compatibility, address, model, and system prompt on the right. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (name) | CopilotName | 名称 | Name | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (vendor) | CopilotVendor | 厂商 | Vendor | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (protocol) | CopilotProtocol | 协议 | Protocol | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (model) | CopilotModel | 模型 | Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (token) | CopilotMaxToken | 最大 Token | Max Token | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (temp) | CopilotTemperature | 温度 | Temperature | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (rounds) | CopilotToolRounds | 工具轮次 | Tool Rounds | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (prompt) | CopilotSystemPrompt | 系统提示词 | System Prompt | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (settings hint) | CopilotSettingsHint | 支持厂商预设、模型预设和手动地址输入；右侧 AI 面板发送前可直接切换模型。 | Supports vendor presets, model presets, and manual address input; the AI panel can switch models before sending. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (cancel) | Cancel | 取消 | Cancel | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (save) | Save | 保存 | Save | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Title | CopilotExceptionDiagnosis | 异常诊断 | Exception Diagnosis | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Text (header) | CopilotUnhandledException | ColorVision 捕获到未处理异常 | ColorVision Captured an Unhandled Exception | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (details) | CopilotExceptionDetails | 异常详情 | Exception Details | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (logs) | CopilotRecentLogs | 最近日志 | Recent Logs | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (n lines) | CopilotRecentNLines | 最近 N 行 | Recent N Lines | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Text (lines) | CopilotLines | 行 | Lines | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (today) | CopilotTodayLog | 当日日志 | Today's Log | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (refresh) | CopilotRefreshLog | 刷新日志 | Refresh Log | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (ai) | CopilotAiRequest | AI 请求 | AI Request | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (copy) | CopilotCopyDetails | 复制详情 | Copy Details | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (ask) | CopilotAskAi | 询问 AI | Ask AI | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (search) | CopilotGoogleSearch | Google 搜索 | Google Search | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (close) | Close | 关闭 | Close | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Title | Input | 输入 | Input | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Content (cancel) | Cancel | 取消 | Cancel | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Content (ok) | OK | 确定 | OK | Replaced |
| ColorVision | AboutMsg.xaml | Text (ok) | OK | 确定 | OK | Replaced |
| ColorVision | ChangelogWindow.xaml | Content (open) | Open | 打开 | Open | Replaced |
| ColorVision | ChangelogWindow.xaml | Content (default) | OpenWithSystemDefault | 系统默认打开 | Open with System Default | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (test) | DesktopPetSendTestReminder | 发送测试提醒 | Send Test Reminder | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Notify (title) | DesktopPetReminder | 桌宠提醒 | Pet Reminder | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Notify (message) | DesktopPetTestMessage | 这是一条来自桌宠的测试消息。 | This is a test message from the desktop pet. | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (show) | DesktopPetShowMainWindow | 显示主窗口 | Show Main Window | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (settings) | DesktopPetSettings | 桌宠设置 | Pet Settings | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (topmost) | DesktopPetAlwaysOnTop | 始终置顶 | Always on Top | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (notifications) | DesktopPetShowNotifications | 显示通知 | Show Notifications | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (hide) | DesktopPetHide | 隐藏桌宠 | Hide Pet | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Header (exit) | DesktopPetExit | 退出程序 | Exit Program | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Idle tips | DesktopPetIdleTip1-4 | 我在这儿，有消息会提醒你。等 | I'm here, I'll remind you when there are messages. etc. | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Live2D error | DesktopPetLive2DError | 模型加载失败，已切换为内置形象。 | Model loading failed, switched to built-in appearance. | Replaced |
| ColorVision | FloatingBallWindow.xaml.cs | Live2D error detail | DesktopPetLive2DErrorDetail | 模型加载失败：{0} | Model loading failed: {0} | Replaced |
| ColorVision | DesktopPetService.cs | Greeting | DesktopPetStartupGreeting | ColorVision 已启动，我会在这里提醒你。 | ColorVision has started, I'll be here to remind you. | Replaced |
| ColorVision | DesktopPetService.cs | Title | DesktopPetSettingsTitle | 桌面宠物设置 | Desktop Pet Settings | Replaced |
| ColorVision | DesktopPetService.cs | Name | DesktopPetInitializerName | 桌面宠物 | Desktop Pet | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetName | 宠物名称 | Pet Name | Replaced |
| ColorVision | DesktopPetConfig.cs | Category | ConfigDesktopPetCategory | 桌面宠物 | Desktop Pet | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetAlwaysOnTop | 始终置顶 | Always on Top | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetShowNotifications | 显示消息通知 | Show Message Notifications | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetStartupGreeting | 启动问候 | Startup Greeting | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetEnableIdleTips | 启用待机提示 | Enable Idle Tips | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetIdleTipInterval | 待机提示间隔分钟 | Idle Tip Interval (Minutes) | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetMessageDisplaySeconds | 消息显示秒数 | Message Display Seconds | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetScale | 宠物缩放 | Pet Scale | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigDesktopPetOpacity | 宠物透明度 | Pet Opacity | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigLive2DEnable | 启用 Live2D 渲染 | Enable Live2D Rendering | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigLive2DPath | Live2D 模型或 HTML 路径 | Live2D Model or HTML Path | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigLive2DMaxFps | Live2D 最大帧率 | Live2D Max FPS | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigLive2DRenderScale | Live2D 渲染分辨率 | Live2D Render Scale | Replaced |
| ColorVision | DesktopPetConfig.cs | DisplayName | ConfigLive2DMotionEffects | Live2D 轻量动效 | Live2D Lightweight Motion Effects | Replaced |
| ColorVision | MainWindowConfig.cs | DisplayName | ConfigEnableDesktopPet | 启用桌面宠物 | Enable Desktop Pet | Replaced |
| ColorVision | MainWindowConfig.cs | Name | ConfigDesktopPet | 桌面宠物 | Desktop Pet | Replaced |
| ColorVision | App.xaml.cs | MessageBox | UnsupportedFileFormat | 不支持的文件格式 | Unsupported file format | Replaced |
| ColorVision | App.xaml.cs | MessageBox | PluginLoadFailedPrompt | 检测到软件上次没有成功打开，是否禁用插件 | The software failed to open last time. Do you want to disable plugins? | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdateCurrentVersion | 当前版本 | Current Version | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdateTargetVersion | 目标版本 | Target Version | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdateMethod | 更新方式 | Update Method | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Value | UpdateIncrementalPackage | 主体增量包 | Incremental Package | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Value | UpdateFullPackage | 完整安装包 | Full Package | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdatePackageCount | 更新包数 | Package Count | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdateScope | 更新范围 | Update Scope | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Value | UpdateFullApplicationUpdate | 主体完整更新 | Full Application Update | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdatePluginId | 插件 ID | Plugin ID | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Label | UpdateHostRequirement | 宿主要求 | Host Requirement | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Fallback | UpdateCompatibilityStability | 包含兼容性与稳定性更新。 | Includes compatibility and stability updates. | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Fallback | UpdateUnnamedPlugin | 未命名插件 | Unnamed Plugin | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Format | UpdatePluginCount | 等 {0} 个插件 | and {0} other plugins | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Format | UpdateIncrementalChain | 增量链：{0} | Incremental chain: {0} | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateExecutionIncremental | 执行方式：主体增量包与插件包会先全部下载，再一次性覆盖更新。 | Execution: Incremental packages and plugin packages will be downloaded first, then applied in one go. | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateExecutionFull | 执行方式：下载完整安装包，按原来的主体更新流程安装。 | Execution: Download the full installer and install using the current application update process. | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateNotes | 更新说明： | Update Notes: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdatePlugin | 插件： | Plugin: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdatePluginIdLabel | 插件 ID： | Plugin ID: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateCurrentVersionLabel | 当前版本： | Current Version: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateTargetVersionLabel | 目标版本： | Target Version: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateHostRequirementLabel | 宿主要求： | Host Requirement: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdatePluginDescription | 插件说明： | Plugin Description: | Replaced |
| ColorVision | CombinedUpdateCoordinator.cs | Text | UpdateVersionNotes | 版本说明： | Version Notes: | Replaced |
| ColorVision | ChangelogWindow.xaml.cs | MessageBox | CannotFindUpdateRecord | 无法找到更新记录 | Cannot find update record | Replaced |
| ColorVision | ChangelogWindow.xaml.cs | MessageBox | ReadUpdateRecordFailed | 读取更新记录失败: {0} | Failed to read update record: {0} | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | ShellExtensionNotFound | 未找到 Shell Extension:\n{0} | Shell Extension not found:\n{0} | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | ComRegistrationFailed | COM 注册失败，请确认已授予管理员权限。 | COM registration failed. Please confirm administrator privileges have been granted. | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | ThumbnailRegistrationSuccess | 缩略图预览注册成功！\n重启资源管理器后，.cvraw 和 .cvcie 文件将显示图像缩略图。 | Thumbnail preview registered successfully!\nAfter restarting the file explorer, .cvraw and .cvcie files will display image thumbnails. | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | RegistrationFailed | 注册失败：{0} | Registration failed: {0} | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | ThumbnailUnregistered | 缩略图预览已卸载。 | Thumbnail preview unregistered. | Replaced |
| ColorVision | MenuRegisterThumbnail.cs | MessageBox | UnregistrationFailed | 卸载失败：{0} | Unregistration failed: {0} | Replaced |
| ColorVision | FileAssociationHelper.cs | MessageBox | RegistryAppliedSuccess | 注册表应用成功 | Registry applied successfully | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (copy) | CopilotCopyMessage | 复制当前消息 | Copy Current Message | Replaced |
| ColorVision | CopilotChatPanel.xaml | Content (retry) | CopilotRetry | 重试 | Retry | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (retry) | CopilotRegenerateAnswer | 重新生成当前轮回答 | Regenerate Current Answer | Replaced |
| ColorVision | CopilotChatPanel.xaml | Content (refresh) | Refresh | 刷新 | Refresh | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (refresh) | CopilotRefreshWebContext | 重新抓取网页上下文并生成回答 | Re-fetch Web Context and Generate Answer | Replaced |
| ColorVision | CopilotChatPanel.xaml | Text (context) | CopilotCurrentWindowContext | 当前窗口上下文 | Current Window Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (attach) | CopilotAttachTooltip | 挂载文件、图片或上下文 | Attach Files, Images, or Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (paste) | CopilotPasteImage | 粘贴图片 | Paste Image | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (web) | CopilotSpecifyWebPage | 指定网页 | Specify Web Page | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (file) | CopilotAddFile | 添加文件 | Add File | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (context) | CopilotAddContext | 添加上下文 | Add Context | Replaced |
| ColorVision | CopilotChatPanel.xaml | ToolTip (collapse) | CopilotCollapseConversationList | 收起会话列表 | Collapse Conversation List | Replaced |
| ColorVision | CopilotChatPanel.xaml | Text (conversations) | CopilotConversations | 会话 | Conversations | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (rename) | Rename | 重命名 | Rename | Replaced |
| ColorVision | CopilotChatPanel.xaml | Header (delete) | Delete | 删除 | Delete | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Title | CopilotLanguageModel | 语言模型 | Language Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (title) | CopilotLanguageModel | 语言模型 | Language Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (desc) | CopilotModelConfigDescription | 可同时保存多个模型配置；保存后，右侧 AI 面板会直接使用这里的模型列表。 | Save multiple model configurations; the AI panel on the right will use this model list directly. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (add) | CopilotAddModel | + 添加模型 | + Add Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (copy) | Copy | 复制 | Copy | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (delete) | Delete | 删除 | Delete | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (hint) | CopilotVendorHint | 先选厂商再添加；右侧仍可继续修改兼容性、地址、模型和系统提示词。 | Select a vendor first, then add; you can continue to modify compatibility, address, model, and system prompt on the right. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (name) | CopilotName | 名称 | Name | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (vendor) | CopilotVendor | 厂商 | Vendor | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (protocol) | CopilotProtocol | 协议 | Protocol | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (model) | CopilotModel | 模型 | Model | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (token) | CopilotMaxToken | 最大 Token | Max Token | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (temp) | CopilotTemperature | 温度 | Temperature | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (rounds) | CopilotToolRounds | 工具轮次 | Tool Rounds | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (prompt) | CopilotSystemPrompt | 系统提示词 | System Prompt | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Text (settings hint) | CopilotSettingsHint | 支持厂商预设、模型预设和手动地址输入；右侧 AI 面板发送前可直接切换模型。 | Supports vendor presets, model presets, and manual address input; the AI panel can switch models before sending. | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (cancel) | Cancel | 取消 | Cancel | Replaced |
| ColorVision | CopilotSettingsWindow.xaml | Content (save) | Save | 保存 | Save | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Title | CopilotExceptionDiagnosis | 异常诊断 | Exception Diagnosis | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Text (header) | CopilotUnhandledException | ColorVision 捕获到未处理异常 | ColorVision Captured an Unhandled Exception | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (details) | CopilotExceptionDetails | 异常详情 | Exception Details | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (logs) | CopilotRecentLogs | 最近日志 | Recent Logs | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (n lines) | CopilotRecentNLines | 最近 N 行 | Recent N Lines | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Text (lines) | CopilotLines | 行 | Lines | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (today) | CopilotTodayLog | 当日日志 | Today's Log | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (refresh) | CopilotRefreshLog | 刷新日志 | Refresh Log | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Header (ai) | CopilotAiRequest | AI 请求 | AI Request | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (copy) | CopilotCopyDetails | 复制详情 | Copy Details | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (ask) | CopilotAskAi | 询问 AI | Ask AI | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (search) | CopilotGoogleSearch | Google 搜索 | Google Search | Replaced |
| ColorVision | CopilotExceptionWindow.xaml | Content (close) | Close | 关闭 | Close | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Title | Input | 输入 | Input | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Content (cancel) | Cancel | 取消 | Cancel | Replaced |
| ColorVision | CopilotTextInputWindow.xaml | Content (ok) | OK | 确定 | OK | Replaced |
| ColorVision | AboutMsg.xaml | Text (ok) | OK | 确定 | OK | Replaced |
| ColorVision | ChangelogWindow.xaml | Content (open) | Open | 打开 | Open | Replaced |
| ColorVision | ChangelogWindow.xaml | Content (default) | OpenWithSystemDefault | 系统默认打开 | Open with System Default | Replaced |

## Remaining Chinese Text (Not Processed)

The following categories of Chinese text were found but intentionally not processed:

1. **Log messages** (log.Info, log.Warn, log.Error) - Internal logging, not user-visible
2. **Code comments** - Developer documentation, not user-visible
3. **AI system prompts** - Internal prompts sent to AI models, not direct UI text
4. **Copilot agent keywords** - Used for NLP matching, not displayed as UI text
5. **Desktop pet feature** - Lower priority feature, can be processed later
6. **Update coordinator** - Internal update logic strings

## ColorVision.UI Project

| Project | File | Resource Key | Chinese | English | 备注 |
|---|---|---|---|---|---|
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Title | 磁盘空间分析 - Treemap Viewer | Disk Space Analyzer - Treemap Viewer | Window Title |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_DriveLabel | 盘符： | Drive: | Label |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_BrowseDirectory | 浏览目录… | Browse... | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_BrowseDirectoryTooltip | 选择要扫描的目录 | Select directory to scan | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Scan | 扫 描 | Scan | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_ScanTooltip | 扫描选定目录并显示树图 | Scan selected directory and display treemap | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Cancel | 取 消 | Cancel | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_CancelTooltip | 中止当前扫描 | Abort current scan | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Save | 保 存 | Save | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_SaveTooltip | 将当前扫描结果保存为 JSON 文件 | Save current scan results as JSON file | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Load | 加 载 | Load | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_LoadTooltip | 从已保存的 JSON 文件加载扫描结果 | Load scan results from saved JSON file | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_Up | ↑ 上级 | ↑ Up | Button Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_UpTooltip | 返回父目录节点 | Return to parent directory node | ToolTip |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_ShowLabels | 显示标签 | Show Labels | CheckBox Content |
| ColorVision.UI | TreemapDemoWindow.xaml | Treemap_NodeCount | 节点数： | Nodes: | Label |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_SelectDirPrompt | 请选择目录并点击"扫描"... | Select a directory and click "Scan"... | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Scanning | 扫描中… | Scanning... | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_ScannedFiles | 已扫描 {0:N0} 个文件… | Scanned {0:N0} files... | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Cancelled | 已取消。 | Cancelled. | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Complete | 完成。共 {0:N0} 个文件。 | Done. {0:N0} files total. | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_ScanError | 扫描出错：{0} | Scan error: {0} | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_SaveDialogTitle | 保存扫描结果 | Save Scan Results | Dialog Title |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Saved | 已保存：{0} | Saved: {0} | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_SaveFailed | 保存失败：{0} | Save failed: {0} | MessageBox |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_LoadDialogTitle | 加载扫描结果 | Load Scan Results | Dialog Title |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_InvalidFormat | 文件格式无效或为空。 | Invalid or empty file format. | MessageBox |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Loaded | 已加载：{0} | Loaded: {0} | Status text |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_LoadFailed | 加载失败：{0} | Load failed: {0} | MessageBox |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_OpenFile | 打开文件 | Open File | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_OpenFolder | 打开文件夹 | Open Folder | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_OpenInExplorer | 在资源管理器中打开 | Open in Explorer | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_ShowInExplorer | 在资源管理器中显示 | Show in Explorer | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_CopyPath | 复制路径 | Copy Path | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_DrillDown | 向下钻取（以此为根） | Drill Down (set as root) | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_GoUp | 返回上级 | Go Up | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_DeleteFile | 删除文件{0} | Delete file {0} | Context menu |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_ConfirmDelete | 确认删除文件：\n{0} | Confirm delete file:\n{0} | MessageBox |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_ConfirmDeleteTitle | 确认删除 | Confirm Delete | MessageBox Title |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_DeleteFailed | 删除失败：{0} | Delete failed: {0} | MessageBox |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Error | 错误 | Error | MessageBox Title |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_Prompt | 提示 | Prompt | MessageBox Title |
| ColorVision.UI | TreemapDemoWindow.xaml.cs | Treemap_SelectValidDir | 请先选择有效的目录。 | Please select a valid directory first. | MessageBox |
| ColorVision.UI | TreemapControl.cs | Treemap_File | 文件 | File | Tooltip type |
| ColorVision.UI | TreemapControl.cs | Treemap_Folder | 文件夹 | Folder | Tooltip type |
| ColorVision.UI | PropertyEditorWindow.xaml | PropEditor_SearchPlaceholder | 搜索属性... | Search properties... | Placeholder |
| ColorVision.UI | PropertyEditorWindow.xaml | PropEditor_ResetToDefault | 恢复到默认 | Reset to Default | Button Content |
| ColorVision.UI | PropertyEditorWindow.xaml | PropEditor_ResetTooltip | 重置到类默认值 | Reset to class default values | ToolTip |
| ColorVision.UI | PropertyEditorWindow.xaml.cs | PropEditor_SortDefault | 默认排序 | Default Sort | ComboBoxItem |
| ColorVision.UI | PropertyEditorWindow.xaml.cs | PropEditor_SortNameAsc | 按名称排序 (升序) | Sort by Name (Ascending) | ComboBoxItem |
| ColorVision.UI | PropertyEditorWindow.xaml.cs | PropEditor_SortNameDesc | 按名称排序 (降序) | Sort by Name (Descending) | ComboBoxItem |
| ColorVision.UI | PropertyEditorWindow.xaml.cs | PropEditor_SortCategoryAsc | 按分类排序 (升序) | Sort by Category (Ascending) | ComboBoxItem |
| ColorVision.UI | PropertyEditorWindow.xaml.cs | PropEditor_SortCategoryDesc | 按分类排序 (降序) | Sort by Category (Descending) | ComboBoxItem |
| ColorVision.UI | JsonPropertyEditorControl.xaml.cs | PropEditor_JsonParseError | JSON 解析错误： | JSON parse error: | Error text |
| ColorVision.UI | JsonPropertyEditorControl.xaml.cs | PropEditor_LoadError | 加载错误： | Load error: | Error text |
| ColorVision.UI | JsonPropertyEditorControl.xaml.cs | PropEditor_NoEditableProperties | 没有可编辑的属性 | No editable properties | TextBlock |
| ColorVision.UI | JsonPropertyEditorControl.xaml.cs | PropEditor_JsonGenerateError | JSON 生成错误： | JSON generation error: | Error text |
| ColorVision.UI | JsonPropertyEditorControl.xaml.cs | PropEditor_ValidationError | 验证失败： | Validation failed: | Error text |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_Title | 编辑列表 | Edit List | Window Title |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_Add | 添加 | Add | Button Content |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_Delete | 删除 | Delete | Button Content |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_DeleteAll | 全部删除 | Delete All | Button Content |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_MoveUp | 上移 | Move Up | Button Content |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_MoveDown | 下移 | Move Down | Button Content |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_Index | 索引 | Index | Column Header |
| ColorVision.UI | ListEditorWindow.xaml | ListEditor_Value | 值 | Value | Column Header |
| ColorVision.UI | ListItemEditorWindow.xaml | ListItemEditor_Title | 编辑项 | Edit Item | Window Title |
| ColorVision.UI | ListItemEditorWindow.xaml | ListItemEditor_EditorType | 编辑器类型: | Editor Type: | Label |
| ColorVision.UI | DictionaryEditorWindow.xaml | DictEditor_Title | 编辑字典 | Edit Dictionary | Window Title |
| ColorVision.UI | DictionaryEditorWindow.xaml | DictEditor_Key | 键 (Key) | Key | Column Header |
| ColorVision.UI | DictionaryEditorWindow.xaml | DictEditor_Value | 值 (Value) | Value | Column Header |
| ColorVision.UI | DictionaryItemEditorWindow.xaml | DictItemEditor_Title | 编辑字典项 | Edit Dictionary Item | Window Title |
| ColorVision.UI | DictionaryItemEditorWindow.xaml | DictItemEditor_Key | 键 (Key) | Key | GroupBox Header |
| ColorVision.UI | DictionaryItemEditorWindow.xaml | DictItemEditor_Value | 值 (Value) | Value | GroupBox Header |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_Count | 当前包含 {0} 个项 | Contains {0} items | TextBlock |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_Empty | 空列表 | Empty list | TextBlock |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_EditList | 编辑列表... | Edit list... | Button Content |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_KeyEmpty | 键不能为空！ | Key cannot be empty! | MessageBox |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_ValidationError | 验证错误 | Validation Error | MessageBox Title |
| ColorVision.UI | DictionaryItemEditorWindow.xaml.cs | DictItemEditor_KeyExists | 键 '{0}' 已存在！ | Key '{0}' already exists! | MessageBox |
| ColorVision.UI | DictionaryEditorWindow.xaml.cs | DictEditor_CollectionCount | 集合: {0} 项 | Collection: {0} items | Display text |
| ColorVision.UI | DictionaryEditorWindow.xaml.cs | DictEditor_ConfirmDeleteSelected | 确定要删除选中的 {0} 项吗？ | Are you sure you want to delete the selected {0} items? | MessageBox |
| ColorVision.UI | DictionaryEditorWindow.xaml.cs | DictEditor_ConfirmDeleteTitle | 确认删除 | Confirm Delete | MessageBox Title |
| ColorVision.UI | DictionaryEditorWindow.xaml.cs | DictEditor_ConfirmDeleteAll | 确定要删除全部 {0} 项吗？ | Are you sure you want to delete all {0} items? | MessageBox |
| ColorVision.UI | DictionaryEditorWindow.xaml.cs | DictEditor_ConfirmDeleteAllTitle | 确认全部删除 | Confirm Delete All | MessageBox Title |
| ColorVision.UI | ConfigHandler.cs | Config_Parameters | 配置相关参数 | Configuration Parameters | DisplayName |
| ColorVision.UI | ConfigHandler.cs | Config_EnableBackup | 是否启用定时备份 | Enable scheduled backup | DisplayName |
| ColorVision.UI | HotKeysSetting.xaml | HotKey_RestoreDefault | 恢复默认 | Restore Default | Button Content |
| ColorVision.UI | HotKeysSetting.xaml | HotKey_SaveHotkey | 保存热键 | Save Hotkey | Button Content |
| ColorVision.UI | HotKeysSetting.xaml | HotKey_LoadHotkey | 加载热键 | Load Hotkey | Button Content |
| ColorVision.UI | LogLocalOutput.xaml | Log_ReverseOrder | 倒序 | Reverse | ToggleButton Content |
| ColorVision.UI | LogLocalOutput.xaml | Log_OpenFolder | 打开文件夹 | Open Folder | Button Content |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_ViewStats | 查看耗时统计 | View Timing Stats | MenuItem Header |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_NoHistory | 暂无历史耗时。 | No timing history. | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_Warmup | 预热: | Warmup: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_NoStableSamples | 稳定样本暂无记录。 | No stable samples recorded yet. | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_WarmupNote | 本次软件启动的首轮样本按预热处理... | First-run samples after startup are treated as warmup... | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_Last | 上次: | Last: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_Average | 平均: | Average: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_Fastest | 最快: | Fastest: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_Slowest | 最慢: | Slowest: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_StableSamples | 稳定样本: | Stable samples: | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_RightClickHint | 右键按钮可打开统计窗口。 | Right-click the button to open stats window. | Tooltip |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_FewSamples | 稳定样本仍然较少... | Stable samples still few... | Trend text |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_StatsRecorded | 耗时统计已记录。 | Timing stats recorded. | Trend text |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_CloseToAverage | 本次与历史平均接近。 | Close to historical average. | Trend text |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_FasterThanAverage | 本次比历史平均快 {0}。 | {0} faster than historical average. | Trend text |
| ColorVision.UI | TimedButtonOperation.cs | TimedOp_SlowerThanAverage | 本次比历史平均慢 {0}。 | {0} slower than historical average. | Trend text |
| ColorVision.UI | UpdateRecoveryService.cs | UpdateRecovery_Restored | 上次更新未完成，已恢复到更新前版本。 | Previous update was incomplete. System has been restored... | MessageBox |
| ColorVision.UI | UpdateRecoveryService.cs | UpdateRecovery_Failed | 上次更新未完成，且自动恢复失败... | Previous update was incomplete and automatic recovery failed... | MessageBox |

## ColorVision.UI.Desktop Project

| Project | File | Resource Key | Chinese | English | 备注 |
|---|---|---|---|---|---|
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml | ConfigManager_Columns | 列数: | Columns: | Label |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml | ConfigManager_Assembly | 程序集 | Assembly | Section header |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml | ConfigManager_ConfigList | 配置列表 | Config List | Section header |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml | ConfigManager_SelectConfig | 选择一个配置查看详情 | Select a config to view details | Placeholder |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_Summary | 共计 {0} 个程序集，{1} 个配置类型 | {0} assemblies, {1} config types | Status bar |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_ConfigPrefix | 配置: {0} | Config: {0} | Label |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_CurrentSelection | 当前选择: {0} | Current: {0} | Label |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_LoadFailed | 无法加载配置属性 | Failed to load config properties | Error text |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_SaveSuccess | 保存成功 | Saved successfully | MessageBox |
| ColorVision.UI.Desktop | ConfigManagerWindow.xaml.cs | ConfigManager_Prompt | 提示 | Prompt | MessageBox Title |
| ColorVision.UI.Desktop | WizardWindow.xaml | Wizard_Configure | 配置 | Configure | Button Content |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_AddTitle | 添加自定义应用 | Add Custom Application | Window Title |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Name | 名称 | Name | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Type | 类型 | Type | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Executable | 可执行文件 | Executable | ComboBoxItem |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_CmdScript | CMD 脚本 | CMD Script | ComboBoxItem |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_PsScript | PowerShell 脚本 | PowerShell Script | ComboBoxItem |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Path | 路径 | Path | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Arguments | 参数 | Arguments | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_WorkDir | 工作目录 | Working Directory | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_Group | 分组 | Group | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml | CustomApp_HintExe | 提示：可执行文件填写 exe 完整路径... | Hint: Enter the full path of the exe file... | Hint text |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml.cs | CustomApp_EditTitle | 编辑自定义应用 | Edit Custom Application | Window Title |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml.cs | CustomApp_Command | 命令 | Command | Label |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml.cs | CustomApp_NameRequired | 请填写应用名称 | Please enter the application name | MessageBox |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml.cs | CustomApp_PathRequired | 请填写路径或命令 | Please enter a path or command | MessageBox |
| ColorVision.UI.Desktop | AddCustomAppWindow.xaml.cs | CustomApp_SelectApp | 选择应用程序 | Select Application | Dialog Title |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml | CustomApp_QuickActions | 快捷操作 | Quick Actions | Section label |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_AddTooltip | 添加自定义应用 | Add Custom Application | Tooltip |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_AddScriptTooltip | 添加快捷脚本 | Add Quick Script | Tooltip |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_Category | 分类 | Category | Label |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_ConfirmDelete | 确定要删除自定义应用 "{0}" 吗？ | Are you sure you want to delete custom application "{0}"? | MessageBox |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_ConfirmDeleteTitle | 确认删除 | Confirm Delete | MessageBox Title |
| ColorVision.UI.Desktop | ThirdPartyAppsWindow.xaml.cs | CustomApp_AddScriptTitle | 添加快捷脚本 | Add Quick Script | Dialog Title |
| ColorVision.UI.Desktop | StartMenuAppProvider.cs | CustomApp_LocalApps | 本机应用 | Local Apps | Group name |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Title | 按钮耗时统计 | Button Timing Stats | Window Title |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_OperationKey | 操作键 | Operation Key | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Start | 开始 | Start | Label |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_End | 结束 | End | Label |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Query | 查询 | Query | Button |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Refresh | 刷新 | Refresh | Button |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_ClearFilter | 清除筛选 | Clear Filter | Button |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_ExportCsv | 导出 CSV | Export CSV | Button |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_DeleteSelected | 删除选中 | Delete Selected | Button/MenuItem |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_ClearAll | 清空全部 | Clear All | Button |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Warmup | 预热 | Warmup | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Last | 上次 | Last | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Average | 平均 | Average | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Fastest | 最快 | Fastest | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_Slowest | 最慢 | Slowest | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_StableSamples | 稳定样本 | Stable | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_WarmupSamples | 预热样本 | Warmup | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml | Stats_LastCompleted | 最近完成 | Last Completed | Column Header |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_Summary | 共 {0} 个操作... | {0} operations... | Status bar |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_ExportSuccess | 已导出 {0} 条耗时统计到... | Exported {0} timing entries to... | MessageBox |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_ConfirmDeleteOne | 确定删除操作 {0} 的耗时统计吗？ | Delete timing stats for operation {0}? | MessageBox |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_ConfirmDeleteSelected | 确定删除当前选中的 {0} 条耗时统计吗？ | Delete {0} selected timing entries? | MessageBox |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_Deleted | 已删除 {0} 条耗时统计。 | Deleted {0} timing entries. | MessageBox |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_ConfirmClearAll | 确定清空全部 {0} 条耗时统计吗？ | Clear all {0} timing entries? | MessageBox |
| ColorVision.UI.Desktop | TimedButtonOperationStatsWindow.xaml.cs | Stats_Cleared | 已清空 {0} 条耗时统计。 | Cleared {0} timing entries. | MessageBox |
| ColorVision.UI.Desktop | MarketplaceWindow.xaml.cs | Marketplace_OpenConfig | 点击打开 {0} 配置界面 | Click to open {0} configuration | Tooltip |
| ColorVision.UI.Desktop | MarketplaceWindow.xaml.cs | Marketplace_CreateShortcut | 创建快捷方式 | Create Shortcut | Button Content |
| ColorVision.UI.Desktop | MenuConfigExport.cs | Config_ExportTitle | 选择导出文件位置 | Select Export File Location | Dialog Title |
| ColorVision.UI.Desktop | MenuConfigImport.cs | Config_ImportTitle | 选择导入文件 | Select Import File | Dialog Title |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_Name | 名称 | Name | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_Group | 分组 | Group | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_Type | 类型 | Type | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_PathOrCmd | 路径/命令 | Path/Command | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_Arguments | 参数 | Arguments | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_WorkDir | 工作目录 | Working Directory | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_Sort | 排序 | Sort | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_ConfigTitle | 自定义应用配置 | Custom App Configuration | ClassName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_ListTitle | 自定义应用列表 | Custom App List | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_DefaultCustomGroup | 默认自定义分组名 | Default Custom Group Name | DisplayName |
| ColorVision.UI.Desktop | CustomAppsConfig.cs | CfgApp_DefaultScriptGroup | 默认脚本分组名 | Default Script Group Name | DisplayName |

## Engine Layer

| Project | File | Resource Key | Chinese | English | 备注 |
|---|---|---|---|---|---|
| ColorVision.Engine | BatchProcessManagerWindow.xaml | Engine_Batch_Title | 流程处理配置 | Process Configuration | Window Title |
| ColorVision.Engine | MessagesListWindow.xaml | Engine_Msg_All | 全部 | All | Button |
| ColorVision.Engine | MySqlToolWindow.xaml | Engine_Mysql_Tool | MySQL工具 | MySQL Tool | Window Title |
| ColorVision.Engine | DatabaseCleanupWindow.xaml | Engine_Mysql_Cleanup | 数据清理 | Data Cleanup | Window Title |
| ColorVision.Engine | ~50 XAML files | Engine_Tpl_*/Engine_POI_* | Various | Various | Template/POI UI |
| ColorVision.Engine | ~21 C# files | Engine_Msg_* | Various | Various | MessageBox messages |
| ColorVision.Engine | ~25 C# files | Engine_PG_* | Various | Various | PropertyGrid attributes |
| ColorVision.Engine | ~8 C# files | Engine_Dlg_* | Various | Various | Dialog titles |

## Plugins

| Project | File | Resource Key | Chinese | English | 备注 |
|---|---|---|---|---|---|
| EventVWR | EventWindow.xaml | EventVWR_Title | 事件查看器 | Event Viewer | Window Title |
| EventVWR | EventVWRPlugins.cs | EventVWR_PluginName | 事件插件 | Event Plugin | Plugin Header |
| EventVWR | DumpConfig.cs | EventVWR_AdminRequired | 操作需要使用管理员权限 | This operation requires admin | MessageBox |
| Conoscope | ~6 C# files | Con_* | Various | Various | PropertyGrid attributes |

## Next Steps

1. ~~Process remaining C# files in ColorVision/ project~~ ✓
2. ~~Process UI/ColorVision.UI/ project~~ ✓
3. ~~Process UI/ColorVision.UI.Desktop/ project~~ ✓
4. ~~Process UI/ColorVision.Themes/ project~~ ✓
5. Process UI/ColorVision.ImageEditor/ project (pending)
6. ~~Process Engine/ColorVision.Engine/ project~~ ✓
7. ~~Process Plugins/EventVWR/~~ ✓
8. Process Plugins/SystemMonitor/ (0 instances - already clean)
9. ~~Process Plugins/Conoscope/~~ ✓
