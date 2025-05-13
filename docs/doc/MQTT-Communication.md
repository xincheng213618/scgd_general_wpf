# MQTT Communication

> **Relevant source files**
> * [DLL/CVCommCore.dll](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/DLL/CVCommCore.dll)
> * [DLL/FlowEngineLib.dll](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/DLL/FlowEngineLib.dll)
> * [DLL/MQTTMessageLib.dll](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/DLL/MQTTMessageLib.dll)
> * [DLL/ST.Library.UI.dll](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/DLL/ST.Library.UI.dll)
> * [Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgResultDistortionDao.cs](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgResultDistortionDao.cs)
> * [Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewHandleDistortion.cs](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/Engine/ColorVision.Engine/Templates/ARVR/Distortion/ViewHandleDistortion.cs)
> * [Engine/cvColorVision/CMStruct.cs](https://github.com/xincheng213618/scgd_general_wpf/blob/987af5f7/Engine/cvColorVision/CMStruct.cs)

This page documents the MQTT-based communication framework used within the ColorVision system. The MQTT messaging system serves as the backbone for inter-service communication, allowing various components such as cameras, algorithms, and motor controllers to exchange information in a standardized way. For information about the service architecture that uses this communication layer, see [Service System](/xincheng213618/scgd_general_wpf/4-service-system).

## 1. Overview

The ColorVision system implements an MQTT (Message Queuing Telemetry Transport) communication protocol to enable asynchronous, loosely-coupled messaging between services and components. This lightweight publish-subscribe framework facilitates reliable data exchange across the application with minimal dependencies between components.

```mermaid
flowchart TD

subgraph Flow_Layer ["Flow Layer"]
end

subgraph Service_Layer ["Service Layer"]
end

subgraph Communication_Layer ["Communication Layer"]
end

MQTT["MQTT Message System"]
CommCore["CVCommCore"]
ServiceManager["ServiceManager"]
Camera["Camera Services"]
Algorithm["Algorithm Services"]
Calibration["Calibration Services"]
SMU["Source Meter Services"]
Motor["Motor Control"]
PG["Pattern Generator"]
FlowEngine["Flow Engine"]
FlowControl["Flow Control"]

    ServiceManager --> Camera
    ServiceManager --> Algorithm
    ServiceManager --> Calibration
    ServiceManager --> SMU
    ServiceManager --> Motor
    ServiceManager --> PG
    Camera --> MQTT
    Algorithm --> MQTT
    Calibration --> MQTT
    SMU --> MQTT
    Motor --> MQTT
    PG --> MQTT
    MQTT --> CommCore
    MQTT --> FlowControl
    FlowControl --> FlowEngine
```

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll

## 2. Core Components

The MQTT communication system consists of several key components:

| Component | Purpose |
| --- | --- |
| MQTTMessageLib | Core library containing message definitions and MQTT clients |
| CVCommCore | Communication abstraction layer providing service-specific interfaces |
| MQTT Broker | External or embedded message broker that routes messages between publishers and subscribers |
| Service Adapters | Service-specific code that translates between MQTT messages and service actions |

### 2.1 Component Architecture

```mermaid
flowchart TD

subgraph Infrastructure ["Infrastructure"]
end

subgraph Communication_Layer ["Communication Layer"]
end

subgraph Application_Components ["Application Components"]
end

UI["User Interface"]
FlowEngine["Flow Engine"]
Services["Services"]
Clients["MQTT Clients"]
MsgLib["MQTTMessageLib"]
CommCore["CVCommCore"]
MQTTBroker["MQTT Broker"]

    UI --> Clients
    FlowEngine --> Clients
    Services --> Clients
    Clients --> MsgLib
    MsgLib --> CommCore
    CommCore --> MQTTBroker
```

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll

## 3. Message Structure

The ColorVision system uses standardized message formats to ensure consistent communication across all components.

### 3.1 Core Message Types

Based on the binary analysis of MQTTMessageLib.dll, the system appears to support several message types:

| Message Type | Description | Typical Use |
| --- | --- | --- |
| Command | Instructions to perform an action | Trigger a camera capture, move a motor |
| Status | Current state information | Report device ready/busy/error states |
| Data | Raw or processed information | Image data, measurement results |
| Configuration | Settings updates | Update camera exposure settings |
| Result | Processing outcomes | Algorithm analysis results |

### 3.2 Message Format

Messages in the system are likely structured as follows:

```

```

Sources: DLL/MQTTMessageLib.dll

## 4. Topic Structure

Topics in the ColorVision MQTT system follow a hierarchical structure that enables effective message routing and filtering.

### 4.1 Standard Topic Format

```
scgd/{serviceType}/{serviceId}/{messageType}
```

For example:

* `scgd/camera/cam01/command`
* `scgd/algorithm/alg01/status`
* `scgd/motor/mot01/position`

### 4.2 Topic Examples by Service

| Service Type | Example Topics |
| --- | --- |
| Camera | scgd/camera/{id}/command, scgd/camera/{id}/status, scgd/camera/{id}/image |
| Algorithm | scgd/algorithm/{id}/process, scgd/algorithm/{id}/status, scgd/algorithm/{id}/result |
| Motor | scgd/motor/{id}/move, scgd/motor/{id}/position, scgd/motor/{id}/status |
| SMU | scgd/smu/{id}/measure, scgd/smu/{id}/status, scgd/smu/{id}/result |
| Pattern Generator | scgd/pattern/{id}/generate, scgd/pattern/{id}/status |

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll

## 5. Message Flow

The MQTT communication system facilitates several key message flow patterns within the ColorVision application.

### 5.1 Command-Response Pattern

```mermaid
sequenceDiagram
  participant Client
  participant MQTT
  participant Service

  Client->MQTT: Publish Command Message
  Note over Client,MQTT: Topic: scgd/service/id/command
  MQTT->Service: Deliver Command Message
  Service->MQTT: Publish Status Message (Processing)
  Note over Service: Topic: scgd/service/id/status
  MQTT->Client: Deliver Status Message
  Note over Service: Process Command
  Service->MQTT: Publish Result Message
  Note over Service: Topic: scgd/service/id/result
  MQTT->Client: Deliver Result Message
  Service->MQTT: Publish Status Message (Complete)
  Note over Service: Topic: scgd/service/id/status
  MQTT->Client: Deliver Status Message
```

Sources: DLL/MQTTMessageLib.dll, DLL/FlowEngineLib.dll

### 5.2 Flow Engine Integration

The Flow Engine uses MQTT to orchestrate complex workflows involving multiple services:

```mermaid
sequenceDiagram
  participant FE
  participant MQTT
  participant Cam
  participant Alg

  FE->MQTT: Publish Capture Command
  Note over FE,MQTT: Topic: scgd/camera/id/command
  MQTT->Cam: Deliver Capture Command
  Cam->MQTT: Publish Status (Capturing)
  MQTT->FE: Deliver Status
  Cam->MQTT: Publish Image Data
  Note over Cam,Alg: Topic: scgd/camera/id/image
  MQTT->FE: Deliver Image Data
  FE->MQTT: Publish Process Command
  Note over FE,MQTT: Topic: scgd/algorithm/id/process
  MQTT->Alg: Deliver Process Command
  Alg->MQTT: Publish Status (Processing)
  MQTT->FE: Deliver Status
  Alg->MQTT: Publish Processing Result
  Note over Alg: Topic: scgd/algorithm/id/result
  MQTT->FE: Deliver Processing Result
```

Sources: DLL/FlowEngineLib.dll, DLL/MQTTMessageLib.dll

## 6. Client Implementation

### 6.1 MQTT Client Architecture

From analyzing the DLL files, the ColorVision system likely implements MQTT client functionality with following architecture:

```mermaid
flowchart TD

subgraph Application_Layer ["Application Layer"]
end

subgraph Service_Adapter_Layer ["Service Adapter Layer"]
end

subgraph MQTT_Client_Layer ["MQTT Client Layer"]
end

MQTTClient["MQTTClient"]
MQTTConfig["MQTTConfiguration"]
TopicManager["TopicManager"]
MessageSerializer["MessageSerializer"]
ServiceClient["ServiceClient"]
MessageHandlers["MessageHandlers"]
CommandFactory["CommandFactory"]
ServiceManager["ServiceManager"]
FlowNodes["Flow Nodes"]

    MQTTClient --> MQTTConfig
    MQTTClient --> TopicManager
    MQTTClient --> MessageSerializer
    ServiceClient --> MQTTClient
    ServiceClient --> MessageHandlers
    ServiceClient --> CommandFactory
    ServiceManager --> ServiceClient
    FlowNodes --> ServiceClient
```

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll

### 6.2 Connection Management

The MQTT clients in the ColorVision system implement several reliability features:

* Automatic reconnection when broker connections are interrupted
* Message queueing during disconnection periods
* Support for different Quality of Service (QoS) levels
* Connection status monitoring and reporting

## 7. Service Integration Examples

### 7.1 Camera Service Integration

```mermaid
flowchart TD

subgraph Client_Components ["Client Components"]
end

subgraph MQTT_Layer ["MQTT Layer"]
end

subgraph Camera_Service ["Camera Service"]
end

CameraDriver["CameraDriver"]
CameraClient["CameraClient"]
ImageProcessor["ImageProcessor"]
MQTTBroker["MQTT Broker"]
FlowEngine["Flow Engine"]
UI["User Interface"]

    CameraClient --> MQTTBroker
    CameraClient --> CameraDriver
    CameraDriver --> ImageProcessor
    ImageProcessor --> CameraClient
    FlowEngine --> MQTTBroker
    UI --> MQTTBroker
```

### 7.2 Algorithm Service Integration

```mermaid
flowchart TD

subgraph Client_Components ["Client Components"]
end

subgraph MQTT_Layer ["MQTT Layer"]
end

subgraph Algorithm_Service ["Algorithm Service"]
end

AlgorithmCore["AlgorithmCore"]
AlgorithmClient["AlgorithmClient"]
ResultsManager["ResultsManager"]
MQTTBroker["MQTT Broker"]
FlowEngine["Flow Engine"]
UI["User Interface"]

    AlgorithmClient --> MQTTBroker
    AlgorithmClient --> AlgorithmCore
    AlgorithmCore --> ResultsManager
    ResultsManager --> AlgorithmClient
    FlowEngine --> MQTTBroker
    UI --> MQTTBroker
```

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll, Engine/ColorVision.Engine/Templates/ARVR/Distortion/AlgResultDistortionDao.cs

## 8. Error Handling

The MQTT communication system includes mechanisms for handling errors at different levels:

| Error Type | Handling Mechanism |
| --- | --- |
| Connection Failures | Automatic reconnection with exponential backoff |
| Message Delivery Failures | Message persistence and retransmission based on QoS |
| Command Execution Errors | Error status messages with descriptive information |
| Service Errors | Dedicated error topics for detailed error reporting |

Sources: DLL/MQTTMessageLib.dll, DLL/CVCommCore.dll

## 9. Configuration

The MQTT communication system can be configured through various settings:

| Setting | Description | Typical Values |
| --- | --- | --- |
| Broker Address | Location of the MQTT broker | localhost, IP address, domain name |
| Port | Network port for the broker connection | 1883 (default), 8883 (TLS) |
| Client ID | Unique identifier for this client | Generated UUID or meaningful name |
| Username/Password | Authentication credentials | As configured on broker |
| QoS Level | Default quality of service | 0 (at most once), 1 (at least once), 2 (exactly once) |
| Reconnect Interval | Time between reconnection attempts | 1-30 seconds |
| Keep Alive | Time between ping messages | 15-60 seconds |

Sources: DLL/MQTTMessageLib.dll

## 10. Usage in Flow Engine

The Flow Engine system makes extensive use of MQTT communication to execute workflow steps across multiple services:

```mermaid
flowchart TD

subgraph Services ["Services"]
end

subgraph MQTT_Layer ["MQTT Layer"]
end

subgraph Flow_Engine ["Flow Engine"]
end

FlowEditor["Flow Editor"]
FlowExecutor["Flow Executor"]
NodeLibrary["Node Library"]
MQTTClient["MQTT Client"]
MQTTBroker["MQTT Broker"]
CameraService["Camera Service"]
MotorService["Motor Service"]
AlgorithmService["Algorithm Service"]

    FlowEditor --> FlowExecutor
    FlowExecutor --> NodeLibrary
    NodeLibrary --> MQTTClient
    MQTTClient --> MQTTBroker
    MQTTBroker --> CameraService
    MQTTBroker --> MotorService
    MQTTBroker --> AlgorithmService
```

Sources: DLL/FlowEngineLib.dll, DLL/MQTTMessageLib.dll

## Summary

The MQTT Communication system in ColorVision provides a flexible, reliable messaging infrastructure that enables loosely-coupled integration between various services and components. This architecture facilitates modular development, testing, and deployment of system components while ensuring consistent and reliable information exchange across the entire application.