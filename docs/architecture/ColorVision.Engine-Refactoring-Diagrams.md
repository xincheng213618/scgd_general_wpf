# ColorVision.Engine 重构架构图

本文档包含 ColorVision.Engine 重构的架构可视化图表。

## 当前架构（单体DLL）

```mermaid
graph TB
    UI[ColorVision.UI<br/>用户界面层] --> Engine[ColorVision.Engine.dll<br/>单体DLL<br/>580+文件]
    
    Engine --> ExternalDLL1[CVCommCore.dll]
    Engine --> ExternalDLL2[MQTTMessageLib.dll]
    Engine --> DB[(MySQL/SQLite<br/>数据库)]
    Engine --> MQTT[MQTT Broker]
    
    subgraph "ColorVision.Engine 内部"
        Engine --> Services[Services<br/>197个文件]
        Engine --> Templates[Templates<br/>316个文件]
        Engine --> MQTT_Internal[MQTT<br/>6个文件]
        Engine --> Dao[Dao<br/>数据访问]
        Engine --> Others[其他模块]
    end
    
    style Engine fill:#ff6b6b,stroke:#333,stroke-width:4px
    style UI fill:#4ecdc4,stroke:#333,stroke-width:2px
```

**问题**:
- ❌ 所有业务逻辑集中在一个DLL
- ❌ 模块边界不清晰
- ❌ 难以独立测试和部署
- ❌ 修改影响范围大

---

## 目标架构（模块化）

```mermaid
graph TB
    UI[ColorVision.UI<br/>用户界面]
    
    subgraph "核心层"
        Core[ColorVision.Engine.Core<br/>核心抽象<br/>~60个文件]
    end
    
    subgraph "业务模块层"
        Flow[ColorVision.Engine.Flow<br/>流程引擎<br/>~50个文件]
        Templates[ColorVision.Engine.Templates<br/>模板系统<br/>~220个文件]
        Devices[ColorVision.Engine.Devices<br/>设备服务<br/>~160个文件]
        Algorithms[ColorVision.Engine.Algorithms<br/>算法引擎<br/>~50个文件]
        PhyDevices[ColorVision.Engine.PhysicalDevices<br/>物理设备<br/>~25个文件]
    end
    
    subgraph "基础设施层"
        Data[ColorVision.Engine.Data<br/>数据访问<br/>~70个文件]
        Comm[ColorVision.Engine.Communication<br/>通信层<br/>~40个文件]
        Infra[ColorVision.Engine.Infrastructure<br/>基础设施<br/>~50个文件]
    end
    
    UI --> Core
    UI --> Flow
    UI --> Templates
    
    Flow --> Core
    Templates --> Core
    Devices --> Core
    Algorithms --> Core
    PhyDevices --> Core
    
    Core --> Data
    Core --> Comm
    Core --> Infra
    
    Flow --> Comm
    Devices --> Comm
    Devices --> Data
    Templates --> Data
    PhyDevices --> Data
    PhyDevices --> Devices
    
    Data --> DB[(MySQL/SQLite)]
    Comm --> MQTT[MQTT Broker]
    
    style Core fill:#4ecdc4,stroke:#333,stroke-width:4px
    style Flow fill:#95e1d3,stroke:#333,stroke-width:2px
    style Templates fill:#95e1d3,stroke:#333,stroke-width:2px
    style Devices fill:#95e1d3,stroke:#333,stroke-width:2px
    style Algorithms fill:#95e1d3,stroke:#333,stroke-width:2px
    style PhyDevices fill:#95e1d3,stroke:#333,stroke-width:2px
    style Data fill:#f38181,stroke:#333,stroke-width:2px
    style Comm fill:#f38181,stroke:#333,stroke-width:2px
    style Infra fill:#f38181,stroke:#333,stroke-width:2px
```

**优势**:
- ✅ 清晰的模块边界
- ✅ 可独立开发和测试
- ✅ 按需加载模块
- ✅ 易于维护和扩展

---

## 模块依赖关系

```mermaid
graph LR
    subgraph "应用层"
        UI[UI Layer]
    end
    
    subgraph "核心抽象层"
        Core[Core<br/>接口和抽象]
    end
    
    subgraph "业务模块"
        Flow[Flow Engine]
        Templates[Templates]
        Devices[Devices]
        Algorithms[Algorithms]
        PhyDevices[Physical Devices]
    end
    
    subgraph "基础设施"
        Data[Data Access]
        Comm[Communication]
        Infra[Infrastructure]
    end
    
    UI -->|使用| Core
    UI -->|使用| Flow
    UI -->|使用| Templates
    
    Flow -->|依赖| Core
    Flow -->|依赖| Comm
    
    Templates -->|依赖| Core
    Templates -->|依赖| Data
    
    Devices -->|依赖| Core
    Devices -->|依赖| Comm
    Devices -->|依赖| Data
    
    Algorithms -->|依赖| Core
    Algorithms -->|依赖| Templates
    
    PhyDevices -->|依赖| Core
    PhyDevices -->|依赖| Devices
    PhyDevices -->|依赖| Data
    
    Core -->|依赖| Infra
    Data -->|依赖| Core
    Comm -->|依赖| Core
    
    style Core fill:#ffd93d,stroke:#333,stroke-width:3px
```

**依赖规则**:
1. 所有模块都可依赖 Core
2. 业务模块之间避免直接依赖
3. 通过 Core 中的接口进行模块间通信
4. 依赖方向：上层→下层，业务层→基础设施层

---

## 数据流图

```mermaid
sequenceDiagram
    participant UI as 用户界面
    participant Core as Core<br/>(ServiceBus/EventBus)
    participant Flow as Flow Engine
    participant Device as Device Service
    participant Template as Template Engine
    participant Data as Data Access
    participant MQTT as MQTT Service
    
    UI->>Core: 1. 启动流程
    Core->>Flow: 2. 执行流程
    Flow->>Device: 3. 请求设备操作
    Device->>MQTT: 4. 发送MQTT命令
    MQTT-->>Device: 5. 设备响应
    Device->>Core: 6. 发布事件<br/>(ImageCapturedEvent)
    Core->>Template: 7. 触发模板处理
    Template->>Data: 8. 保存结果
    Data-->>Template: 9. 确认保存
    Template->>Core: 10. 发布事件<br/>(ProcessCompletedEvent)
    Core-->>Flow: 11. 流程继续
    Flow-->>UI: 12. 更新状态
    
    Note over Core: 事件驱动架构<br/>模块间松耦合
```

---

## 启动序列

```mermaid
sequenceDiagram
    participant App as Application
    participant DI as DI Container
    participant Core as Core Module
    participant Infra as Infrastructure
    participant Comm as Communication
    participant Data as Data Access
    participant Devices as Devices
    participant Templates as Templates
    participant Flow as Flow Engine
    
    App->>DI: 1. ConfigureServices()
    DI->>Core: 2. AddEngineCore()
    DI->>Infra: 3. AddEngineInfrastructure()
    DI->>Comm: 4. AddEngineCommunication()
    DI->>Data: 5. AddEngineData()
    DI->>Devices: 6. AddEngineDevices()
    DI->>Templates: 7. AddEngineTemplates()
    DI->>Flow: 8. AddEngineFlow()
    
    App->>Core: 9. Initialize()
    Core->>Infra: 10. 初始化日志和配置
    Core->>Comm: 11. 连接MQTT
    Core->>Data: 12. 初始化数据库
    
    par 并行初始化
        Core->>Devices: 13a. 初始化设备
        Core->>Templates: 13b. 加载模板
        Core->>Flow: 13c. 初始化流程引擎
    end
    
    Core-->>App: 14. 初始化完成
    
    Note over App,Flow: 延迟加载策略<br/>按需初始化模块
```

---

## 模块间通信模式

### 1. 直接调用（通过接口）

```mermaid
graph LR
    A[业务模块A] -->|1. 获取服务| B[ServiceBus]
    B -->|2. 返回接口| C[IService接口]
    A -->|3. 调用方法| C
    C -->|4. 实现| D[业务模块B]
    
    style B fill:#ffd93d,stroke:#333,stroke-width:2px
```

### 2. 事件驱动（松耦合）

```mermaid
graph TD
    A[设备服务] -->|1. Publish| E[EventBus]
    E -->|2. Notify| B[模板引擎]
    E -->|2. Notify| C[流程引擎]
    E -->|2. Notify| D[其他订阅者]
    
    style E fill:#ffd93d,stroke:#333,stroke-width:2px
```

### 3. 消息队列（异步通信）

```mermaid
graph LR
    A[发送者] -->|1. SendMessage| M[MessageQueue]
    M -->|2. DeliverMessage| B[接收者1]
    M -->|2. DeliverMessage| C[接收者2]
    
    style M fill:#ffd93d,stroke:#333,stroke-width:2px
```

---

## 插件化架构

```mermaid
graph TB
    subgraph "核心系统"
        Core[Engine Core]
        PluginLoader[Plugin Loader]
    end
    
    subgraph "官方插件"
        CameraPlugin[Camera Plugin]
        SpectrumPlugin[Spectrum Plugin]
        AlgorithmPlugin[Algorithm Plugin]
    end
    
    subgraph "第三方插件"
        CustomDevice[Custom Device Plugin]
        CustomAlgorithm[Custom Algorithm Plugin]
    end
    
    PluginLoader -->|加载| CameraPlugin
    PluginLoader -->|加载| SpectrumPlugin
    PluginLoader -->|加载| AlgorithmPlugin
    PluginLoader -->|加载| CustomDevice
    PluginLoader -->|加载| CustomAlgorithm
    
    CameraPlugin -->|注册服务| Core
    SpectrumPlugin -->|注册服务| Core
    AlgorithmPlugin -->|注册服务| Core
    CustomDevice -->|注册服务| Core
    CustomAlgorithm -->|注册服务| Core
    
    style Core fill:#4ecdc4,stroke:#333,stroke-width:3px
    style PluginLoader fill:#ffd93d,stroke:#333,stroke-width:2px
    style CustomDevice fill:#ff6b6b,stroke:#333,stroke-width:2px
    style CustomAlgorithm fill:#ff6b6b,stroke:#333,stroke-width:2px
```

**插件开发流程**:
1. 实现 `IEnginePlugin` 接口
2. 在 `RegisterServices()` 中注册服务
3. 在 `Initialize()` 中初始化插件
4. 将DLL放入 `plugins/` 目录
5. 系统自动加载和注册

---

## 部署架构

### 开发环境

```mermaid
graph TB
    subgraph "开发机"
        Dev[Visual Studio]
        Dev -->|编译| Modules[模块DLL]
        Modules -->|输出| DevOutput[bin/Debug/]
    end
    
    subgraph "测试环境"
        TestApp[测试应用]
        TestApp -->|加载| DevOutput
        TestApp -->|连接| TestDB[(测试数据库)]
        TestApp -->|连接| TestMQTT[测试MQTT]
    end
```

### 生产环境

```mermaid
graph TB
    subgraph "应用服务器"
        App[ColorVision应用]
        
        subgraph "核心模块"
            Core[Core.dll]
        end
        
        subgraph "业务模块"
            Flow[Flow.dll]
            Templates[Templates.dll]
            Devices[Devices.dll]
        end
        
        subgraph "基础设施"
            Data[Data.dll]
            Comm[Comm.dll]
        end
        
        subgraph "插件目录"
            Plugins[plugins/]
        end
        
        App --> Core
        App --> Flow
        App --> Templates
        App --> Devices
        App --> Data
        App --> Comm
        App --> Plugins
    end
    
    subgraph "基础设施"
        DB[(生产数据库)]
        MQTT[生产MQTT Broker]
    end
    
    Data --> DB
    Comm --> MQTT
    
    style App fill:#4ecdc4,stroke:#333,stroke-width:3px
```

---

## 性能优化策略

```mermaid
graph TB
    subgraph "启动优化"
        A1[延迟加载] --> A2[并行初始化]
        A2 --> A3[按需加载插件]
    end
    
    subgraph "运行时优化"
        B1[对象池] --> B2[缓存机制]
        B2 --> B3[异步处理]
    end
    
    subgraph "内存优化"
        C1[弱引用] --> C2[及时释放]
        C2 --> C3[资源复用]
    end
    
    subgraph "通信优化"
        D1[批量操作] --> D2[消息压缩]
        D2 --> D3[连接复用]
    end
    
    A3 --> Performance[性能提升]
    B3 --> Performance
    C3 --> Performance
    D3 --> Performance
    
    style Performance fill:#95e1d3,stroke:#333,stroke-width:3px
```

**优化目标**:
- 启动时间: ≤ 5秒
- 模块加载: ≤ 500ms/模块
- 内存占用: 降低30%
- API响应: ≤ 100ms

---

## 测试策略

```mermaid
graph TB
    subgraph "单元测试"
        UT1[Core模块测试] --> UT2[业务模块测试]
        UT2 --> UT3[基础设施测试]
    end
    
    subgraph "集成测试"
        IT1[模块间集成] --> IT2[系统集成]
    end
    
    subgraph "性能测试"
        PT1[启动性能] --> PT2[运行性能]
        PT2 --> PT3[压力测试]
    end
    
    subgraph "验收测试"
        AT1[功能测试] --> AT2[回归测试]
    end
    
    UT3 --> IT1
    IT2 --> PT1
    PT3 --> AT1
    AT2 --> Release[发布]
    
    style Release fill:#4ecdc4,stroke:#333,stroke-width:3px
```

**测试覆盖率目标**:
- 单元测试: ≥ 80%
- 集成测试: 100% 关键路径
- 性能测试: 所有性能指标
- 验收测试: 100% 功能点

---

## 迁移路线图时间轴

```mermaid
gantt
    title ColorVision.Engine 重构时间轴
    dateFormat  YYYY-MM-DD
    section 准备阶段
    代码分析           :done, prep1, 2025-01-08, 1w
    基础设施搭建        :done, prep2, 2025-01-15, 1w
    
    section 核心开发
    Core模块           :active, core1, 2025-01-22, 3w
    Infrastructure     :crit, infra1, 2025-02-12, 1w
    Communication      :crit, comm1, 2025-02-12, 1w
    Data模块           :data1, 2025-02-19, 2w
    
    section 业务模块
    Devices模块        :dev1, 2025-03-05, 4w
    Templates模块      :temp1, 2025-04-02, 4w
    Algorithms模块     :algo1, 2025-04-02, 2w
    Flow模块           :flow1, 2025-04-30, 3w
    PhysicalDevices    :phy1, 2025-05-21, 1w
    
    section 集成测试
    系统集成           :test1, 2025-05-28, 2w
    性能优化           :opt1, 2025-06-11, 1w
    
    section 部署
    生产部署           :deploy1, 2025-06-18, 1w
```

---

## 相关文档

- [完整重构方案](./ColorVision.Engine-Refactoring-Plan.md)
- [执行摘要](./ColorVision.Engine-Refactoring-Summary.md)
- [实施检查清单](./ColorVision.Engine-Refactoring-Checklist.md)
- [现有架构文档](../engine-components/ColorVision.Engine.md)

---

**文档版本**: v1.0  
**创建日期**: 2025-01-08  
**图表说明**: 使用 Mermaid 语法，支持在 Markdown 预览器中直接渲染
