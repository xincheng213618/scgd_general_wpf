# 本地相机内存帧预览：生命周期与显示语义

本文补充[方案总览](./local-camera-memory-preview.md)中的租约、显示模式和内存约束。

## 租约取得时机

推荐顺序：

1. 节点完成取图并设置 `frame.MasterId`。
2. Publisher 同步调用 `frame.Acquire()`。
3. Publisher 持有该 `LocalFlowFrameLease`。
4. UI 更新完成、请求被覆盖或被丢弃时释放租约。
5. 流程独立结束并释放根引用。

不要把裸 `LocalFlowFrame`、`IntPtr` 或延迟执行的 `Acquire()` 委托放进 Dispatcher 队列。

## 只保留最新预览

流程可能连续执行，UI Dispatcher 也可能暂时繁忙。预览队列采用 latest-wins：

- 新帧到来时原子替换尚未显示的旧请求。
- 被替换请求立即释放租约。
- 同一设备最多存在一个待显示请求。
- 不因预览积压阻塞相机取图或后续算法。
- View 未加载、不可见或关闭自动刷新时可跳过预览。

## RAW 与 CIE 分层

| 模式 | 行为 | 适用场景 |
| --- | --- | --- |
| `Off` | 不生成 UI 预览 | 无人值守或最低内存 |
| `Raw` | RAW 转为 `WriteableBitmap` | 默认预览 |
| `FullCie` | RAW 显示并挂载 CIE 数据 | 取点、伪彩和图层 |

RAW 显示可以复用 `CameraLocalWindow.ShowImageInView(...)` 的像素格式和通道转换逻辑，但应提取为独立 Presenter。

完整 CIE 预览应优先为 `CVRawOpen.AttachLiveCvcie(...)` 增加 `IntPtr` 入口，并复用 `ConvertXYZ.CM_SetBufferXYZ(..., IntPtr)`，避免先生成数百 MiB 的托管 `byte[]`。

应以目标分辨率测试后台构造并冻结 `BitmapSource` 与 UI 线程写入 `WriteableBitmap` 两种方式。无论采用哪一种，都应避免“非托管帧 → 大型托管数组 → WriteableBitmap”的重复复制。

## 文件与内存结果

| `SaveFiles` | 预览 | 当前显示 | 历史重新打开 |
| --- | --- | --- | --- |
| `false` | 关闭 | 不显示 | 不可用 |
| `false` | 开启 | 从内存显示 | 仅最新帧可用，重启后不可用 |
| `true` | 开启 | 从内存显示 | 后续通过文件重新打开 |

即使 `SaveFiles=true`，当前帧也不必等待磁盘写入后再读回。无文件结果可以保留数据库元数据和 `MasterId`，但 UI 应标记为“内存帧”，不能表现为普通的缺失文件。

第一阶段只更新当前图像，不支持从历史无文件结果行重新打开，避免让结果列表长期持有图像或租约。

## 内存预算

以 5544 × 3692、3 通道图像为例：

| 数据 | 估算大小 |
| --- | ---: |
| 16-bit RAW | 约 117 MiB |
| 32-bit CIE | 约 234 MiB |
| `Rgb48 WriteableBitmap` | 约 117 MiB |
| CIE 分析 native 缓冲 | 可能再占约 234 MiB |

流程帧、显示副本和完整 CIE 工具同时存活时，单帧相关峰值可能超过 700 MiB。因此：

- 禁止无界预览队列。
- 禁止在全局结果集合中保存帧租约。
- 默认优先考虑 `Raw` 模式。
- 新帧替换旧帧时主动清理旧的 `ImageView`/CIE 状态。
- 性能验证同时观察 Private Bytes、Working Set 和帧处理时间。
