---
knowledge_id: "platform.kernel-driver"
knowledge_type: "topic"
status: "current"
summary: "ColorVisionDriver 实验性 WDM 驱动骨架的两个 IOCTL、WDK 构建输入与接入边界；尚未接入主程序、服务宿主或正式发布链。"
aliases: ["ColorVisionDriver","内核驱动","驱动骨架","IOCTL_CVDRV_PING","IOCTL_CVDRV_GET_VERSION","CVDRV_ABI_VERSION","CVDRV_PING_RESPONSE","CVDRV_VERSION_INFO"]
code_paths: ["Drivers/ColorVisionDriver","build.sln","scgd_general_wpf.sln"]
test_paths: []
related: ["platform.system","platform.service-host"]
---

# ColorVisionDriver：实验性内核驱动骨架

`Drivers/ColorVisionDriver/` 是独立的 Windows WDM 驱动工程，当前只实现通信探测和版本查询。它尚未加入主程序解决方案或正式构建、安装与发布流程，也没有连接 `ColorVisionServiceHost`、相机 SDK 或设备采集链。

本页描述已有源码，供评估和开发使用。普通 ColorVision 安装不需要手工安装此驱动；现有[本机权限代理](./service-host.md)通过自己的服务与命名管道工作。

## 已有设备对象和请求

`driver.c` 的 `DriverEntry` 创建一个命名设备对象及符号链接，并注册创建、关闭、设备控制和卸载处理器：

| 名称 | 值 |
| --- | --- |
| 内核设备对象 | `\Device\ColorVisionDriver` |
| DOS 符号链接 | `\DosDevices\ColorVisionDriver` |
| 用户态打开路径 | `\\.\ColorVisionDriver` |
| 设备类型 | `FILE_DEVICE_COLORVISION = 0x8000` |
| ABI 版本 | `CVDRV_ABI_VERSION = 1` |

接口和响应结构在 `public.h`。两个 IOCTL 均使用 `METHOD_BUFFERED` 和 `FILE_READ_DATA`，从 `Irp->AssociatedIrp.SystemBuffer` 写入响应，不消费输入业务参数。

| 控制码 | 输出结构与字段 |
| --- | --- |
| `IOCTL_CVDRV_PING` | `CVDRV_PING_RESPONSE`：`Signature = 0x43564452`，`AbiVersion = 1` |
| `IOCTL_CVDRV_GET_VERSION` | `CVDRV_VERSION_INFO`：ABI 版本和 `Major/Minor/Patch/Build`；当前源码返回 `0.1.0.0` |

调用方提供的输出空间至少应为对应结构的 `sizeof`。不足时返回 `STATUS_BUFFER_TOO_SMALL`，完成字节数为 0；成功时返回 `STATUS_SUCCESS` 和实际结构大小。未支持的控制码或其它 IRP 返回 `STATUS_INVALID_DEVICE_REQUEST`；创建、关闭请求直接成功。

创建符号链接失败时删除已创建的设备对象；正常卸载删除符号链接和设备对象。源码没有文件、注册表、网络或相机过滤逻辑，也没有厂商硬件 SDK 调用。两个 IOCTL 可用不等于硬件能力、访问控制或驱动稳定性已通过验证。

## 构建输入

在具备 Visual Studio C++、Windows SDK 和 WDK 的 Windows 开发环境中，从仓库根目录执行：

```powershell
msbuild .\Drivers\ColorVisionDriver\ColorVisionDriver.vcxproj /p:Configuration=Debug /p:Platform=x64
```

此命令构建本地驱动产物，不安装驱动。工程声明 `Debug|x64`、`Release|x64`，使用 `WindowsKernelModeDriver10.0` 工具集、`DriverType=WDM` 和 Windows 10 目标；编译与链接警告按错误处理。WDK 工具链、SDK 选择和签名环境须另行准备，仓库没有为该工程提供自动化构建验收结果。

`ColorVisionDriver.inf` 声明 `Root\ColorVisionDriver` 硬件 ID、`ColorVisionDriver.sys`、`ColorVisionDriver.cat` 以及按需启动的内核服务。INF 中的名称和版本是包输入，不能据此确认目录文件已生成、签名有效或设备已经创建。

## 安装与接入的验证范围

当前源码在 `DriverEntry` 直接创建设备对象，没有注册 `AddDevice` 或实现 PnP 请求处理，也没有随仓库提供调用两个 IOCTL 的用户态客户端。INF 的根枚举声明与这段驱动代码需要作为完整安装流程一起验证。

Microsoft 将 `pnputil /add-driver ... /install` 定义为向驱动存储添加包并安装到已有设备；它不是本项目从零创建设备、完成签名和证明驱动可用的完整步骤。见 [Microsoft PnPUtil 示例](https://learn.microsoft.com/en-us/windows-hardware/drivers/devtest/pnputil-examples)。

进一步接入前，应在专用测试虚拟机中验证工具链与签名、设备创建和卸载、两个 IOCTL 的正常及短缓冲响应、非法请求、访问控制和故障恢复，再设计宿主客户端与发布流程。当前没有相关自动化测试声明；主程序构建、文档检查或 INF 文件存在均不能替代这些驱动验证。
