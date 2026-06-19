# 使用手册

本章节面向操作者、测试工程师和现场交付人员，按“安装启动 -> 界面操作 -> 设备与流程 -> 数据与排查”的顺序整理。实现细节、DLL 发布和插件开发不放在这里，避免使用手册变成源码手册。

## 章节入口

### 按操作目标查找

- [使用手册操作工作流矩阵](./operation-workflow-matrix.md)
- [现场操作验收清单](./field-operation-acceptance.md)

### 安装与首次使用

- [系统要求](../00-getting-started/prerequisites.md)
- [安装指南](../00-getting-started/installation.md)
- [首次运行](../00-getting-started/first-steps.md)
- [快速上手](../00-getting-started/quick-start.md)

### 界面与基础交互

- [主窗口导览](./interface/main-window.md)
- [UI 组件使用手册](./interface/ui-component-handbook.md)
- [属性编辑器](./interface/property-editor.md)
- [日志查看器](./interface/log-viewer.md)
- [终端](./interface/terminal.md)

### 图像编辑器

- [图像编辑器概览](./image-editor/overview.md)

### 设备管理

- [设备服务概览](./devices/overview.md)
- [添加与配置设备](./devices/configuration.md)
- [相机服务](./devices/camera.md)
- [相机管理](./devices/camera-management.md)
- [相机参数配置](./devices/camera-configuration.md)
- [校准服务](./devices/calibration.md)
- [电机服务](./devices/motor.md)
- [SMU 服务](./devices/smu.md)
- [流程设备服务](./devices/flow-device.md)
- [文件服务器](./devices/file-server.md)

### 工作流程

- [工作流程概览](./workflow/README.md)
- [流程设计](./workflow/design.md)
- [流程执行与调试](./workflow/execution.md)

### 数据管理

- [数据管理概览](./data-management/README.md)
- [数据库操作](./data-management/database.md)
- [数据导出与导入](./data-management/export-import.md)

### 故障排查

- [常见问题](./troubleshooting/common-issues.md)

## 推荐阅读路线

1. 不确定该看哪页时，先看 [使用手册操作工作流矩阵](./operation-workflow-matrix.md)。
2. 第一次部署先看 [安装指南](../00-getting-started/installation.md) 和 [首次运行](../00-getting-started/first-steps.md)。
3. 现场交付或复测时，按 [现场操作验收清单](./field-operation-acceptance.md) 逐项确认。
4. 日常操作先看 [主窗口导览](./interface/main-window.md)，了解主界面布局。
5. 再看 [UI 组件使用手册](./interface/ui-component-handbook.md)，按窗口和控件确认入口、完成标准和排查方向。
6. 继续看 [属性编辑器](./interface/property-editor.md) 和 [图像编辑器概览](./image-editor/overview.md)，建立基本操作路径。
7. 涉及硬件时进入 [设备服务概览](./devices/overview.md) 和对应设备专题页。
8. 需要自动化时进入 [工作流程概览](./workflow/README.md)。
9. 遇到异常先查 [常见问题](./troubleshooting/common-issues.md)。

## 章节边界

- 偏实现和扩展机制的内容已经移到 [开发手册](../02-developer-guide/README.md)。
- 偏类库、接口和模块级说明的内容已经移到 [模块参考](../04-api-reference/README.md)。
- 需要整体理解系统设计时，直接进入 [架构设计](../03-architecture/README.md)。
