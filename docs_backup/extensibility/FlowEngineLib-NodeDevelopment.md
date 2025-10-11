# FlowEngineLib 节点开发指南

> 如何为 FlowEngineLib 开发自定义流程节点

## 📋 目录

- [概述](#概述)
- [节点基础](#节点基础)
- [节点类型](#节点类型)
- [开发步骤](#开发步骤)
- [高级特性](#高级特性)
- [最佳实践](#最佳实践)
- [调试技巧](#调试技巧)
- [常见问题](#常见问题)

## 概述

FlowEngineLib 提供了强大的节点扩展机制，开发者可以通过继承基类来创建自定义节点，实现特定的业务逻辑。

### 节点分类

```
CVCommonNode (基类)
├── BaseStartNode (启动节点)
│   ├── MQTTStartNode
│   └── ModbusStartNode
├── CVBaseServerNode (服务节点)
│   ├── CVCameraNode
│   ├── AlgorithmNode
│   ├── SMUNode
│   └── CVBaseLoopServerNode (循环节点)
│       └── PGLoopNode
├── CVEndNode (结束节点)
└── LoopNode (循环控制节点)
```

## 节点基础

### 核心概念

1. **节点输入输出**
   - `InputOptions` - 输入选项集合
   - `OutputOptions` - 输出选项集合
   - `STNodeOption` - 连接点

2. **数据传递**
   - `CVStartCFC` - 流程控制对象
   - `CVTransAction` - 数据传输对象
   - `CVLoopCFC` - 循环控制对象

3. **节点生命周期**
   ```
   构造函数 → OnCreate → DoServerWork → DoTransferData
   ```

### 必需的Attribute

```csharp
[STNode("/Category/NodeName")]  // 节点分类和名称
public class MyNode : CVBaseServerNode
{
    // 节点实现
}
```

分类建议：
- `/01 运算` - 运算和逻辑节点
- `/02 相机` - 相机相关节点
- `/03 算法` - 算法处理节点
- `/04 设备` - 设备控制节点
- `/05 通信` - 通信节点

## 节点类型

### 1. 普通服务节点

最常用的节点类型，用于执行具体的业务逻辑。

#### 基本模板

```csharp
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;

[STNode("/Custom/MyServiceNode")]
public class MyServiceNode : CVBaseServerNode
{
    // 1. 构造函数
    public MyServiceNode()
        : base("MyServiceNode", "ServiceNode", "SN1", "DEV01")
    {
        // 初始化成员变量
    }
    
    // 2. 创建节点
    protected override void OnCreate()
    {
        base.OnCreate();
        
        // 设置节点属性
        base.AutoSize = false;
        base.Width = 200;
        base.Height = 120;
        
        // 添加自定义输入输出
        InputOptions.Add("CustomInput", typeof(double), false);
        OutputOptions.Add("CustomOutput", typeof(double), false);
    }
    
    // 3. 执行业务逻辑
    protected override void DoServerWork(CVStartCFC cfc)
    {
        try
        {
            // 获取输入数据
            var inputValue = GetInputData\<double\>("CustomInput");
            
            // 处理数据
            var result = ProcessData(inputValue);
            
            // 设置输出数据
            SetOutputData("CustomOutput", result);
            
            // 构建响应数据
            var response = BuildServerData();
            
            // 传递给下一节点
            DoTransferData(m_op_data_out, cfc);
        }
        catch (Exception ex)
        {
            logger.Error($"Node {NodeName} execution failed", ex);
            throw;
        }
    }
    
    // 4. 处理数据的私有方法
    private double ProcessData(double input)
    {
        // 实现业务逻辑
        return input * 2;
    }
}
```

#### 添加节点属性

```csharp
private double _threshold;
private string _mode;

[STNodeProperty("阈值", "处理阈值", false, false)]
public double Threshold
{
    get => _threshold;
    set
    {
        if (_threshold != value)
        {
            _threshold = value;
            OnPropertyChanged();
        }
    }
}

[STNodeProperty("模式", "处理模式", false, false)]
public string Mode
{
    get => _mode;
    set
    {
        _mode = value;
        OnPropertyChanged();
    }
}
```

### 2. 循环节点

用于需要重复执行的场景。

#### 基本模板

```csharp
[STNode("/Custom/MyLoopNode")]
public class MyLoopNode : CVBaseLoopServerNode\<MyLoopProperty\>
{
    public MyLoopNode()
        : base("MyLoopNode", "LoopNode", "LN1", "DEV01")
    {
    }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        // 设置循环属性
        LoopModel = new LoopDataModel
        {
            BeginVal = 1,
            EndVal = 10,
            StepVal = 1
        };
    }
    
    // 循环执行的逻辑
    protected override void DoLoopAction(CVStartCFC cfc, int loopIndex)
    {
        logger.Info($"Loop iteration {loopIndex}/{LoopModel.EndVal}");
        
        // 执行循环任务
        var result = ProcessLoopIteration(loopIndex);
        
        // 传递循环数据
        var loopCFC = new CVLoopCFC
        {
            NodeName = NodeName,
            SerialNumber = cfc.SerialNumber,
            LoopIndex = loopIndex,
            LoopData = result
        };
        
        DoLoopTransferData(m_op_loop_out, loopCFC);
    }
    
    private object ProcessLoopIteration(int index)
    {
        // 实现循环逻辑
        return $"Result_{index}";
    }
}

// 循环属性类
public class MyLoopProperty : ILoopNodeProperty
{
    public int BeginValue { get; set; }
    public int EndValue { get; set; }
    public int StepValue { get; set; }
}
```

### 3. MQTT节点

用于MQTT通信。

#### 发布节点

```csharp
[STNode("/MQTT/MyPublishNode")]
public class MyMQTTPublishNode : CVBaseServerNode
{
    private string _topic;
    
    [STNodeProperty("主题", "MQTT主题", false, false)]
    public string Topic
    {
        get => _topic;
        set => _topic = value;
    }
    
    protected override void DoServerWork(CVStartCFC cfc)
    {
        // 构建消息
        var message = BuildMessage(cfc);
        
        // 发布到MQTT
        MQTTHelper.PublishAsyncClient(_topic, message);
        
        logger.Info($"Published to {_topic}: {message}");
        
        // 传递给下一节点
        DoTransferData(m_op_data_out, cfc);
    }
    
    private string BuildMessage(CVStartCFC cfc)
    {
        return JsonConvert.SerializeObject(new
        {
            Timestamp = DateTime.Now,
            FlowName = cfc.FlowName,
            Data = cfc.Params
        });
    }
}
```

#### 订阅节点

```csharp
[STNode("/MQTT/MySubscribeNode")]
public class MyMQTTSubscribeNode : CVBaseServerNode
{
    private string _topic;
    private string _receivedMessage;
    
    [STNodeProperty("主题", "订阅主题", false, false)]
    public string Topic
    {
        get => _topic;
        set
        {
            if (_topic != value)
            {
                if (!string.IsNullOrEmpty(_topic))
                {
                    MQTTHelper.UnsubscribeAsync(_topic);
                }
                _topic = value;
                if (!string.IsNullOrEmpty(_topic))
                {
                    MQTTHelper.SubscribeAsyncClient(_topic);
                    MQTTHelper.MessageReceived += OnMessageReceived;
                }
            }
        }
    }
    
    private void OnMessageReceived(string topic, string message)
    {
        if (topic == _topic)
        {
            _receivedMessage = message;
            logger.Info($"Received from {topic}: {message}");
        }
    }
    
    protected override void DoServerWork(CVStartCFC cfc)
    {
        // 等待消息或超时
        if (WaitForMessage(timeout: 5000))
        {
            // 解析消息
            var data = JsonConvert.DeserializeObject<Dictionary\\<string, object>\>(_receivedMessage);
            
            // 设置到输出
            SetOutputData("ReceivedData", data);
            
            // 传递给下一节点
            DoTransferData(m_op_data_out, cfc);
        }
        else
        {
            throw new TimeoutException($"No message received on {_topic}");
        }
    }
}
```

### 4. 相机节点

用于相机控制和图像采集。

```csharp
[STNode("/Camera/MyCameraNode")]
public class MyCameraNode : CVBaseServerNode
{
    private int _exposureTime;
    private int _gain;
    
    [STNodeProperty("曝光时间", "曝光时间(us)", false, false)]
    public int ExposureTime
    {
        get => _exposureTime;
        set => _exposureTime = value;
    }
    
    [STNodeProperty("增益", "相机增益", false, false)]
    public int Gain
    {
        get => _gain;
        set => _gain = value;
    }
    
    protected override void DoServerWork(CVStartCFC cfc)
    {
        // 1. 设置相机参数
        SetCameraParameters();
        
        // 2. 触发拍照
        var imageData = CaptureImage();
        
        // 3. 保存图像
        var imagePath = SaveImage(imageData, cfc.SerialNumber);
        
        // 4. 构建响应
        var response = new CameraDataModel
        {
            ImagePath = imagePath,
            Width = imageData.Width,
            Height = imageData.Height,
            Timestamp = DateTime.Now
        };
        
        // 5. 设置输出
        SetOutputData("ImageData", response);
        
        // 6. 传递给下一节点
        DoTransferData(m_op_data_out, cfc);
    }
    
    private void SetCameraParameters()
    {
        // 通过MQTT设置相机参数
        var command = new
        {
            Command = "SetParams",
            ExposureTime = _exposureTime,
            Gain = _gain
        };
        
        MQTTHelper.PublishAsyncClient(
            $"Camera/{DeviceCode}/cmd",
            JsonConvert.SerializeObject(command));
    }
    
    private ImageData CaptureImage()
    {
        // 触发拍照并等待结果
        var captureCmd = new { Command = "Capture" };
        MQTTHelper.PublishAsyncClient(
            $"Camera/{DeviceCode}/cmd",
            JsonConvert.SerializeObject(captureCmd));
            
        // 等待图像数据
        return WaitForImageData(timeout: 10000);
    }
}
```

### 5. 算法节点

用于图像处理和数据分析。

```csharp
[STNode("/Algorithm/MyAlgorithmNode")]
public class MyAlgorithmNode : CVBaseServerNode
{
    private string _algorithmType;
    private Dictionary\\<string, object\> _parameters;
    
    [STNodeProperty("算法类型", "算法类型", false, false)]
    public string AlgorithmType
    {
        get => _algorithmType;
        set => _algorithmType = value;
    }
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        // 添加参数输入
        InputOptions.Add("ImageData", typeof(CameraDataModel), false);
        InputOptions.Add("ROI", typeof(Rectangle), true);  // 可选输入
        
        // 添加结果输出
        OutputOptions.Add("Result", typeof(AlgorithmResult), false);
    }
    
    protected override void DoServerWork(CVStartCFC cfc)
    {
        // 1. 获取输入数据
        var imageData = GetInputData\<CameraDataModel\>("ImageData");
        var roi = GetInputData<Rectangle?>("ROI");
        
        // 2. 加载图像
        var image = LoadImage(imageData.ImagePath);
        
        // 3. 执行算法
        var result = ExecuteAlgorithm(image, roi);
        
        // 4. 保存结果
        SaveResult(result, cfc.SerialNumber);
        
        // 5. 设置输出
        SetOutputData("Result", result);
        
        // 6. 传递给下一节点
        DoTransferData(m_op_data_out, cfc);
    }
    
    private AlgorithmResult ExecuteAlgorithm(Image image, Rectangle? roi)
    {
        // 根据算法类型执行不同的算法
        return _algorithmType switch
        {
            "EdgeDetection" => ExecuteEdgeDetection(image, roi),
            "BlobAnalysis" => ExecuteBlobAnalysis(image, roi),
            "Measurement" => ExecuteMeasurement(image, roi),
            _ => throw new NotSupportedException($"Algorithm {_algorithmType} not supported")
        };
    }
}
```

## 开发步骤

### 步骤1: 创建节点类

```csharp
using FlowEngineLib.Base;
using ST.Library.UI.NodeEditor;
using log4net;

[STNode("/Custom/MyNode")]
public class MyNode : CVBaseServerNode
{
    private static readonly ILog logger = LogManager.GetLogger(typeof(MyNode));
    
    public MyNode()
        : base("MyNode", "CustomNode", "CN1", "DEV01")
    {
    }
}
```

### 步骤2: 配置节点属性

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    
    // 设置节点外观
    base.TitleColor = Color.FromArgb(200, Color.Blue);
    base.AutoSize = false;
    base.Width = 200;
    base.Height = 100;
    
    // 添加输入输出
    InputOptions.Add("Input1", typeof(double), false);
    OutputOptions.Add("Output1", typeof(double), false);
}
```

### 步骤3: 实现业务逻辑

```csharp
protected override void DoServerWork(CVStartCFC cfc)
{
    logger.Info($"Node {NodeName} starting execution");
    
    try
    {
        // 1. 验证输入
        ValidateInputs();
        
        // 2. 获取数据
        var input = GetInputData\<double\>("Input1");
        
        // 3. 处理数据
        var output = ProcessData(input);
        
        // 4. 设置输出
        SetOutputData("Output1", output);
        
        // 5. 传递数据
        DoTransferData(m_op_data_out, cfc);
        
        logger.Info($"Node {NodeName} completed successfully");
    }
    catch (Exception ex)
    {
        logger.Error($"Node {NodeName} execution failed", ex);
        throw;
    }
}
```

### 步骤4: 测试节点

```csharp
[TestClass]
public class MyNodeTests
{
    [TestMethod]
    public void MyNode_ValidInput_ReturnsCorrectOutput()
    {
        // Arrange
        var node = new MyNode();
        var cfc = new CVStartCFC
        {
            NodeName = "TestNode",
            SerialNumber = "12345"
        };
        
        // Act
        node.DoServerWork(cfc);
        
        // Assert
        var output = node.GetOutputData\<double\>("Output1");
        Assert.AreEqual(expected, output);
    }
}
```

## 高级特性

### 1. 自定义UI控件

```csharp
public class MyCustomControl : STNodeControl
{
    private TextBox _textBox;
    
    protected override void OnCreate()
    {
        base.OnCreate();
        
        _textBox = new TextBox
        {
            Width = 100,
            Height = 20
        };
        
        Controls.Add(_textBox);
    }
    
    protected override void OnPaint(PaintEventArgs e)
    {
        // 自定义绘制
    }
}

// 在节点中使用
protected override void OnCreate()
{
    base.OnCreate();
    var control = new MyCustomControl();
    Controls.Add(control);
}
```

### 2. 节点状态管理

```csharp
public enum NodeState
{
    Idle,
    Running,
    Completed,
    Error
}

private NodeState _state;

public NodeState State
{
    get => _state;
    set
    {
        _state = value;
        UpdateNodeAppearance();
    }
}

private void UpdateNodeAppearance()
{
    base.TitleColor = _state switch
    {
        NodeState.Idle => Color.Gray,
        NodeState.Running => Color.Blue,
        NodeState.Completed => Color.Green,
        NodeState.Error => Color.Red,
        _ => Color.Gray
    };
    Invalidate();
}
```

### 3. 异步操作

```csharp
protected override void DoServerWork(CVStartCFC cfc)
{
    // 使用异步方法
    var result = Task.Run(async () =>
    {
        return await ProcessDataAsync();
    }).GetAwaiter().GetResult();
    
    SetOutputData("Result", result);
    DoTransferData(m_op_data_out, cfc);
}

private async Task\<object\> ProcessDataAsync()
{
    await Task.Delay(100); // 模拟异步操作
    return ProcessData();
}
```

### 4. 数据验证

```csharp
private void ValidateInputs()
{
    if (!InputOptions["Input1"].IsConnected)
        throw new InvalidOperationException("Input1 must be connected");
        
    var value = GetInputData\<double\>("Input1");
    if (value < 0 || value > 100)
        throw new ArgumentOutOfRangeException("Input1 must be between 0 and 100");
}
```

## 最佳实践

### 1. 日志记录

```csharp
// 在关键位置记录日志
logger.Debug($"Node {NodeName} - Input: {JsonConvert.SerializeObject(input)}");
logger.Info($"Node {NodeName} - Processing started");
logger.Warn($"Node {NodeName} - Unusual condition detected");
logger.Error($"Node {NodeName} - Error occurred", exception);
```

### 2. 错误处理

```csharp
protected override void DoServerWork(CVStartCFC cfc)
{
    try
    {
        // 业务逻辑
    }
    catch (DeviceNotReadyException ex)
    {
        logger.Warn("Device not ready, retrying...", ex);
        Retry(() => DoServerWork(cfc), maxAttempts: 3);
    }
    catch (Exception ex)
    {
        logger.Error("Unexpected error", ex);
        throw new NodeExecutionException($"Node {NodeName} failed", ex);
    }
}
```

### 3. 资源清理

```csharp
public class MyNode : CVBaseServerNode, IDisposable
{
    private Stream _fileStream;
    
    public void Dispose()
    {
        _fileStream?.Dispose();
        // 清理其他资源
    }
}
```

### 4. 性能优化

```csharp
// 使用对象池
private static readonly ObjectPool<byte[]> BufferPool = 
    ObjectPool.Create(() => new byte[1024]);

protected override void DoServerWork(CVStartCFC cfc)
{
    var buffer = BufferPool.Get();
    try
    {
        // 使用buffer
    }
    finally
    {
        BufferPool.Return(buffer);
    }
}
```

## 调试技巧

### 1. 使用断点

在以下位置设置断点：
- `OnCreate()` - 检查初始化
- `DoServerWork()` - 检查执行流程
- `DoTransferData()` - 检查数据传递

### 2. 日志调试

```csharp
logger.Debug($"Node state: {JsonConvert.SerializeObject(this, Formatting.Indented)}");
```

### 3. 可视化调试

```csharp
protected override string OnGetDrawTitle()
{
    return $"{base.Title}\n{NodeName}\nState: {State}";
}
```

## 常见问题

### Q1: 如何获取上一个节点的输出？

```csharp
protected override void DoServerWork(CVStartCFC cfc)
{
    // 从CFC中获取
    var previousData = cfc.Params;
    
    // 或从输入连接获取
    var inputData = GetInputData\<MyDataType\>("InputName");
}
```

### Q2: 如何处理可选输入？

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    // 第三个参数为true表示可选
    InputOptions.Add("OptionalInput", typeof(string), true);
}

protected override void DoServerWork(CVStartCFC cfc)
{
    var optionalValue = InputOptions["OptionalInput"].IsConnected
        ? GetInputData\<string\>("OptionalInput")
        : "default_value";
}
```

### Q3: 如何实现条件分支？

```csharp
protected override void OnCreate()
{
    base.OnCreate();
    OutputOptions.Add("Success", typeof(CVTransAction), false);
    OutputOptions.Add("Failure", typeof(CVTransAction), false);
}

protected override void DoServerWork(CVStartCFC cfc)
{
    if (ProcessData())
    {
        DoTransferData(OutputOptions["Success"], cfc);
    }
    else
    {
        DoTransferData(OutputOptions["Failure"], cfc);
    }
}
```

### Q4: 如何共享数据到后续节点？

```csharp
protected override void DoServerWork(CVStartCFC cfc)
{
    var result = ProcessData();
    
    // 方式1: 设置到CFC
    cfc.Params = result;
    
    // 方式2: 设置到输出
    SetOutputData("Output", result);
    
    DoTransferData(m_op_data_out, cfc);
}
```

---

## 参考资料

- [FlowEngineLib API文档](../engine-components/FlowEngineLib.md)
- [ST.Library.UI文档](../engine-components/ST.Library.UI.md)
- [流程引擎概述](../flow-engine/flow-engine-overview.md)

---

**文档版本**: 1.0  
**最后更新**: 2024年  
**维护团队**: ColorVision 开发团队
