# FlowEngineLib API 参考文档

> FlowEngineLib 核心类和接口的API参考

## 📋 目录

- [核心类](#核心类)
- [节点基类](#节点基类)
- [数据模型](#数据模型)
- [事件系统](#事件系统)
- [MQTT通信](#mqtt通信)
- [工具类](#工具类)

## 核心类

### FlowEngineControl

流程引擎的主控制器类。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 继承关系
```csharp
FlowEngineAPI → FlowEngineControl
```

#### 构造函数

##### FlowEngineControl(bool)
```csharp
public FlowEngineControl(bool isAutoStartName)
```
创建流程引擎控制器实例。

**参数：**
- `isAutoStartName` - 是否自动生成启动节点名称

**示例：**
```csharp
var engine = new FlowEngineControl(isAutoStartName: true);
```

##### FlowEngineControl(STNodeEditor, bool)
```csharp
public FlowEngineControl(STNodeEditor nodeEditor, bool isAutoStartName)
```
创建流程引擎控制器并附加节点编辑器。

**参数：**
- `nodeEditor` - 节点编辑器实例
- `isAutoStartName` - 是否自动生成启动节点名称

**示例：**
```csharp
var nodeEditor = new STNodeEditor();
var engine = new FlowEngineControl(nodeEditor, true);
```

#### 属性

##### IsReady
```csharp
public bool IsReady { get; }
```
获取流程是否就绪。

**返回值：**
- `bool` - 如果流程已加载且就绪返回true，否则返回false

##### IsRunning
```csharp
public bool IsRunning { get; }
```
获取流程是否正在运行。

**返回值：**
- `bool` - 如果流程正在运行返回true，否则返回false

#### 方法

##### AttachNodeEditor
```csharp
public FlowEngineControl AttachNodeEditor(STNodeEditor nodeEditor)
```
附加节点编辑器到流程引擎。

**参数：**
- `nodeEditor` - 节点编辑器实例

**返回值：**
- `FlowEngineControl` - 返回当前实例，支持链式调用

**示例：**
```csharp
var engine = new FlowEngineControl(false)
    .AttachNodeEditor(nodeEditor);
```

##### RunFlow
```csharp
public void RunFlow(string flowName, string serialNumber = "")
```
运行指定的流程。

**参数：**
- `flowName` - 流程名称
- `serialNumber` - 流水号（可选）

**示例：**
```csharp
engine.RunFlow("MainFlow", "SN12345");
```

##### StopFlow
```csharp
public void StopFlow(string flowName)
```
停止指定的流程。

**参数：**
- `flowName` - 流程名称

**示例：**
```csharp
engine.StopFlow("MainFlow");
```

#### 事件

##### Finished
```csharp
public event FlowEngineEventHandler Finished
```
流程完成事件。

**事件参数：**
- `FlowEngineEventArgs` - 流程事件参数

**示例：**
```csharp
engine.Finished += (sender, args) => {
    Console.WriteLine($"Flow {args.FlowName} completed");
};
```

---

### FlowNodeManager

节点管理器类，管理所有设备节点。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### Instance
```csharp
public static FlowNodeManager Instance { get; }
```
获取FlowNodeManager的单例实例。

**返回值：**
- `FlowNodeManager` - 单例实例

#### 方法

##### AddDevice
```csharp
public void AddDevice(DeviceNode device)
```
添加设备节点到管理器。

**参数：**
- `device` - 设备节点实例

**示例：**
```csharp
var device = new DeviceNode("Camera", "CAM001", serviceInfo);
FlowNodeManager.Instance.AddDevice(device);
```

##### UpdateDevice
```csharp
public void UpdateDevice(Dictionary<string, Dictionary<string, DeviceNode>> devices)
```
更新设备节点状态。

**参数：**
- `devices` - 设备字典

##### UpdateDevice
```csharp
public void UpdateDevice(List<MQTTServiceInfo> services)
```
从MQTT服务信息更新设备。

**参数：**
- `services` - MQTT服务信息列表

##### Clear
```csharp
public void Clear()
```
清除所有设备节点。

---

### FlowServiceManager

服务管理器类。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### Instance
```csharp
public static FlowServiceManager Instance { get; }
```
获取FlowServiceManager的单例实例。

#### 方法

##### AddService
```csharp
public void AddService(MQTTServiceInfo service)
```
添加MQTT服务。

**参数：**
- `service` - MQTT服务信息

##### GetServices
```csharp
public List<MQTTServiceInfo> GetServices()
```
获取所有MQTT服务。

**返回值：**
- `List<MQTTServiceInfo>` - 服务列表

##### FindService
```csharp
public MQTTServiceInfo FindService(string serviceType, string serviceCode)
```
查找指定的服务。

**参数：**
- `serviceType` - 服务类型
- `serviceCode` - 服务代码

**返回值：**
- `MQTTServiceInfo` - 服务信息，未找到返回null

---

## 节点基类

### CVCommonNode

所有流程节点的基类。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 继承关系
```csharp
STNode → CVCommonNode
```

#### 属性

##### NodeName
```csharp
[STNodeProperty("服务名称", "服务名称", false, false)]
public string NodeName { get; set; }
```
获取或设置节点名称。

##### NodeType
```csharp
[STNodeProperty("节点类型", "节点类型/类别", false, true, true)]
public string NodeType { get; set; }
```
获取或设置节点类型。

##### DeviceCode
```csharp
[STNodeProperty("设备代码", "设备代码", false, false)]
public string DeviceCode { get; set; }
```
获取或设置设备代码。

##### NodeID
```csharp
[STNodeProperty("节点ID", "节点ID", false, false, true)]
public string NodeID { get; }
```
获取节点唯一标识符（GUID）。

##### ZIndex
```csharp
[STNodeProperty("z-index", "z-index", true, false, false)]
public int ZIndex { get; set; }
```
获取或设置节点的显示层级。

#### 方法

##### OnNodeNameChanged
```csharp
protected virtual void OnNodeNameChanged(string oldName, string newName)
```
节点名称改变时调用。

**参数：**
- `oldName` - 旧名称
- `newName` - 新名称

---

### BaseStartNode

流程启动节点基类。

#### 命名空间
```csharp
namespace FlowEngineLib.Start
```

#### 继承关系
```csharp
CVCommonNode → BaseStartNode
```

#### 属性

##### Ready
```csharp
public bool Ready { get; set; }
```
获取或设置节点是否就绪。

##### Running
```csharp
public bool Running { get; set; }
```
获取或设置节点是否正在运行。

##### m_op_start
```csharp
public STNodeOption m_op_start
```
主流程输出选项。

#### 方法

##### RunFlow
```csharp
public void RunFlow(CVStartCFC cfc)
```
运行流程。

**参数：**
- `cfc` - 流程控制对象

##### DoLoopNextAction
```csharp
public void DoLoopNextAction(CVLoopCFC next)
```
执行循环下一步动作。

**参数：**
- `next` - 循环控制对象

#### 事件

##### Finished
```csharp
public event FlowStartEventHandler Finished
```
流程完成事件。

---

### CVBaseServerNode

服务节点基类，执行具体业务逻辑。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 继承关系
```csharp
CVCommonNode → CVBaseServerNode
```

#### 属性

##### ServiceInfo
```csharp
public ServiceInfo ServiceInfo { get; set; }
```
获取或设置服务信息。

##### m_op_param_in
```csharp
public STNodeOption m_op_param_in
```
参数输入选项。

##### m_op_cmd_in
```csharp
public STNodeOption m_op_cmd_in
```
命令输入选项。

##### m_op_data_out
```csharp
public STNodeOption m_op_data_out
```
数据输出选项。

#### 方法

##### DoServerWork
```csharp
protected abstract void DoServerWork(CVStartCFC cfc)
```
执行服务工作（抽象方法，子类必须实现）。

**参数：**
- `cfc` - 流程控制对象

##### BuildServerData
```csharp
protected virtual CVBaseDataFlowResp BuildServerData()
```
构建服务响应数据。

**返回值：**
- `CVBaseDataFlowResp` - 响应数据对象

##### DoTransferData
```csharp
protected void DoTransferData(STNodeOption option, CVStartCFC cfc)
```
传输数据到下一节点。

**参数：**
- `option` - 输出选项
- `cfc` - 流程控制对象

---

### CVBaseLoopServerNode<T>

循环服务节点基类。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 继承关系
```csharp
CVBaseServerNode → CVBaseLoopServerNode<T>
```

#### 泛型参数
- `T` - 循环节点属性类型，必须实现ILoopNodeProperty接口

#### 属性

##### LoopModel
```csharp
public LoopDataModel LoopModel { get; set; }
```
获取或设置循环数据模型。

#### 方法

##### DoLoopAction
```csharp
protected abstract void DoLoopAction(CVStartCFC cfc, int loopIndex)
```
执行循环动作（抽象方法）。

**参数：**
- `cfc` - 流程控制对象
- `loopIndex` - 当前循环索引

##### BuildLoopStatusMsg
```csharp
protected virtual string BuildLoopStatusMsg(string nodeName, string deviceCode, int loopIndex)
```
构建循环状态消息。

**参数：**
- `nodeName` - 节点名称
- `deviceCode` - 设备代码
- `loopIndex` - 循环索引

**返回值：**
- `string` - 状态消息JSON字符串

---

## 数据模型

### CVStartCFC

流程启动控制对象。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 属性

##### NodeName
```csharp
public string NodeName { get; set; }
```
节点名称。

##### SerialNumber
```csharp
public string SerialNumber { get; set; }
```
流水号。

##### FlowName
```csharp
public string FlowName { get; set; }
```
流程名称。

##### Params
```csharp
public object Params { get; set; }
```
参数对象。

##### EventName
```csharp
public string EventName { get; set; }
```
事件名称。

---

### CVTransAction

数据传输动作对象。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 属性

##### ActionType
```csharp
public ActionTypeEnum ActionType { get; set; }
```
动作类型。

##### Data
```csharp
public object Data { get; set; }
```
数据内容。

##### Status
```csharp
public StatusTypeEnum Status { get; set; }
```
状态。

---

### CVLoopCFC

循环控制对象。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 属性

##### NodeName
```csharp
public string NodeName { get; set; }
```
循环节点名称。

##### SerialNumber
```csharp
public string SerialNumber { get; set; }
```
流水号。

##### LoopIndex
```csharp
public int LoopIndex { get; set; }
```
循环索引。

##### LoopData
```csharp
public object LoopData { get; set; }
```
循环数据。

---

### LoopDataModel

循环数据模型。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### BeginVal
```csharp
public int BeginVal { get; set; }
```
起始值。

##### EndVal
```csharp
public int EndVal { get; set; }
```
结束值。

##### StepVal
```csharp
public int StepVal { get; set; }
```
步长。

##### CurVal
```csharp
public int CurVal { get; set; }
```
当前值。

##### LoopCount
```csharp
public int LoopCount { get; set; }
```
循环次数。

---

### DeviceNode

设备节点类。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### DeviceType
```csharp
public string DeviceType { get; set; }
```
设备类型。

##### DeviceCode
```csharp
public string DeviceCode { get; set; }
```
设备代码。

##### ServiceInfo
```csharp
public ServiceInfo ServiceInfo { get; set; }
```
服务信息。

#### 方法

##### GetKey
```csharp
public string GetKey()
```
获取设备唯一键。

**返回值：**
- `string` - 设备唯一键

##### Update
```csharp
public void Update(DeviceNode device)
```
更新设备状态。

**参数：**
- `device` - 设备节点

---

## 事件系统

### FlowEngineEventHandler

流程引擎事件处理器委托。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 定义
```csharp
public delegate void FlowEngineEventHandler(object sender, FlowEngineEventArgs e)
```

---

### FlowEngineEventArgs

流程引擎事件参数。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 继承关系
```csharp
EventArgs → FlowEngineEventArgs
```

#### 属性

##### FlowName
```csharp
public string FlowName { get; set; }
```
流程名称。

##### Success
```csharp
public bool Success { get; set; }
```
是否成功。

##### Message
```csharp
public string Message { get; set; }
```
消息。

##### Result
```csharp
public object Result { get; set; }
```
结果数据。

---

### FlowStartEventHandler

流程启动事件处理器。

#### 命名空间
```csharp
namespace FlowEngineLib.Start
```

#### 定义
```csharp
public delegate void FlowStartEventHandler(object sender, FlowStartEventArgs e)
```

---

## MQTT通信

### MQTTHelper

MQTT通信辅助类。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 静态方法

##### SetDefaultCfg
```csharp
public static void SetDefaultCfg(
    string host,
    int port,
    string userName,
    string userPwd,
    bool useTls,
    string clientId)
```
设置MQTT默认配置。

**参数：**
- `host` - MQTT服务器地址
- `port` - 端口号
- `userName` - 用户名
- `userPwd` - 密码
- `useTls` - 是否使用TLS
- `clientId` - 客户端ID

**示例：**
```csharp
MQTTHelper.SetDefaultCfg("localhost", 1883, "user", "pass", false, null);
```

##### PublishAsyncClient
```csharp
public static void PublishAsyncClient(string topic, string data)
```
异步发布MQTT消息。

**参数：**
- `topic` - 主题
- `data` - 消息内容

**示例：**
```csharp
MQTTHelper.PublishAsyncClient("device/cmd", JsonConvert.SerializeObject(command));
```

##### SubscribeAsyncClient
```csharp
public static void SubscribeAsyncClient(string topic)
```
异步订阅MQTT主题。

**参数：**
- `topic` - 主题

**示例：**
```csharp
MQTTHelper.SubscribeAsyncClient("device/resp");
```

##### UnsubscribeAsync
```csharp
public static void UnsubscribeAsync(string topic)
```
取消订阅MQTT主题。

**参数：**
- `topic` - 主题

---

### MQTTServiceInfo

MQTT服务信息。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### ServiceType
```csharp
public string ServiceType { get; set; }
```
服务类型。

##### ServiceCode
```csharp
public string ServiceCode { get; set; }
```
服务代码。

##### Devices
```csharp
public Dictionary<string, MQTTDeviceInfo> Devices { get; set; }
```
设备字典。

---

### MQTTDeviceInfo

MQTT设备信息。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 属性

##### DeviceCode
```csharp
public string DeviceCode { get; set; }
```
设备代码。

##### DeviceName
```csharp
public string DeviceName { get; set; }
```
设备名称。

##### Status
```csharp
public string Status { get; set; }
```
设备状态。

---

## 工具类

### LogHelper

日志辅助类。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 静态方法

##### GetLogger
```csharp
public static ILog GetLogger(Type type)
```
获取日志记录器。

**参数：**
- `type` - 类型

**返回值：**
- `ILog` - 日志记录器实例

**示例：**
```csharp
private static readonly ILog logger = LogHelper.GetLogger(typeof(MyClass));
```

---

## 枚举类型

### ActionTypeEnum

动作类型枚举。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 值
- `Command` - 命令
- `Data` - 数据
- `Status` - 状态

---

### StatusTypeEnum

状态类型枚举。

#### 命名空间
```csharp
namespace FlowEngineLib.Base
```

#### 值
- `Success` - 成功
- `Failed` - 失败
- `Running` - 运行中
- `Waiting` - 等待中

---

### ActionStatusEnum

动作状态枚举。

#### 命名空间
```csharp
namespace FlowEngineLib
```

#### 值
- `Start` - 开始
- `Running` - 运行中
- `Completed` - 完成
- `Error` - 错误

---

## 使用示例

### 完整示例：创建和运行流程

```csharp
using FlowEngineLib;
using ST.Library.UI.NodeEditor;
using log4net;

public class FlowExample
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(FlowExample));
    
    public void RunExample()
    {
        // 1. 创建节点编辑器
        var nodeEditor = new STNodeEditor();
        
        // 2. 创建流程引擎
        var engine = new FlowEngineControl(nodeEditor, isAutoStartName: true);
        
        // 3. 订阅事件
        engine.Finished += OnFlowFinished;
        
        // 4. 配置MQTT
        MQTTHelper.SetDefaultCfg("localhost", 1883, "user", "pass", false, null);
        
        // 5. 运行流程
        engine.RunFlow("TestFlow", "SN001");
    }
    
    private void OnFlowFinished(object sender, FlowEngineEventArgs e)
    {
        if (e.Success)
        {
            logger.Info($"Flow {e.FlowName} completed successfully");
        }
        else
        {
            logger.Error($"Flow {e.FlowName} failed: {e.Message}");
        }
    }
}
```

---

**文档版本**: 1.0  
**最后更新**: 2024年  
**维护团队**: ColorVision 开发团队

> 💡 提示：本文档提供了FlowEngineLib的核心API参考。详细使用请参考[完整文档](../engine-components/FlowEngineLib.md)。
