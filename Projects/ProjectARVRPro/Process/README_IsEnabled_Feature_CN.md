# ProcessMeta IsEnabled 功能详解

## 概述

IsEnabled功能为ProcessMeta添加了选择性执行的能力。当ProcessMeta被禁用时，它将在测试执行流程中被自动跳过，而无需删除或修改流程配置。

## 属性说明

- **IsEnabled**: 布尔属性（默认值: `true`）
  - 当值为 `true` 时：ProcessMeta包含在执行流程中
  - 当值为 `false` 时：ProcessMeta在执行时被跳过

## 功能特性

### 1. 灵活的流程控制
- 无需删除ProcessMeta即可临时禁用测试步骤
- 支持运行时动态调整测试流程
- 保留完整的流程配置，便于后续重新启用

### 2. 配置持久化
- IsEnabled状态自动保存到`ProcessMetas.json`
- 应用重启后保持配置状态
- 支持配置文件的导入导出

### 3. 智能执行逻辑
- 自动跳过禁用的步骤
- 动态查找下一个启用的ProcessMeta
- 仅基于启用步骤判断测试完成

## 行为说明

### 示例场景

假设有8个ProcessMeta（索引0-7），只有索引0和7的ProcessMeta被启用：

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

### 执行流程

#### 1. 初始化（`FlowInit` in SocketControl）
- 系统查找第一个启用的ProcessMeta（索引0）
- 发送 `ARVRTestType = 0` 切换PG

#### 2. 完成步骤0后（`SwitchPGCompleted` in ARVRWindow）
- 系统从索引0之后搜索下一个启用的ProcessMeta
- 找到索引7的ProcessMeta（跳过1-6）
- 执行索引7的ProcessMeta

#### 3. 完成步骤7后（`IsTestTypeCompleted` in ARVRWindow）
- 系统检查索引7之后是否还有启用的ProcessMeta
- 未找到更多启用的ProcessMeta
- 返回 `true`，表示测试完成

#### 4. 测试完成（`TestCompleted` in ARVRWindow）
- 发送最终测试结果
- 清理资源和状态

## 使用指南

### 通过UI配置

1. **打开流程管理窗口**
   - 在ARVRWindow中点击"流程管理"按钮
   - 或使用菜单：工具 → 流程管理

2. **配置IsEnabled状态**
   - ProcessManagerWindow会显示"是否启用"列
   - 勾选复选框启用该ProcessMeta
   - 取消勾选禁用该ProcessMeta

3. **保存配置**
   - 更改会立即自动保存到`ProcessMetas.json`
   - 无需手动保存操作

### 通过代码配置

```csharp
// 获取ProcessManager实例
var processManager = ProcessManager.GetInstance();

// 禁用特定的ProcessMeta
processManager.ProcessMetas[2].IsEnabled = false;

// 批量配置
foreach (var meta in processManager.ProcessMetas)
{
    // 只启用名称包含"White"的测试
    meta.IsEnabled = meta.Name.Contains("White");
}

// 保存会自动触发（通过PropertyChanged事件）
```

### 配置文件示例

`ProcessMetas.json` 文件格式：

```json
[
  {
    "Name": "White255测试",
    "FlowTemplate": "White255Flow",
    "ProcessTypeFullName": "ProjectARVRPro.Process.W255.White255Process",
    "IsEnabled": true,
    "ConfigJson": "{}"
  },
  {
    "Name": "Black测试",
    "FlowTemplate": "BlackFlow",
    "ProcessTypeFullName": "ProjectARVRPro.Process.Black.BlackProcess",
    "IsEnabled": false,
    "ConfigJson": "{}"
  }
]
```

## 实现细节

### 修改的关键方法

#### ARVRWindow.cs

##### 1. SwitchPGCompleted()
- 从 `CurrentTestType + 1` 开始搜索下一个启用的ProcessMeta
- 仅处理启用的步骤

```csharp
public void SwitchPGCompleted()
{
    // 查找下一个启用的ProcessMeta
    int nextIndex = CurrentTestType + 1;
    while (nextIndex < ProcessMetas.Count && !ProcessMetas[nextIndex].IsEnabled)
    {
        nextIndex++;
    }
    
    if (nextIndex < ProcessMetas.Count)
    {
        CurrentTestType = nextIndex;
        // 执行下一个启用的步骤
    }
}
```

##### 2. IsTestTypeCompleted()
- 检查当前步骤之后是否还有启用的ProcessMeta
- 如果没有更多启用的步骤则返回 `true`

```csharp
private bool IsTestTypeCompleted()
{
    // 检查是否还有启用的步骤
    for (int i = CurrentTestType + 1; i < ProcessMetas.Count; i++)
    {
        if (ProcessMetas[i].IsEnabled)
            return false;
    }
    return true;
}
```

##### 3. SwitchPG()
- 发送下一个启用ProcessMeta的索引
- 如果未找到启用的ProcessMeta，发送 `-1`

```csharp
private void SwitchPG(int testType)
{
    if (testType >= 0 && testType < ProcessMetas.Count && ProcessMetas[testType].IsEnabled)
    {
        // 发送PG切换命令
        var switchPG = new SwitchPG { ARVRTestType = testType };
        // ...
    }
}
```

#### SocketControl.cs

##### FlowInit.Handle()
- 在初始化时查找第一个启用的ProcessMeta
- 不再硬编码为索引0

```csharp
public void Handle(MsgReturn msg)
{
    // 查找第一个启用的ProcessMeta
    int firstEnabledIndex = -1;
    for (int i = 0; i < ProcessMetas.Count; i++)
    {
        if (ProcessMetas[i].IsEnabled)
        {
            firstEnabledIndex = i;
            break;
        }
    }
    
    if (firstEnabledIndex >= 0)
    {
        SwitchPG(firstEnabledIndex);
    }
}
```

### UI变更

#### ProcessManagerWindow
- 新增"是否启用"列显示IsEnabled状态
- 用户可通过复选框切换启用/禁用状态
- 更改立即保存到`ProcessMetas.json`

### 数据持久化

#### ProcessManager.cs

配置的保存和加载通过以下方法实现：

```csharp
// 保存配置
private void SavePersistedMetas()
{
    var list = ProcessMetas.Select(m => new ProcessMetaPersist
    {
        Name = m.Name,
        FlowTemplate = m.FlowTemplate,
        ProcessTypeFullName = m.Process?.GetType().FullName,
        IsEnabled = m.IsEnabled,  // 保存IsEnabled状态
        ConfigJson = m.ConfigJson
    }).ToList();
    
    string json = JsonConvert.SerializeObject(list, Formatting.Indented);
    File.WriteAllText(PersistFilePath, json);
}

// 加载配置
private void LoadPersistedMetas()
{
    var list = JsonConvert.DeserializeObject<List<ProcessMetaPersist>>(json);
    foreach (var item in list)
    {
        ProcessMeta meta = new ProcessMeta
        {
            Name = item.Name,
            FlowTemplate = item.FlowTemplate,
            Process = proc,
            IsEnabled = item.IsEnabled  // 加载IsEnabled状态
        };
        ProcessMetas.Add(meta);
    }
}
```

## 使用场景

### 1. 快速调试
在开发或调试阶段，只启用需要测试的特定步骤：

```csharp
// 调试White255Process
ProcessMetas.Where(m => m.Name != "White255测试")
    .ToList()
    .ForEach(m => m.IsEnabled = false);
```

### 2. 部分测试
在产线上只执行关键测试项，跳过耗时的可选测试：

```csharp
// 只执行必需的测试
var requiredTests = new[] { "White255测试", "Black测试", "Distortion测试" };
ProcessMetas.ToList().ForEach(m => 
    m.IsEnabled = requiredTests.Contains(m.Name)
);
```

### 3. 阶段性测试
分阶段执行不同的测试组合：

```csharp
// 第一阶段：基础测试
EnableTestsByPattern("White.*|Black.*");

// 第二阶段：高级测试
EnableTestsByPattern("MTF.*|Distortion.*");
```

### 4. 测试优化
通过禁用非必要步骤提升测试效率：

```csharp
// 禁用耗时较长的可选测试
ProcessMetas.Where(m => m.Name.Contains("Optional"))
    .ToList()
    .ForEach(m => m.IsEnabled = false);
```

## 注意事项

### 1. 默认值
- 默认值为 `true` 以保持向后兼容性
- 现有配置文件中没有 `IsEnabled` 字段的ProcessMeta将默认为启用状态

### 2. 配置验证
- 确保至少有一个ProcessMeta被启用
- 如果所有ProcessMeta都被禁用，测试将立即完成

```csharp
// 验证配置
private bool ValidateConfiguration()
{
    if (!ProcessMetas.Any(m => m.IsEnabled))
    {
        MessageBox.Show("至少需要启用一个测试步骤", "配置错误");
        return false;
    }
    return true;
}
```

### 3. 性能影响
- IsEnabled检查的性能开销极小
- 跳过禁用步骤可显著提升整体测试速度
- 建议根据实际需求合理配置

### 4. 配置管理
- 定期备份 `ProcessMetas.json` 配置文件
- 为不同测试场景维护多个配置文件版本
- 使用版本控制系统管理配置文件

## 故障排查

### 问题：测试立即完成，没有执行任何步骤

**原因**：所有ProcessMeta都被禁用

**解决方案**：
```csharp
// 检查并启用至少一个ProcessMeta
if (!ProcessMetas.Any(m => m.IsEnabled))
{
    ProcessMetas.FirstOrDefault()?.IsEnabled = true;
}
```

### 问题：某些步骤被意外跳过

**原因**：IsEnabled状态被错误配置

**解决方案**：
1. 打开ProcessManagerWindow检查IsEnabled状态
2. 查看 `ProcessMetas.json` 确认配置
3. 重置为默认配置（全部启用）

### 问题：配置更改未生效

**原因**：配置文件未正确保存或加载

**解决方案**：
1. 检查 `ProcessMetas.json` 文件是否存在
2. 验证文件权限
3. 查看日志文件中的错误信息
4. 重启应用程序重新加载配置

## 扩展开发

### 添加条件启用逻辑

```csharp
public class ProcessMeta : ViewModelBase
{
    // 基于外部条件动态确定是否启用
    public bool ShouldExecute
    {
        get
        {
            if (!IsEnabled) return false;
            
            // 添加额外的条件判断
            if (RequiresSpecialHardware && !HardwareAvailable)
                return false;
            
            return true;
        }
    }
}
```

### 添加启用条件表达式

```csharp
public class ProcessMeta : ViewModelBase
{
    // 条件表达式（如："Temperature > 25 && Humidity < 60"）
    public string EnableCondition { get; set; }
    
    public bool EvaluateEnableCondition()
    {
        if (string.IsNullOrEmpty(EnableCondition))
            return IsEnabled;
        
        // 解析并评估条件表达式
        return ConditionEvaluator.Evaluate(EnableCondition) && IsEnabled;
    }
}
```

## 相关文档

- [ProcessMeta类详解](ProcessMeta.cs)
- [ProcessManager管理器](ProcessManager.cs)
- [ARVRWindow主窗口](../ARVRWindow.xaml.cs)
- [SocketControl通信控制](../Services/SocketControl.cs)

## 版本历史

### v1.0.3.4 (2025.11.17)
- 首次引入IsEnabled功能
- 实现基本的启用/禁用逻辑
- 添加UI支持和配置持久化

### v1.0.4.1 (2025.12.01)
- 优化IsEnabled实现
- 改进性能和稳定性
- 完善文档和示例

---

*本文档最后更新：2025年12月*
