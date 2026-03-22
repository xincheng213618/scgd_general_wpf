# ProjectARVRPro ProcessGroup 设计文档

> **版本**: v0.1 (草案)  
> **日期**: 2026-03-17  
> **状态**: 待确认 — 确认后再实施代码变更  

---

## 1. 需求概述

### 1.1 当前现状

ProjectARVRPro 目前采用**外部触发**模式执行流程:

```
外部系统 → Socket("ProjectARVRInit") → 初始化测试
外部系统 → Socket("SwitchPGCompleted") → 执行下一个启用的 ProcessMeta
         → Socket 返回 "SwitchPG" {ARVRTestType: nextIndex}
外部系统 → 切图后 → Socket("SwitchPGCompleted") → 继续...
         → 所有步骤完成 → Socket 返回 "ProjectARVRResult"
```

所有 ProcessMeta 以扁平列表形式排列，按顺序逐一执行启用的项。

### 1.2 新增需求

| # | 需求 | 说明 |
|---|------|------|
| 1 | **一键执行** | 增加按钮，一键执行所有已配置且启用的流程并解析结果 |
| 2 | **步间通信触发** | 两个流程之间可插入串口或 Socket 通信指令，触发外部设备切图后再执行下一步 |
| 3 | **ProcessGroup（组）** | 在流程中增加"组"概念，用户可切换不同的组来执行，实现多产品/多配置方案管理 |

### 1.3 约束

- 现有代码已在实际生产中运行，**必须保持向后兼容**
- 外部 Socket 协议需保持兼容（新增事件可以，不能改变已有事件的语义）
- ProcessMetas.json 持久化格式需平滑升级

---

## 2. 总体架构

### 2.1 概念模型

```
ProcessManager
  └── ProcessGroups (ObservableCollection<ProcessGroup>)
        ├── Group "产品A" (ActiveGroup ★)
        │     ├── ProcessMeta[0]: White255 + White255Process ✓
        │     ├── ProcessMeta[1]: W51 + White51Process ✓
        │     ├── ProcessMeta[2]: Chessboard + ChessboardProcess ✓
        │     └── ProcessMeta[3]: MTFHV + MTFHVProcess ✓
        │
        ├── Group "产品B"
        │     ├── ProcessMeta[0]: White255 + White255Process ✓
        │     ├── ProcessMeta[1]: Distortion + DistortionProcess ✓
        │     └── ProcessMeta[2]: OpticCenter + OpticCenterProcess ✓
        │
        └── Group "调试"
              └── ProcessMeta[0]: Black + BlackProcess ✓
```

- 一个 **ProcessGroup** 是一组有序的 ProcessMeta
- **ActiveGroup** 是当前选中执行的组
- 切换 ActiveGroup 即切换整套执行方案

### 2.2 向后兼容

现有的 `ProcessMetas` 扁平列表将作为 **"默认组(Default)"** 自动导入:

```
升级路径:
  ProcessMetas.json (旧格式: List<ProcessMetaPersist>)
    → 检测到旧格式 → 包装为 { Groups: [{ Name: "Default", Metas: [...] }], ActiveGroupIndex: 0 }
    → 保存为新格式 ProcessGroups.json
```

---

## 3. 数据模型设计

### 3.1 ProcessGroup 类（新增）

```csharp
/// <summary>
/// 代表一组有序的流程配置，用于不同产品/场景的测试方案切换。
/// </summary>
public class ProcessGroup : ViewModelBase
{
    /// <summary>
    /// 组名（唯一标识）
    /// </summary>
    public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
    private string _Name;

    /// <summary>
    /// 组内的流程列表
    /// </summary>
    public ObservableCollection<ProcessMeta> ProcessMetas { get; set; } = new();
}
```

### 3.2 ProcessGroupPersist 类（新增）

```csharp
internal class ProcessGroupPersist
{
    public string Name { get; set; }
    public List<ProcessMetaPersist> Metas { get; set; } = new();
}

internal class ProcessGroupsRoot
{
    public int Version { get; set; } = 1;
    public int ActiveGroupIndex { get; set; }
    public List<ProcessGroupPersist> Groups { get; set; } = new();
}
```

### 3.3 ProcessManager 变更

```csharp
public class ProcessManager : ViewModelBase
{
    // ---- 新增 ----
    
    /// <summary>
    /// 所有流程组
    /// </summary>
    public ObservableCollection<ProcessGroup> ProcessGroups { get; } = new();

    /// <summary>
    /// 当前激活的组索引
    /// </summary>
    public int ActiveGroupIndex { get; set; }

    /// <summary>
    /// 当前激活组（快捷属性）
    /// </summary>
    [JsonIgnore]
    public ProcessGroup ActiveGroup => (ActiveGroupIndex >= 0 && ActiveGroupIndex < ProcessGroups.Count) 
        ? ProcessGroups[ActiveGroupIndex] : null;

    // ---- 保留（指向 ActiveGroup 的 ProcessMetas，保持兼容）----
    
    /// <summary>
    /// 当前组的 ProcessMetas（兼容属性，与 ActiveGroup.ProcessMetas 同步）
    /// </summary>
    public ObservableCollection<ProcessMeta> ProcessMetas => ActiveGroup?.ProcessMetas ?? _emptyMetas;
    private static readonly ObservableCollection<ProcessMeta> _emptyMetas = new();

    // ---- 新增命令 ----
    
    public RelayCommand AddGroupCommand { get; set; }      // 添加组
    public RelayCommand RemoveGroupCommand { get; set; }    // 删除组
    public RelayCommand RenameGroupCommand { get; set; }    // 重命名组
    public RelayCommand DuplicateGroupCommand { get; set; } // 复制组
}
```

### 3.4 ProcessMeta 变更

ProcessMeta **无需改变**，保持现有结构不变。

### 3.5 持久化文件

旧文件: `ProcessMetas.json` (保留不删除，作为备份)  
新文件: `ProcessGroups.json`

```json
{
  "Version": 1,
  "ActiveGroupIndex": 0,
  "Groups": [
    {
      "Name": "产品A",
      "Metas": [
        {
          "Name": "White255",
          "FlowTemplate": "White255",
          "ProcessTypeFullName": "ProjectARVRPro.Process.W255.White255Process",
          "IsEnabled": true,
          "ConfigJson": "{...}"
        },
        ...
      ]
    },
    {
      "Name": "产品B",
      "Metas": [...]
    }
  ]
}
```

---

## 4. 一键执行功能设计

### 4.1 执行模式

新增 **"一键执行"** 模式，区别于外部触发模式:

| 模式 | 触发方式 | PG切换 | 说明 |
|------|---------|--------|------|
| **外部触发**（现有） | Socket `SwitchPGCompleted` | 外部系统切换 | 生产环境，与PG设备联动 |
| **一键执行**（新增） | UI 按钮 / Socket `RunAll` | 自动发送切图指令或直接执行 | 调试/简化操作 |

### 4.2 一键执行流程

```
用户点击 "一键执行" 按钮
    │
    ├─ 1. InitTest(SN) — 初始化测试
    │
    ├─ 2. FOR EACH 启用的 ProcessMeta in ActiveGroup:
    │       │
    │       ├─ a. 如果有步间通信指令 (InterStepAction):
    │       │       └─ 发送串口/Socket命令 → 等待应答/超时
    │       │
    │       ├─ b. 设置 FlowTemplate
    │       ├─ c. RunTemplate() → 等待完成
    │       ├─ d. Processing() → 解析结果
    │       └─ e. 继续下一个
    │
    └─ 3. TestCompleted() — 汇总并输出结果
```

### 4.3 ARVRWindow 新增方法

```csharp
/// <summary>
/// 一键执行当前组的所有启用的 ProcessMeta
/// </summary>
public async Task RunAllAsync()
{
    if (flowControl.IsFlowRun) return;
    
    InitTest(ProjectARVRProConfig.Instance.SN);
    
    var enabledMetas = ProcessMetas.Where(m => m.IsEnabled).ToList();
    
    for (int i = 0; i < enabledMetas.Count; i++)
    {
        ProcessMeta meta = enabledMetas[i];
        CurrentTestType = ProcessMetas.IndexOf(meta);
        ProjectConfig.StepIndex = CurrentTestType;
        
        // 执行步间通信指令（如有配置）
        if (meta.InterStepAction != null && meta.InterStepAction.IsEnabled)
        {
            bool actionResult = await ExecuteInterStepAction(meta.InterStepAction);
            if (!actionResult)
            {
                log.Error($"步间通信指令执行失败: {meta.Name}");
                break;
            }
        }
        
        // 设置流程模板并执行
        FlowTemplate.SelectedValue = TemplateFlow.Params
            .First(a => a.Key.Contains(meta.FlowTemplate)).Value;
        
        await RunTemplateAndWaitAsync();
        // RunTemplateAndWaitAsync 内部等待 FlowCompleted 事件
    }
}
```

### 4.4 UI 变更

在 `ARVRWindow.xaml` 的 StatusBar 中添加一键执行按钮:

```xml
<StatusBarItem>
    <Button Content="一键执行" Command="{Binding RunAllCommand}" Background="LightGreen"/>
</StatusBarItem>
```

或在顶部工具栏位置添加更显眼的按钮:

```xml
<Button Content="▶ 一键执行全部" DockPanel.Dock="Right" 
        Background="#FF4CAF50" Foreground="White" FontWeight="Bold"
        Click="RunAllClick" Margin="5,0,0,0" Padding="10,2"/>
```

---

## 5. 步间通信指令设计

### 5.1 InterStepAction 模型

```csharp
/// <summary>
/// 步间通信动作 — 在执行下一个流程之前发送的通信指令
/// </summary>
public class InterStepAction : ViewModelBase
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// 通信类型
    /// </summary>
    public InterStepActionType ActionType { get; set; } = InterStepActionType.None;

    /// <summary>
    /// 发送的指令内容（文本/十六进制）
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    /// 期望的应答内容（空则不等待应答）
    /// </summary>
    public string ExpectedResponse { get; set; }

    /// <summary>
    /// 超时时间（毫秒）
    /// </summary>
    public int TimeoutMs { get; set; } = 5000;

    /// <summary>
    /// Socket 目标地址（当 ActionType 为 Socket 时使用）
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// Socket 端口
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 串口名称（当 ActionType 为 SerialPort 时使用）
    /// </summary>
    public string SerialPortName { get; set; }

    /// <summary>
    /// 串口波特率
    /// </summary>
    public int BaudRate { get; set; } = 9600;
}

public enum InterStepActionType
{
    None,           // 无动作
    Socket,         // Socket 通信
    SerialPort,     // 串口通信
    SwitchPG,       // 通过现有 SwitchPG 协议切图（兼容现有模式）
    Delay           // 简单延时
}
```

### 5.2 InterStepAction 在 ProcessMeta 中的位置

```csharp
public class ProcessMeta : ViewModelBase
{
    // ... 现有属性不变 ...

    /// <summary>
    /// 执行此流程前的步间通信指令（可选）
    /// </summary>
    public InterStepAction InterStepAction { get; set; }
}
```

### 5.3 InterStepAction 持久化

在 ProcessMetaPersist 中增加:

```csharp
internal class ProcessMetaPersist
{
    // ... 现有字段 ...
    public InterStepAction InterStepAction { get; set; }
}
```

### 5.4 步间通信执行器

```csharp
/// <summary>
/// 执行步间通信指令
/// </summary>
private async Task<bool> ExecuteInterStepAction(InterStepAction action)
{
    switch (action.ActionType)
    {
        case InterStepActionType.Socket:
            return await ExecuteSocketAction(action);
        
        case InterStepActionType.SerialPort:
            return await ExecuteSerialPortAction(action);
        
        case InterStepActionType.SwitchPG:
            // 复用现有的 SwitchPG 逻辑
            SwitchPG();
            // 等待 SwitchPGCompleted 事件
            return await WaitForSwitchPGCompleted(action.TimeoutMs);
        
        case InterStepActionType.Delay:
            await Task.Delay(action.TimeoutMs);
            return true;
        
        default:
            return true;
    }
}
```

---

## 6. ProcessGroup UI 设计

### 6.1 ProcessManagerWindow 变更

在 ProcessManagerWindow 的顶部增加组管理区域:

```
┌────────────────────────────────────────────────────────────┐
│ 流程处理配置                                                  │
├────────────────────────────────────────────────────────────┤
│ 组管理:                                                      │
│  [产品A ▼]  [+添加组] [复制组] [删除组] [重命名]               │
├────────────────────────────────────────────────────────────┤
│ ┌─当前组: 产品A──────────────────────┐ ┌──创建/更新──────┐ │
│ │ 名称    │ 流程模板  │ 处理类    │启用│ │  名称: ___     │ │
│ │ White255│ White255 │ W255Proc │ ☑ │ │  模板: [___▼]  │ │
│ │ W51     │ W51      │ W51Proc  │ ☑ │ │  处理: [___▼]  │ │
│ │ Chess   │ Chess    │ ChesProc │ ☑ │ │  [添加]        │ │
│ │ MTFHV   │ MTFHV    │ MTFProc  │ ☑ │ │                │ │
│ └─────────────────────────────────────┘ │  步间指令: [编辑│ │
│                                          └────────────────┘ │
│ ┌Recipe配置────┐ ┌Fix配置────────┐ ┌Process配置─────┐     │
│ │  ...          │ │  ...          │ │  ...            │     │
│ └───────────────┘ └───────────────┘ └─────────────────┘     │
└────────────────────────────────────────────────────────────┘
```

### 6.2 ARVRWindow 组切换

在主窗口 StatusBar 或工具栏增加组选择器:

```xml
<!-- StatusBar 中增加 -->
<StatusBarItem>
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="当前组:" VerticalAlignment="Center" Margin="0,0,5,0"/>
        <ComboBox ItemsSource="{Binding ProcessManager.ProcessGroups}" 
                  DisplayMemberPath="Name"
                  SelectedIndex="{Binding ProcessManager.ActiveGroupIndex}"
                  Width="120"/>
    </StackPanel>
</StatusBarItem>
<StatusBarItem>
    <Button Content="▶ 一键执行" Command="{Binding RunAllCommand}" 
            Background="LightGreen" FontWeight="Bold"/>
</StatusBarItem>
```

### 6.3 StepBar 同步

切换组时自动重新生成 StepBar:

```csharp
// ProcessManager.ActiveGroupIndex setter 中
set 
{ 
    _ActiveGroupIndex = value; 
    OnPropertyChanged();
    OnPropertyChanged(nameof(ActiveGroup));
    OnPropertyChanged(nameof(ProcessMetas));
    ActiveGroupChanged?.Invoke(this, EventArgs.Empty);
}

// ARVRWindow 中监听
ProcessManager.ActiveGroupChanged += (s, e) => 
{
    ProcessManager.GenStepBar(stepBar);
};
```

---

## 7. Socket 协议扩展

### 7.1 新增事件

| 事件名 | 方向 | 说明 |
|--------|------|------|
| `SwitchGroup` | 外部 → 系统 | 切换到指定组 |
| `RunAll` | 外部 → 系统 | 一键执行当前组所有流程 |
| `GetGroups` | 外部 → 系统 | 查询所有组信息 |

### 7.2 SwitchGroup 协议

```json
// 请求
{
  "EventName": "SwitchGroup",
  "Data": { "GroupName": "产品B" }
}

// 响应
{
  "EventName": "SwitchGroup",
  "Code": 0,
  "Msg": "Switched to 产品B",
  "Data": { "GroupName": "产品B", "MetaCount": 3 }
}
```

### 7.3 RunAll 协议

```json
// 请求
{
  "EventName": "RunAll",
  "SerialNumber": "SN12345"
}

// 响应 (每个流程完成时发送进度)
{
  "EventName": "RunAllProgress",
  "Code": 0,
  "Data": { "Current": 1, "Total": 4, "MetaName": "White255", "Result": true }
}

// 最终响应
{
  "EventName": "ProjectARVRResult",
  "Code": 0,
  "Data": { ... ObjectiveTestResult ... }
}
```

### 7.4 现有协议兼容

| 现有事件 | 变更 | 说明 |
|---------|------|------|
| `ProjectARVRInit` | **无变更** | 继续使用 ActiveGroup 的 ProcessMetas |
| `SwitchPGCompleted` | **无变更** | 继续在 ActiveGroup 内推进 |
| `ProjectARVRResult` | **无变更** | 返回格式不变 |

---

## 8. 文件变更清单

### 8.1 新增文件

| 文件 | 说明 |
|------|------|
| `Process/ProcessGroup.cs` | ProcessGroup 类定义 |
| `Process/ProcessGroupPersist.cs` | 持久化模型 |
| `Process/InterStepAction.cs` | 步间通信指令模型 |
| `Process/InterStepActionExecutor.cs` | 步间通信执行器 |
| `Services/SwitchGroupSocket.cs` | 组切换 Socket 处理器 |
| `Services/RunAllSocket.cs` | 一键执行 Socket 处理器 |

### 8.2 修改文件

| 文件 | 变更内容 |
|------|---------|
| `Process/ProcessManager.cs` | 增加 ProcessGroups 管理、组切换逻辑、新持久化逻辑 |
| `Process/ProcessMeta.cs` | 增加 InterStepAction 属性 |
| `Process/ProcessMetaPersist.cs` | 增加 InterStepAction 字段 |
| `Process/ProcessManagerWindow.xaml` | 增加组管理 UI |
| `Process/ProcessManagerWindow.xaml.cs` | 增加组管理交互逻辑 |
| `ARVRWindow.xaml` | 增加一键执行按钮、组选择器 |
| `ARVRWindow.xaml.cs` | 增加 RunAllAsync()、组切换逻辑 |
| `ProjectARVRProConfig.cs` | 增加 RunAllCommand |

### 8.3 不修改的文件

| 文件 | 原因 |
|------|------|
| `Process/IProcess.cs` | 接口不变 |
| `Process/*/Process.cs` | 各处理模块不变 |
| `Services/SocketControl.cs` 中现有处理器 | 保持兼容 |
| `Recipe/`, `Fix/` | 不涉及 |

---

## 9. 实施步骤

建议按以下阶段分步实施:

### 阶段一: ProcessGroup 基础（最小改动）

1. 新增 `ProcessGroup.cs` 和 `ProcessGroupPersist.cs`
2. 修改 `ProcessManager` 支持多组管理
3. 实现旧格式自动迁移
4. 修改 `ProcessManagerWindow` 增加组管理 UI
5. 修改 `ARVRWindow` 增加组选择器

**验证**: 旧配置能正常加载为"Default"组，组切换功能正常

### 阶段二: 一键执行

1. 在 `ARVRWindow` 中添加 `RunAllAsync()` 方法
2. 添加 UI 按钮
3. 处理异步流程等待和错误处理

**验证**: 点击一键执行能逐一执行所有启用的流程

### 阶段三: 步间通信指令

1. 新增 `InterStepAction.cs` 和执行器
2. 修改 `ProcessMeta` 增加 InterStepAction 属性
3. 修改 ProcessManagerWindow 增加步间指令编辑入口
4. 在 `RunAllAsync()` 中集成步间通信

**验证**: 配置 Socket/串口指令后，一键执行时能在步骤间发送通信指令

### 阶段四: Socket 协议扩展

1. 新增 `SwitchGroup` 和 `RunAll` Socket 处理器
2. 更新文档

**验证**: 外部系统能通过 Socket 切换组和触发一键执行

---

## 10. 风险评估

| 风险 | 影响 | 缓解措施 |
|------|------|---------|
| 旧 ProcessMetas.json 格式不兼容 | 升级后丢失配置 | 自动迁移 + 保留旧文件备份 |
| 一键执行时流程超时/异常 | 卡在中间步骤 | 超时机制 + 取消按钮 + 错误跳过选项 |
| 组切换时流程正在执行 | 状态混乱 | 执行中禁止切换组，UI 锁定 |
| 步间通信目标不可达 | 流程中断 | 超时设置 + 重试 + 可选跳过 |
| 多组配置导致持久化文件膨胀 | 性能影响微弱 | 可忽略，JSON 格式高效 |

---

## 11. 实施注意事项

以下要点需在编码实施时注意:

1. **属性变更通知**: 所有 setter 中应先检查值是否变化（`if (_field != value)`），避免冗余通知
2. **ProcessMetas 空安全**: `ActiveGroup` 为 null 时 `ProcessMetas` 返回的空集合应为只读或每次新建，防止误修改
3. **一键执行错误处理**: `RunAllAsync` 的每个步骤都需要完整的错误处理（超时、异常、取消），并在日志中包含流程名、步骤名、动作类型等诊断信息
4. **RunTemplateAndWaitAsync**: 需要基于 `TaskCompletionSource` 实现，将 `FlowCompleted` 事件转换为 async/await 模式，包含超时处理
5. **WaitForSwitchPGCompleted**: 同样使用 `TaskCompletionSource`，监听 `SwitchPGCompleted` 调用并转换为 awaitable
6. **ActiveGroupIndex 边界检查**: setter 中需验证索引范围有效性
7. **执行中锁定**: 流程执行期间禁止切换组，UI 上要有明确的禁用状态反馈

## 12. 开放问题

以下问题需要在确认设计时决定:

1. **组复制**: 复制组时是否深拷贝 ProcessConfig？还是共享？
   - 建议: **深拷贝**，保持每组完全独立

2. **InterStepAction 粒度**: 步间指令是绑定在 ProcessMeta 上(每个步骤前执行)，还是绑定在两个步骤之间？
   - 建议: 绑定在 **ProcessMeta 上**（作为"执行此步骤前的前置动作"），简单直观

3. **一键执行时的 SwitchPG**: 一键执行模式下是否还需要通过 Socket 发送 SwitchPG？
   - 选项 A: 完全内部执行，不发送 SwitchPG（纯软件模式）
   - 选项 B: 仍然发送 SwitchPG 但自动等待应答（半自动模式）
   - 选项 C: 由 InterStepAction 配置决定（灵活模式）
   - 建议: **选项 C**，最灵活

4. **组名唯一性**: 是否强制组名唯一？
   - 建议: **是**，避免混淆

5. **默认组保护**: Default 组是否允许删除？
   - 建议: **允许**，但至少保留一个组

---

## 13. 附录: 类图

```
ProcessManager (Singleton)
├── ProcessGroups: ObservableCollection<ProcessGroup>
│     ├── ProcessGroup
│     │     ├── Name: string
│     │     └── ProcessMetas: ObservableCollection<ProcessMeta>
│     │           ├── ProcessMeta
│     │           │     ├── Name: string
│     │           │     ├── FlowTemplate: string
│     │           │     ├── Process: IProcess
│     │           │     ├── IsEnabled: bool
│     │           │     ├── ConfigJson: string
│     │           │     └── InterStepAction: InterStepAction  ← 新增
│     │           │           ├── IsEnabled: bool
│     │           │           ├── ActionType: InterStepActionType
│     │           │           ├── Command: string
│     │           │           ├── ExpectedResponse: string
│     │           │           ├── TimeoutMs: int
│     │           │           ├── Host/Port (Socket)
│     │           │           └── SerialPortName/BaudRate (串口)
│     │           └── ...
│     └── ...
├── ActiveGroupIndex: int
├── ActiveGroup: ProcessGroup (computed)
├── Processes: ObservableCollection<IProcess> (可用处理类)
└── Commands (AddGroup, RemoveGroup, etc.)
```

---

**请确认以上设计，确认无误后即可开始实施代码变更。**

如有任何需要调整的部分，请告知具体意见。
