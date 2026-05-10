# cvColorVision

本页只描述当前仓库里真实可用的 `cvColorVision` 模块，不再继续维护“功能宣传 + 大量虚构示例 + 纯托管算法库”式旧稿。

## 先看这个模块现在是什么

按当前源码状态，`cvColorVision` 不是一个主要靠 C# 实现业务算法的模块，而是一层很厚的原生互操作桥。它当前最核心的角色是：

- 通过 `DllImport` 把 `cvCamera.dll`、`cvOled.dll` 的能力暴露给 C#。
- 把相机、色彩、图卡、源表、OLED 算法等底层接口集中到统一命名空间里。
- 给 `ColorVision.Engine`、插件和设备服务提供薄包装调用面。

因此它更接近“原生能力绑定层”，而不是旧文档里那种纯托管视觉框架。

## 当前最关键的文件

- `Engine/cvColorVision/cvCameraCSLib.cs`
- `Engine/cvColorVision/ConvertXYZ.cs`
- `Engine/cvColorVision/CvOledDLL.cs`
- `Engine/cvColorVision/PG.cs`
- `Engine/cvColorVision/PassSx.cs`
- `Engine/cvColorVision/Algorithms.cs`

如果只是想弄清模块如何对接底层 DLL、当前暴露了哪些能力，这几处代码已经覆盖主体。

## 当前控制面怎么分块

### 相机与通用视觉接口

`cvCameraCSLib.cs` 是当前最大的绑定面。按代码看，它覆盖的并不只是相机开关，而是大量原生入口的总集合，包括：

- 相机打开、关闭、实时预览、取帧
- 配置 JSON 读写
- 自动曝光、ROI、回调注册
- XYZ/xy/uv/CCT/Wave 采样
- TIFF 导出与数据拆分/合并
- 自动对焦、镜头位置、Canon 相关控制
- 各类视觉检测与图像处理函数

因此这不是一个只有几十个相机 API 的小封装，而是当前最密集的 P/Invoke 汇聚点。

### 色彩与色度采样

`ConvertXYZ.cs` 进一步把 `cvCamera.dll` 的 XYZ 相关入口拆成更聚焦的绑定面，当前主要围绕：

- XYZ 缓冲初始化与释放
- Circle / Rect 区域采样
- xyz、uv、CCT、主波长等导出
- 批量点位采样

这说明当前色彩采样链并不是独立 C# 计算器，而是围绕原生缓冲和采样函数运行。

### OLED 专用算法

`CvOledDLL.cs` 当前专门绑定 `cvOled.dll`，提供：

- 参数加载
- 图片读入
- 像素点查找
- 像素重建
- 摩尔纹滤波

因此 OLED 相关能力当前是单独 DLL 面，而不是混在相机接口内部实现。

### 图卡与外设接口

`PG.cs` 当前是图卡设备控制的薄包装，提供：

- PG 初始化
- TCP/串口连接
- Start / Stop / Reset
- 上下切换与指定帧切换

`PassSx.cs` 则提供源表/电源侧的原生调用包装，覆盖：

- 打开和关闭设备
- 设置源模式
- 设置 2 线 / 4 线与前后端口
- 读取电压电流
- 执行步进和扫描

这说明 `cvColorVision` 当前不是只有图像处理，也承接了多类外设的底层绑定。

### 极薄的算法入口

`Algorithms.cs` 这种文件展示了模块的另一个特点：有些封装非常薄，只是把单个底层函数以最直接的形式暴露出来。

所以这一层的职责不是统一设计所有 API 风格，而是尽可能把底层能力完整映射进来。

## 当前几个最容易写错的点

### 它不是纯 C# 算法中心

当前大多数关键能力都来自原生 DLL，C# 代码主要负责声明、少量辅助包装以及数据类型桥接。继续把它写成“主要算法实现在托管层”，会和真实代码结构相反。

### `cvCameraCSLib` 不是只管 camera

文件名容易让人误判，但当前它实际还暴露了很多色彩采样、图像处理、自动对焦和检测函数，是总绑定入口之一。

### 这里的接口粒度并不统一

有些文件像 `cvCameraCSLib.cs` 非常厚，有些像 `Algorithms.cs`、`PG.cs`、`CvOledDLL.cs` 非常薄。文档不应该再把它们硬写成一个整齐划一的分层 API 体系。

### 它更像“被上层调用”的基础层

当前 `ColorVision.Engine`、设备服务和部分插件会调用这里暴露的原生接口；`cvColorVision` 自己并不负责宿主级窗口、模板或工作流编排。

## 推荐阅读顺序

1. `Engine/cvColorVision/cvCameraCSLib.cs`
2. `Engine/cvColorVision/ConvertXYZ.cs`
3. `Engine/cvColorVision/CvOledDLL.cs`
4. `Engine/cvColorVision/PG.cs`
5. `Engine/cvColorVision/PassSx.cs`

这样能先看最厚的总绑定面，再往 OLED、图卡和源表这些专用接口扩展。

## 继续阅读

- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)
- [docs/04-api-reference/engine-components/ColorVision.FileIO.md](./ColorVision.FileIO.md)