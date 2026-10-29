---
knowledge_id: "ui.debounce-timer"
knowledge_type: "topic"
status: "current"
summary: "共享防抖的同键替换、回调所有权、Dispatcher 排队取消与可复用清理边界。"
aliases: ["DebounceTimer", "按键防抖", "防抖取消", "Dispatcher防抖", "ElapsedEventHandler"]
code_paths: ["UI/ColorVision.Common/Utilities/DebounceTimer.cs", "UI/ColorVision.Common/Utilities/DebounceScheduler.cs"]
test_paths: ["Test/ColorVision.UI.Tests/DebounceTimerTests.cs"]
related: ["ui.common"]
---

# 共享防抖与回调生命周期

`ColorVision.Common.Utilities.DebounceTimer` 按字符串 `actionType` 合并短时间内的重复请求：同一键只保留最后一次成功登记的动作，等待其 `delayMilliseconds` 后执行一次。它适用于延迟刷新、参数保存等允许被后续请求替换的操作，不是周期任务或异步任务管理器。

静态入口在进程内共享键空间。不同窗口、对象或用途应使用可区分的键；否则其中一个调用者的更新或取消会影响另一个调用者。窗口结束时用 `Cancel` 撤销自己持有的键；只有需要清空全部共享防抖时才调用 `DisposeAllTimers`。

## 执行线程与兼容入口

| 入口 | 回调契约 |
| --- | --- |
| `AddOrResetTimer` 的 `Action`、`Action<T>`、`Action<T,T1>` 重载 | 在计时器回调线程同步执行委托，参数取自本次登记；需要 UI 访问时由调用方选择 Dispatcher 入口或自行调度 |
| `AddOrResetTimer` 的 `ElapsedEventHandler` 重载 | 保留真实 `System.Timers.Timer` 作为 sender，并转交其真实 `ElapsedEventArgs`，包括 `SignalTime`；不是合成事件参数 |
| `AddOrResetTimerDispatcher` | 到期后向 `Application.Current.Dispatcher` 排队，轮到该操作且登记仍有效时才执行动作；计时器到期不代表 UI 动作已经开始或完成 |
| `Cancel(actionType)` | 撤销当前键的登记并释放计时器；键不存在时可重复调用 |
| `DisposeAllTimers()` | 撤销清理时已有的全部登记并释放其资源；不是永久关闭服务，后续新增仍可正常执行 |

公开重载和签名保持兼容。计时器沿用 `System.Timers.Timer` 的时间间隔约束；新计时器构造或启动失败时异常返回调用方，原有登记继续有效，不会因一次失败的替换而丢失。回调资源的释放不依赖用户委托正常返回；这不构成对用户异常的统一报告或处理机制。

## 取消、替换与正在执行的动作

每次登记具有独立所有权。被替换或取消的旧计时器，即使其到期事件已经排队、随后仍然到达，也不能执行旧动作或移除同键的新计时器。用户动作中再次为同键登记时，原回调结束或抛异常后的清理也只处理原登记，新登记继续有效。

Dispatcher 的登记保留到 UI 动作开始前。到期后已进入 Dispatcher 队列的动作，仍可被 `Cancel`、同键替换或 `DisposeAllTimers` 撤销；队列随后处理旧操作时会跳过用户动作。没有 `Application`、Dispatcher 已开始关闭，或排队操作被 Abort 时，放弃本次动作并清理登记与计时器，不回退到后台线程执行 UI 动作。

撤销只阻止尚未开始的用户动作，不能中断已经开始执行的委托，也不等待其结束。同键旧动作已经开始后再登记新动作，两次用户动作可能在各自线程上重叠；调用方仍须管理业务对象、重入、窗口关闭后的状态及异步任务寿命。普通计时器动作不会持有调度器的全局锁执行，因此某一键的慢动作不应阻塞其它键的登记或回调。

## 验证入口与边界

`Test/ColorVision.UI.Tests/DebounceTimerTests.cs` 通过可手动触发的计时器检查同键替换、迟到回调、重入登记、失败清理和清空后的复用，通过真实 WPF Dispatcher 检查排队后的取消与操作放弃，并用真实计时器检查 sender 和 `ElapsedEventArgs` 兼容性。测试关联不表示本次已运行或通过，也不代表真实客户窗口的关闭、业务动作或所有线程交错都已验收。

在 Windows 仓库根目录运行以下定向测试；它不启动主程序或操作设备：

```powershell
dotnet test .\Test\ColorVision.UI.Tests\ColorVision.UI.Tests.csproj -p:Platform=x64 --filter FullyQualifiedName~DebounceTimerTests
```
