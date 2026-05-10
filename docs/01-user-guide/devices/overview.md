# 设备服务概览

本页作为设备章节入口，优先回答“有哪些设备页可看、通常怎么配置、遇到问题先查哪里”。

## 设备服务是什么

在 ColorVision 里，设备通常以“服务”的形式被管理。主程序会维护一个设备服务列表，用户在设备窗口中查看、配置、启用和操作这些服务。

设备相关实现主要位于：

- `Engine/ColorVision.Engine/Services/`
- `Engine/ColorVision.Engine/Services/Devices/`

当前代码目录中可以看到的典型设备分类包括：

- Camera
- Calibration
- Motor
- FileServer
- FlowDevice
- Sensor
- SMU
- Spectrum

## 本章节怎么读

### 通用入口

- [添加与配置设备](./configuration.md)

### 具体设备

- [相机服务](./camera.md)
- [相机管理](./camera-management.md)
- [相机参数配置](./camera-configuration.md)
- [校准服务](./calibration.md)
- [电机服务](./motor.md)
- [SMU 服务](./smu.md)
- [流程设备服务](./flow-device.md)
- [文件服务器](./file-server.md)

## 常见使用顺序

1. 先看 [添加与配置设备](./configuration.md)，了解新增和保存设备的基本流程。
2. 再进入具体设备页，确认该设备有哪些参数和操作。
3. 如果涉及相机，继续看 [相机管理](./camera-management.md) 和 [相机参数配置](./camera-configuration.md)。
4. 如果需要让设备参与自动化流程，再看 [工作流程概览](../workflow/README.md)。

## 使用时通常会遇到什么

- 一个设备服务可能绑定真实硬件，也可能只是某类通信或文件型服务。
- 设备列表顺序、启用状态和配置内容通常会影响后续窗口和流程里的可见性。
- 某些设备除了基础配置，还会有独立的物理设备管理、标定或参数配置页。

## 排查建议

### 设备没有出现在列表里

- 先确认是否已经在设备配置窗口中创建并保存。
- 再确认对应设备依赖是否已经满足，例如物理硬件、驱动或通信环境。

### 设备出现了，但无法工作

- 优先检查该设备专题页里的参数说明。
- 再检查日志和连接状态。
- 若是流程里调用失败，再联动查看 [流程执行与调试](../workflow/execution.md)。

## 说明

- 本页只做入口和使用路径说明，不再承担设备服务代码分析。
- 设备实现细节以 `Engine/ColorVision.Engine/Services/` 下的实际代码为准。

