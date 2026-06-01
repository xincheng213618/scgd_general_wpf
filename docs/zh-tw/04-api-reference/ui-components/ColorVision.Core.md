# ColorVision.Core

本頁只描述 UI/ColorVision.Core 當前已經落地的原生互操作層，不再延續舊文件裡那種“高層影像 API 手冊”和並不存在的託管方法示例。

## 模組定位

ColorVision.Core 當前更接近一個原生影像和影片能力橋接層，主要負責：

- 定義 `HImage` 這類跨託管/非託管邊界的資料結構
- 透過 P/Invoke 呼叫 `opencv_helper.dll`、`opencv_cuda.dll`
- 提供 WPF 側的點陣圖轉換與更新輔助
- 暴露偽彩色、影像增強、聚焦評價、影片相關等原生入口

它不是一個已經封裝好的高層影像處理框架。很多能力當前仍然是 `extern` 方法級別的原生匯出包裝。

## 當前最關鍵的檔案

從專案目錄看，最值得優先閱讀的是：

- `HImage.cs`：影像資料結構
- `HImageExtension.cs`：`HImage` 與 WPF 影像物件之間的橋接
- `OpenCVMediaHelper.cs`：主要的原生匯出包裝集合
- `OpenCVCuda.cs`：CUDA 相關原生入口
- `ColormapTypes.cs`：偽彩色列舉
- `NativeLogBridge.cs`：原生日誌橋接
- `nvcuda.cs`：CUDA 相關 P/Invoke 定義

## 關鍵入口型別

### HImage

`HImage` 當前不是舊文件裡那種帶大量例項方法的託管類，而是一個承載原生影像緩衝區的結構體。它的核心欄位包括：

- `rows`
- `cols`
- `channels`
- `depth`
- `stride`
- `pData`

同時它實現了 `Dispose()`，負責釋放 `Marshal.AllocHGlobal` 分配的影像記憶體。

這意味著當前模組最重要的職責之一，就是安全地在原生和託管邊界上傳遞影像緩衝區。

### HImageExtension

`HImageExtension` 提供的是橋接輔助，而不是完整處理演算法庫。它當前主要負責：

- 根據通道數和位深推導 `PixelFormat`
- 把 `HImage` 內容複製到 `WriteableBitmap`
- 提供非同步點陣圖更新路徑
- 協助把原生影像資料轉換成 WPF 可顯示物件

因此它的價值主要在顯示鏈，而不是演算法鏈。

### OpenCVMediaHelper

雖然名字叫 `OpenCVMediaHelper`，當前它其實承載了大量 `opencv_helper.dll` 的匯出包裝，不只是影片相關介面，還包括：

- 偽彩色與自動範圍偽彩色
- 最小值/最大值提取
- 自動亮度、自動顏色、自動色調
- 通道提取
- 亮度對比度、Gamma、反相、閾值、銳化、濾波、邊緣檢測
- SFR 與聚焦評價
- 若干識別或檢測類入口
- 影片相關結構和函式

所以當前更準確的理解是：它是主要的原生影像能力匯出面，而不只是“影片幫助類”。

### OpenCVCuda

`OpenCVCuda` 當前並不是舊文件裡聲稱的通用 CUDA 裝置管理層。它現在公開的是少量 `opencv_cuda.dll` 匯出，重點在融合相關入口，例如：

- `CM_Fusion`
- `CM_Fusion_Async`
- `CM_Fusion_Batch`

因此描述 CUDA 能力時，應按當前實際匯出寫，不要再擴寫成完整 GPU 能力總入口。

### ColormapTypes 與 NativeLogBridge

- `ColormapTypes` 負責統一偽彩色對映列舉。
- `NativeLogBridge` 負責把原生側日誌橋接到託管日誌系統。

這兩個檔案都很小，但它們分別是偽彩色鏈和除錯鏈的重要邊界點。

## 當前執行時主鏈

這套模組當前更像下面這條鏈：

1. 上層模組透過 P/Invoke 呼叫 `OpenCVMediaHelper` 或 `OpenCVCuda`。
2. 原生 DLL 返回 `HImage` 或寫入 `HImage` 輸出參數。
3. WPF 顯示鏈透過 `HImageExtension` 把影像資料更新到 `WriteableBitmap`。
4. 像 `ColorVision.ImageEditor` 這類上層模組繼續圍繞這些點陣圖做互動、繪製和顯示。

## 當前實現有哪些邊界

### 不要把它寫成高層 OO API

當前程式碼裡並沒有舊文件寫的這些典型高層介面：

- `HImage.Load(...)`
- `HImage.ToBitmapSource()`
- `OpenCVCuda.GetCudaDeviceCount()`
- `OpenCVCuda.IsCudaAvailable()`

這些寫法會誤導讀者去尋找並不存在的託管封裝。

### HImage 的資源語義很重要

`HImage` 不是普通託管物件，包含非託管指標和顯式釋放邏輯。討論這個模組時，記憶體和所有權邊界比“類設計”更重要。

### 上層業務語義不在這裡

Core 只負責橋接原生能力，不負責像 ImageEditor 那樣的工具欄、互動或文件狀態編排。閱讀時應明確它只是下層能力底座。

## 當前更適合怎樣讀這個模組

### 想看影像資料結構和顯示橋接

先看：

- `HImage.cs`
- `HImageExtension.cs`

### 想看原生匯出面

先看：

- `OpenCVMediaHelper.cs`
- `OpenCVCuda.cs`

### 想看偽彩色和日誌邊界

先看：

- `ColormapTypes.cs`
- `NativeLogBridge.cs`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 不存在的託管高層方法示例
- 把 `OpenCVCuda` 寫成完整裝置管理層
- 大段更新日誌和版本清單
- 把 Core 說成完整上層影像處理框架

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.ImageEditor](./ColorVision.ImageEditor.md)