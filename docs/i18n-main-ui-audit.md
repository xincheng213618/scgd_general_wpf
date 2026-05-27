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

## Next Steps

1. Process remaining C# files in ColorVision/ project
2. Process UI/ColorVision.UI/ project
3. Process UI/ColorVision.UI.Desktop/ project
4. Process UI/ColorVision.Themes/ project
5. Process UI/ColorVision.ImageEditor/ project
6. Process Engine/ColorVision.Engine/ project
