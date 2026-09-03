---
knowledge_id: "ui.desktop-pet"
knowledge_type: "topic"
status: "current"
summary: "桌面宠物的启用、选择、Codex 创建、本地素材导入、精灵表规格、配置与故障定位；创建结果由设置页限时发现。"
aliases: ["桌面宠物","桌宠","小彩","唤醒宠物","收起宠物","选择宠物与设置","用 Codex 创建","导入精灵表","pet.json","avatar.json","spritesheetPath","DesktopPetConfig","DesktopPetAssetCatalog","DesktopPetPackageService","DesktopPetCodexService","FloatingBallWindow","OpenFloatingBall","hatch-pet","素材不可用"]
code_paths: ["ColorVision/FloatingBall/","ColorVision/FloatingBallWindow.xaml","ColorVision/FloatingBallWindow.xaml.cs","ColorVision/MainWindowConfig.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DesktopPetPackageServiceTests.cs","Test/ColorVision.UI.Tests/DesktopPetCodexServiceTests.cs","Test/ColorVision.Copilot.Tests/DesktopPetCopilotActivityTrackerTests.cs"]
related: ["ui.settings","ui.configuration","copilot.interactions"]
---

# 桌面宠物

桌面宠物是 ColorVision 主程序中的桌面悬浮组件，可选择本地素材、调整外观，并展示 ColorVision Copilot 的任务状态与待确认操作。默认关闭；启用后显示独立的透明窗口，不占用任务栏位置。

## 打开与选择

1. 打开应用设置中的“桌面宠物”页，点击“唤醒宠物”；也可通过“启用桌面宠物”选项控制显示。
2. 在素材列表中点击“选择”，桌面上的宠物随之切换。素材选择会立即保存；选中项显示“已选择”。
3. 使用“外观”滑块调整大小，范围为 65%–145%。需要名称、透明度、通知或待机提示设置时，打开“高级设置”。
4. 点击“收起宠物”隐藏窗口。已显示时，右键“选择宠物与设置”可回到同一设置内容。

选择或导入素材不会自动开启桌宠。素材不存在或无法解码时，显示内置“小彩”；不会因此自动改写保存的 `SelectedPetId`。素材列表中的名称来自素材包，消息标题使用的 `PetName` 独立配置。

| 桌面操作 | 结果 |
| --- | --- |
| 按住左键拖动 | 移动窗口并更新 `FloatingBallWindowConfig` 中的位置；横向拖动使用对应方向的动画 |
| 单击 | 播放点击反馈；存在 Copilot 活动时打开当前优先活动 |
| 双击 | 打开 ColorVision Copilot |
| 右键“Copilot 活动” | 选择要查看的会话活动 |
| 右键“显示主窗口” | 显示并激活 ColorVision 主窗口 |
| 右键“发送测试提醒” | 发送本地测试气泡，受“显示通知”控制 |
| 右键“隐藏桌宠” | 关闭桌宠窗口，保留应用运行 |
| 右键“退出程序” | 退出整个 ColorVision 应用 |

## 创建或导入宠物

### 用 Codex 创建

此入口使用本机 Codex Desktop 的 Hatch Pet 流程。内置“小彩”和本地导入不依赖 Codex。

1. 在桌面宠物页点击“创建”，选择“用 Codex 创建”。
2. 等待本机检测，填写不超过 600 字符的想法；留空时，预填任务会请 Codex 根据已有个人上下文创建。
3. 点击“在 Codex 中创建”。ColorVision 会在需要时把本机随附的 `hatch-pet` 技能复制到 Codex 用户技能目录，并通过 `codex://new?prompt=...` 打开预填任务。
4. 在 Codex 中审阅并发送任务。ColorVision 不自动发送该任务；生成、联网与账户使用发生在 Codex 中。
5. 保持 ColorVision 的宠物设置页打开。新包写入 Codex 的 `pets` 目录并能被读取后，列表会刷新并自动选择新宠物。

Codex 数据根目录优先使用进程环境变量 `CODEX_HOME`，未设置时使用 `%USERPROFILE%\.codex`。技能目标为其下的 `skills\hatch-pet`：已有 `SKILL.md` 时直接复用；目录已存在但缺少 `SKILL.md` 时报告不完整，不覆盖该目录。检测就绪只说明找到了技能或可复制来源，打开 Codex 仍依赖本机协议关联。

自动发现每 4 秒检查一次，最长两小时。设置页卸载时停止计时，同一控件重新加载且未到期时可继续；重新创建窗口或重启应用不承诺恢复等待。它通过新增包目录 ID 发现结果，再从新素材中按图片修改时间选择，未绑定具体 Codex 任务。只修改已有包、创建到其他目录，或同时生成多个包时，应手动“刷新”并核对选择。

### 导入精灵表

1. 在“创建”窗口切换到“导入精灵表”。
2. 填写宠物名称（1–80 字符）、可选描述（最多 240 字符），选择本地 PNG 或 WebP。
3. 按图片布局选择 v2（8 列 × 11 行，默认）或 v1（8 列 × 9 行）。
4. 点击“导入并选择”。通过解码和网格校验后，程序复制图片、生成 `pet.json`，刷新列表并选择新包。

目标目录是 `%APPDATA%\ColorVision\DesktopPets`，可从“自定义宠物 → 打开目录”进入。导入会生成独立子目录；同名导入使用可用的新目录名，不覆盖已有包。图片和清单先写入临时导入目录，再整体移入目标。原始图片保留在原位置。

名称去除首尾空白后计数；导入界面的名称和描述限制不等于手工素材清单也有相同字段校验。图片无法解码或网格不符时，先修正素材，不要只修改版本字段以跳过布局要求。

## 素材来源与发现

| 来源标签 | 读取位置 | 说明 |
| --- | --- | --- |
| ColorVision 内置 | 应用资源中的 `Assets/Pets/xiaocai.png` | 默认静态图 `builtin:xiaocai`，不需要精灵表清单 |
| ColorVision 自定义 | `%APPDATA%\ColorVision\DesktopPets\<包目录>\pet.json` | 手工管理或通过导入创建 |
| Codex 自定义 | `<Codex数据根目录>\pets\<包目录>\pet.json` | 兼容宠物包，也是创建结果自动发现所检查的位置 |
| Codex 自定义 | `<Codex数据根目录>\avatars\<包目录>\avatar.json` | 兼容 avatar 清单；可手动刷新发现 |
| Codex 本机素材 | 可读取的 Codex/ChatGPT 安装资源 `resources\app.asar` | 读取已识别的精灵表命名，不保证任意安装版本都有可用素材 |

自定义扫描只读取上述根目录的直接子目录。首次进入设置使用目录缓存，“刷新”重新扫描并加载预览；外部新增或修改文件后应刷新。Codex 安装资源候选包括运行中的 ChatGPT 程序目录、用户安装的 Codex/ChatGPT 目录和 WindowsApps 中的 `OpenAI.Codex_*` 包。以首个发现有效匹配素材的归档为准；权限、安装布局和素材命名变化都可能导致缺项。

ColorVision 直接读取这些本地文件，不会在“刷新”时下载素材，也不把 Codex 素材复制进 ColorVision 的自定义目录。预览取精灵表左上角第 1 帧；清单可发现但图片解码失败时，该项显示“素材不可用”且不能选择。

## 自定义清单

例如把以下 `pet.json` 与符合 v2 网格的 `spritesheet.png` 放在同一个 `blue-cat` 子目录中，然后刷新：

```json
{
  "id": "blue-cat",
  "displayName": "蓝猫",
  "description": "蓝色像素猫",
  "spriteVersionNumber": 2,
  "spritesheetPath": "spritesheet.png"
}
```

| 字段 | 读取规则 |
| --- | --- |
| `displayName` | 显示名；字段缺失或不是字符串时回退 `id`，再回退文件夹名；空字符串仍保留为空 |
| `id` | 显示名回退值。ColorVision 的选择 ID 由来源前缀和文件夹名构成，不以此字段为唯一标识 |
| `description` | 描述；未提供时使用兼容素材包的默认说明 |
| `spriteVersionNumber` | 整数 `2` 表示 v2；缺省或其他整数按 v1 读取，应明确填写 1 或 2 |
| `spritesheetPath` | 相对包目录的图片路径，默认 `spritesheet.webp`；拒绝绝对路径和规范化后越出包目录的路径 |

字段名区分大小写，使用表中的 camelCase。ColorVision 自定义包的选择 ID 例如 `colorvision-custom:blue-cat`，Codex 包例如 `codex-custom:pets:blue-cat`。重命名包目录会改变选择 ID，原选择可能回退到“小彩”。清单格式错误、图片不存在或文件超限时，该包会被跳过；部分解析异常写入 Trace。

### 精灵表布局

单张图片最大 20 MiB，宽高均须大于 0 且不超过 4096 像素。所有版本均为 8 列；宽度须被 8 整除，高度须被版本行数整除。每帧按等分网格裁切，帧宽和帧高可不同。例如 v2 使用 1024 × 1408 图片时，每帧为 128 × 128。

动画按下表读取各行的前若干帧，行号从 1 开始：

| 行 | 活动 | 使用帧数 |
| --- | --- | ---: |
| 1 | 待机 Idle | 6 |
| 2 | 向右 RunningRight | 8 |
| 3 | 向左 RunningLeft | 8 |
| 4 | 问候 Waving | 4 |
| 5 | 点击/跳跃 Jumping | 5 |
| 6 | 失败 Failed | 8 |
| 7 | 等待 Waiting | 6 |
| 8 | 运行 Running | 6 |
| 9 | 完成/查看 Review | 6 |
| 10–11 | v2 的额外网格行 | 当前动画计划未使用 |

v1 为前 9 行，v2 必须提供完整 11 行。解码器只检查尺寸和网格，不验证每帧是否画出了正确动作；空白帧、动作顺序与透明背景效果需要预览确认。

## 配置与保存

显示开关位于 `MainWindowConfig.OpenFloatingBall`，默认 `false`；窗口位置位于 `FloatingBallWindowConfig`。其他设置属于 `DesktopPetConfig`：

| 配置项 | 默认值 | 含义 |
| --- | --- | --- |
| `PetName` | `小彩` | 问候和部分提示使用的名称，独立于素材显示名 |
| `SelectedPetId` | `builtin:xiaocai` | 素材选择 ID，通过列表修改 |
| `AlwaysOnTop` | `true` | 桌宠窗口置顶 |
| `PetScale` | `1.0` | 外观滑块 0.65–1.45，步长 0.05 |
| `PetOpacity` | `1.0` | 高级设置中的透明度，窗口处理变更时限制为 0.35–1 |
| `ShowNotifications` | `true` | 接收普通气泡，并参与 Copilot 确认卡显示门禁 |
| `ShowStartupGreeting` | `true` | 启动问候，仍需桌宠和通知启用 |
| `EnableIdleTips` | `true` | 周期性待机提示，仍受通知开关控制 |
| `IdleTipIntervalMinutes` | `30` | 计时器使用 5–240 分钟范围内的值 |
| `MessageDisplaySeconds` | `6` | 普通气泡使用 2–20 秒范围内的值；确认卡不按此时长自动批准或拒绝 |
| `EnableCopilotIntegration` | `true` | 跟随 ColorVision Copilot 活动 |
| `ShowCopilotNotifications` | `true` | 等待确认、完成和失败的提醒 |

选择素材立即调用 `Save<DesktopPetConfig>()`；设置页卸载以及高级设置提交时也保存该配置。其余配置文件路径、应用整体保存和重载语义见[配置持久化](./configuration.md)。设置绑定活对象，关闭窗口不撤销已应用的值，详见[设置契约](./settings.md)。

## Copilot 状态与确认卡

“跟随 Copilot 任务状态”控制 ColorVision 内部任务投影，与外部 Codex 创建素材的任务独立。需要确认卡时，同时启用“显示通知”和“显示等待确认、完成和失败提醒”；关闭气泡不等于停止 Copilot 任务。

确认卡显示操作、工具、来源、任务/工作区、影响、撤销信息与到期时间。“查看”打开 Copilot，“拒绝”提交拒绝决定，“批准”先展示原生确认内容，再进入同一审批与上下文复查。它不因桌宠动画或窗口出现而自动执行操作。多会话优先级、活动保留和审批契约统一见[Copilot 活动呈现](../../02-developer-guide/core-concepts/copilot-local-interactions.md#消息显示与桌宠活动)。

## 故障定位

| 现象 | 检查与处理 |
| --- | --- |
| 列表已选择，桌面没有宠物 | 点击“唤醒宠物”，检查 `OpenFloatingBall`；选择本身不负责显示窗口 |
| 只能看到“小彩” | 先刷新，检查自定义根目录/直接子目录/清单名；Codex 本机素材另检查归档可读性 |
| “素材不可用”或切换后仍是“小彩” | 检查图片存在、20 MiB 限制、解码、版本和网格；运行日志可能有“桌面宠物素材加载失败，已回退到默认素材” |
| “素材刷新失败” | 查看具体异常；检查目录访问权限和 `CODEX_HOME` 是否能解析为有效路径 |
| “hatch-pet 技能目录不完整” | 到 Codex 技能页处理现有不完整目录，或改用本地导入；ColorVision 不覆盖修复已有目录 |
| “无法打开 Codex” | 查看异常，核对本机 Codex 安装与 `codex://` 协议关联；检测就绪不证明应用已打开 |
| Codex 已打开，但没有开始生成 | 切到 Codex，审阅并发送预填任务 |
| Codex 已生成，但未自动选择 | 检查 `pets\<新目录>\pet.json` 和图片是否完整、是否仍在两小时等待内；手动刷新并选择，可处理已有包更新 |
| 没有提醒或确认卡 | 检查显示开关、通知开关和 Copilot 联动；确认卡还要求有当前可见的待确认操作 |

## 实现与验证

`DesktopPetService` 管理窗口和 Copilot 桥接；`DesktopPetSettingsControl` 管理选择、导入后的刷新和创建等待；`DesktopPetAssetCatalog` 发现清单/归档；`DesktopPetPackageService` 导入本地包；`DesktopPetCodexService` 检测技能并打开预填任务；`DesktopPetSpriteSheet` 负责解码、裁帧和动画计划。

已有测试声明覆盖本地导入清单、重名不覆盖、无效网格、临时目录清理范围，以及 Codex 提示词、深链接编码、技能复制/保留、不完整目录和包 ID 快照。快照测试只证明存在清单文件即可列出 ID，不证明图片可用；实际选择还要经过目录解析和图片解码。Copilot Tracker 测试覆盖多会话活动优先级与终态映射。

这些测试不替代真实桌面拖动、主题/DPI 布局、本机 Codex 启动、生成完成或计时发现的端到端验证。上述入口便于查证覆盖范围，测试路径存在不代表本次已经执行。
