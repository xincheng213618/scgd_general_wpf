# ProcessMeta IsEnabled 功能说明

## 概述

此功能为 `ProcessMeta` 添加了 `IsEnabled` 属性，允许选择性执行测试步骤。当 ProcessMeta 被禁用时，它将在测试执行流程中被跳过。

## 属性说明

| 属性 | 类型 | 默认值 | 说明 |
|-----|------|-------|------|
| IsEnabled | bool | true | 控制该步骤是否参与执行 |

- **true**: ProcessMeta 包含在执行流程中
- **false**: ProcessMeta 在执行时被跳过

## 执行行为

### 示例场景

假设配置了 8 个 ProcessMetas（索引 0-7），仅启用索引 0 和 7：

```csharp
ProcessMetas[0].IsEnabled = true   // 启用
ProcessMetas[1].IsEnabled = false  // 禁用
ProcessMetas[2].IsEnabled = false  // 禁用
ProcessMetas[3].IsEnabled = false  // 禁用
ProcessMetas[4].IsEnabled = false  // 禁用
ProcessMetas[5].IsEnabled = false  // 禁用
ProcessMetas[6].IsEnabled = false  // 禁用
ProcessMetas[7].IsEnabled = true   // 启用
```

### 执行流程图

```
┌─────────────────┐
│   FlowInit      │  初始化
│  (SocketControl)│
└────────┬────────┘
         │ 查找第一个启用的ProcessMeta
         ▼
┌─────────────────┐
│ ProcessMeta[0]  │  执行步骤0
│   (IsEnabled)   │
└────────┬────────┘
         │ SwitchPGCompleted
         │ 跳过1-6，查找下一个启用的步骤
         ▼
┌─────────────────┐
│ ProcessMeta[7]  │  执行步骤7
│   (IsEnabled)   │
└────────┬────────┘
         │ IsTestTypeCompleted
         │ 检查是否还有启用的步骤
         ▼
┌─────────────────┐
│  TestCompleted  │  测试完成
│   发送结果      │
└─────────────────┘
```

### 详细执行流程

1. **初始化阶段** (`FlowInit` in SocketControl):
   - 系统查找第一个启用的 ProcessMeta（索引 0）
   - 发送 `ARVRTestType = 0` 来切换 PG

2. **步骤0完成后** (`SwitchPGCompleted` in ARVRWindow):
   - 系统从 `CurrentTestType + 1` 开始搜索下一个启用的 ProcessMeta
   - 找到索引 7 的 ProcessMeta（跳过 1-6）
   - 执行索引 7 的 ProcessMeta

3. **步骤7完成后** (`IsTestTypeCompleted` in ARVRWindow):
   - 系统检查索引 7 之后是否还有启用的 ProcessMetas
   - 未找到更多启用的 ProcessMetas
   - 返回 `true`，表示测试完成

4. **测试完成** (`TestCompleted` in ARVRWindow):
   - 发送最终测试结果

## 实现细节

### 修改的方法

#### ARVRWindow.cs

| 方法 | 功能 |
|-----|------|
| `SwitchPGCompleted()` | 从 `CurrentTestType + 1` 开始搜索下一个启用的 ProcessMeta |
| `IsTestTypeCompleted()` | 检查当前步骤之后是否还有启用的 ProcessMetas |
| `SwitchPG()` | 发送下一个启用的 ProcessMeta 索引，如果没有则发送 `-1` |

#### SocketControl.cs

| 方法 | 功能 |
|-----|------|
| `FlowInit.Handle()` | 初始化时查找第一个启用的 ProcessMeta（不再硬编码为 0） |

### UI 变更

- ProcessManagerWindow 现在显示"是否启用"列
- 用户可以通过复选框切换每个 ProcessMeta 的启用/禁用状态
- 更改会自动持久化到 `ProcessMetas.json`

## 持久化

`IsEnabled` 状态通过以下方法保存和加载：

```csharp
// 保存
ProcessManager.SavePersistedMetas()

// 加载
ProcessManager.LoadPersistedMetas()
```

### ProcessMetas.json 格式

```json
[
  {
    "Name": "White255",
    "FlowTemplate": "White255_Template",
    "ProcessTypeFullName": "ProjectARVRPro.Process.W255.White255Process",
    "IsEnabled": true,
    "ConfigJson": "{...}"
  },
  {
    "Name": "MTFHV",
    "FlowTemplate": "MTFHV_Template",
    "ProcessTypeFullName": "ProjectARVRPro.Process.MTFHV.MTFHVProcess",
    "IsEnabled": false,
    "ConfigJson": "{...}"
  }
]
```

## 注意事项

- 默认值为 `true` 以保持向后兼容性
- JSON 中没有 `IsEnabled` 的现有 ProcessMetas 将默认为启用
- UI 复选框的更改会立即持久化
- 配置独立性：每个 ProcessMeta 拥有独立的 Process 实例和 Config 配置

## 相关文件

- `Process/ProcessMeta.cs` - ProcessMeta 类定义
- `Process/ProcessManager.cs` - 流程管理器
- `Process/ProcessMetaPersist.cs` - 持久化数据结构
- `Process/ProcessManagerWindow.xaml` - 流程管理窗口 UI

---

*版本: 1.0.3.4+*
*最后更新: 2025-12-09*
