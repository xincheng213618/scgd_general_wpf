---
knowledge_id: "ui.image-frames"
knowledge_type: "topic"
status: "current"
summary: "位图读取时借用原图内存与复制像素的区别、租约释放责任和缓存版本；原图修改须显式失效，复制HImage不延长租约。"
aliases: ["图像帧租约", "图像内存所有权", "像素缓存失效", "借用图像", "复制图像", "SourceImageFrame", "ImageFrameStore", "ImageFrameLease", "HImageExtension", "ForHImage", "ToHImage", "NotifySourcePixelsChanged"]
code_paths: ["UI/ColorVision.Core/SourceImageFrame.cs", "UI/ColorVision.Core/HImage.cs", "UI/ColorVision.Core/HImageExtension.cs", "UI/ColorVision.ImageEditor/ImageView.xaml.cs"]
test_paths: ["Test/ColorVision.UI.Tests/ImageFrameOwnerTests.cs", "Test/ColorVision.UI.Tests/HImageExtensionCopyTests.cs"]
related: ["ui.core", "ui.image-editor", "engine.native-integration", "algorithms.platform"]
---

# 源图像帧：租约、位图复制与缓存失效

本页维护 `ColorVision.Core` 的像素内存及 ImageView 取帧边界。**缓冲还活着、像素属于哪个版本、结果是否允许显示是三件事。** 租约防止受管理的帧在读者使用期间释放，不代表原始文件精度、任意指针安全、像素不可修改或算法调用自动取消。

`SourceImageFrame` 与 `ImageFrameStore` 是 Core 内部实现，`ImageFrameLease` 是公开读者句柄；ImageEditor 的使用入口是 `ImageView.AcquireImageFrame()`。不要把内部存储类型当作外部包的公开构造 API。`HImage` 的 Pack=8、释放标志和 native 函数族约定只在[native 集成](../../02-developer-guide/engine-development/opencv-integration.md)维护。

## 借用、复制与拥有者

| 取得方式 | 像素与释放责任 | 生命周期边界 |
| --- | --- | --- |
| `WriteableBitmap.ForHImage()` | 直接描述原 `BackBuffer`，`isDispose=true`；不复制、不取得所有权 | 方法本身不 Lock。调用者必须维持位图所有者、适当锁定及线程约束，不能把描述符独自交给晚到任务 |
| `WriteableBitmap.ToHImage()` | 用 `AllocCoTaskMem` 创建独立、紧密排列的拥有型副本 | 可变位图在 Lock/Unlock 内逐行复制；冻结位图用 CopyPixels。调用者释放副本一次，后续原位图修改不会自动更新该副本 |
| `ImageFrameLease.Image` | 每次返回同一帧像素的借用 `HImage` 描述符，不额外复制像素 | 租约保活底层存储；复制这个描述符不增加引用计数，不能在租约结束后继续访问旧指针 |

`ToHImage()` 复制像素字节，不统一颜色语义：不交换 Rgb24/Rgb48 通道、不展开 Indexed8 调色板，也不反预乘 Pbgra32。完整格式归一化属于[统一算法输入契约](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md)，不能因获取了租约或成功复制，就把直接 native 输入视为规范化测量数据。

## 帧失效后为什么旧租约还能读

`SourceImageFrame` 接收拥有型、非空且布局基本有效的 `HImage`，将释放责任交给 `SharedImageStorage`。它并不验证指针的实际分配容量，也不深拷贝输入；移交后，原调用者不能再自行释放同一拥有型 HImage。租约不能防止违反该约定造成的提前释放。

存储初始持有一个 owner 引用，每份独立取得的租约再加一个引用。帧/store 的 Dispose 或 Invalidate 撤掉 owner 引用，仍有租约时不会释放像素；最后一个引用退出才调用释放回调。由此可同时存在旧 revision 的在途读者与新 revision 的当前帧，而不是换图时强制终止旧读者。

- `lease.Dispose()` 幂等，只释放这一份租约的引用；其它租约及当前 store 仍可保活像素。将同一个 lease 对象赋给第二个变量，不是取得第二份租约。
- Dispose 后再次访问 `Image`、`Width` 或 `Height` 抛 `ObjectDisposedException`；`Revision` 是保留的只读值，仍可用于记录版本。
- 对 `lease.Image` 的借用副本调用 `HImage.Dispose()` 不释放帧像素，也不释放 lease；必须释放租约本身。
- 所有读者共享实际像素，借用描述符仍暴露指针，类型没有写保护。调用者不得把它当可独立修改的输出缓冲；需要修改时使用独立输出或按所属算法的输入所有权契约处理。

后台调用应持有 lease 直到该次像素读取/native 调用或异步复制真正结束，再释放。不要提前复制 `lease.Image` 后立即释放 lease，也不要在另一个线程仍使用同一 lease/指针时将其 Dispose。终结器只是兜底，不能用来替代明确的调用期所有权。

## revision 不是像素内容监视器

`ImageFrameStore.AcquireOrCreate()` 在已有当前帧时直接取得它的租约，不重新调用像素工厂。首次创建在锁外执行工厂，回锁后竞争发布；竞争失败的拥有型候选会释放。创建过程中 revision 已失效且没有可复用当前帧时返回 null，并释放候选，不把过期候选发布为当前帧。

`Invalidate()` 显式推进 revision、取下当前帧并释放其 owner 引用；`Dispose()` 还关闭 store，此后取得帧会抛异常、`IsCurrent` 返回 false。`IsCurrent(revision)` 只比较这个 store 的版本与存活状态，不读取/哈希像素，不证明当前已有一帧，也不比较不同文档或不同调用。

ImageView 的取帧工厂优先读 `ViewBitmapSource`，其次 `ImageShow.Source`；只有 `WriteableBitmap` 才以 `ToHImage()` 创建缓存副本，不重新读取磁盘。取帧入口会调度到该 ImageView 的 Dispatcher。旧租约因此是旧副本：原位图换掉或改写，不会自动改掉它的像素。

原地改写同一张位图后，宿主须经 `NotifySourcePixelsChanged()` 通知失效；只改字节、只赋值 `ViewBitmapSource` 或调用 Core 的位图复制工具，不会由 store 自动识别为新版本。`SetImageSource`、`Clear` 和显式像素变更入口怎样联动算法会话、预览与 overlay，统一见[文档变更边界](../../02-developer-guide/core-concepts/image-algorithm-platform-v1.md#m0-执行与所有权规则)。

发布结果前仍须核对当前宿主的 revision；统一算法路径还核对 `DocumentInstanceId` 与 `InvocationId`。保留租约只保证读取期帧存储持有的像素内存有效，不能用它证明结果仍属于当前图像，也不自动提供 latest-wins 或取消。

## 位图转换方法不会统一接管输入

`HImageExtension` 的输入均按值传递。即使内部 Dispose 清零了局部指针，也不会清零调用者手里的拥有型副本；在调用后再释放同一拥有型指针可能双重释放。

| 方法 | 当前输入释放行为 |
| --- | --- |
| `ToWriteableBitmap` / `ToWriteableBitmapAsync` | 复制到新位图，不释放输入 HImage |
| `ToWriteableBitmapAndDispose` | finally 中 Dispose 输入副本，包括转换失败；不能随后再释放原拥有型副本 |
| `UpdateWriteableBitmap` | 成功复制并解锁后 Dispose 输入副本；返回 false 或此前抛异常时没有同样的释放保证 |
| `UpdateWriteableBitmapAsync` | 验证失败返回 false 不释放；进入复制阶段后在 finally 先向 Dispatcher 标脏/解锁，再 Dispose。Dispatcher 清理自身抛异常仍可能阻止后续 Dispose |

需要统一的调用方所有权时，可保留唯一 owner，只将 `isDispose=true` 的借用描述符交给复制方法，并在所有读取完成后的 finally 释放 owner；租约输入则让 lease 覆盖整个等待期。不要将任意外部指针伪装成可释放的拥有型缓冲，也不要只看方法名猜释放责任。

这里的 Copy 与格式/stride 检查也不证明任意 WPF 线程调用安全。`UpdateWriteableBitmapAsync` 显式调度目标位图的 Dispatcher；`ToWriteableBitmapAsync` 在调用线程创建/锁定位图，再异步复制，不能将二者当作相同的调度封装。

## 测试证据与缺口

`ImageFrameOwnerTests` 覆盖最后租约释放、重复 Dispose、借用副本、revision 失效、候选竞争、并发取得/失效/关闭。多数用例使用合成指针和释放计数，不解引用真实 native 图像；另有小 WPF 位图切换及原地修改用例，测试主动调用 Invalidate，不证明修改字节会被自动监听。

`HImageExtensionCopyTests` 覆盖冻结/可变位图复制、紧密/带 padding 的行、Bgr32 四通道校验。用例使用借用输入自行清理，不验证全部拥有型输入的成功/失败释放分支，也不覆盖任意格式的色彩正确性、Dispatcher 故障或完整 ImageView 交互。

测试存在不表示本次运行过；这些托管/合成检查不能替代 native ABI、真实 DLL、GPU、相机或发布包验收。文档校验不授权加载真实图像样本、修改文件、触发设备或发布 DLL。
