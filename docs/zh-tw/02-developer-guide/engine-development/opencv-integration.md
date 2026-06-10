# OpenCV 和 native 整合開發交接手冊

本頁說明目前倉庫中 OpenCV/native 能力的真實邊界。Engine 側有 `cvColorVision` 這類設備 SDK / 演算法 DLL 綁定層，UI/Core 側有 `opencv_helper.dll` / `opencv_cuda.dll` 的 P/Invoke 包裝，文件開啟鏈路還包含 `.cvraw` / `.cvcie` 解析和縮圖。

## 目前分層

| 層級 | 目錄或文件 | 職責 |
| --- | --- | --- |
| 設備 SDK 綁定 | `Engine/cvColorVision/` | 相機、光譜儀、感測器、OLED 演算法、MQTTMessageLib 和 native DLL 入口 |
| UI/Core native 包裝 | `UI/ColorVision.Core/` | `HImage`、`OpenCVMediaHelper`、`OpenCVCuda`、`ImageCompute`、native 日誌橋 |
| 文件解析和展示 | `Engine/ColorVision.Engine/Media/` | `.cvraw`、`.cvcie` 開啟、縮圖、CIE 匯出和影像工具 |
| 測試工程 | `Test/opencv_helper_test/` | C++ 驗證工程，重點驗證 `M_FindLuminousArea` |

## 修改落點

| 需求 | 首選落點 |
| --- | --- |
| 新增相機、光譜儀、感測器 SDK 導出 | `Engine/cvColorVision/` |
| 新增給 WPF 呼叫的影像處理函式 | `UI/ColorVision.Core/OpenCVMediaHelper.cs` 或 `OpenCVCuda.cs` |
| 新增 `.cvraw` / `.cvcie` 開啟或縮圖行為 | `Engine/ColorVision.Engine/Media/` |
| 調整亮區、偽彩、SFR、白平衡 helper | native `opencv_helper.dll` 和 C# 簽名一起核對 |
| 調整 CUDA 融合 | `opencv_cuda.dll`、`OpenCVCuda`、`ImageCompute` |

## P/Invoke 規則

- C# 簽名必須和 native 導出一致，包括 calling convention、字串編碼、結構布局和記憶體釋放方式。
- `HImage` 帶 native buffer，呼叫失敗時要釋放已分配輸出。
- 返回 `IntPtr` 字串的 helper 要確認是否需要 `FreeResult()`。
- x64 是主要交付目標，native DLL、測試工程和主程式平台要一致。
- 不要把 `cvColorVision` 寫成純託管演算法庫，它主要是 native 綁定層和訊息資料型別集合。

## 驗證命令

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

## 相關文件

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [測試與驗證交接手冊](../testing.md)
