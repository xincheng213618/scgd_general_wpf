# cvColorVision

本頁只描述當前倉庫裡真實可用的 `cvColorVision` 模組，不再繼續維護“功能宣傳 + 大量虛構示例 + 純託管演算法庫”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，`cvColorVision` 不是一個主要靠 C# 實現業務演算法的模組，而是一層很厚的原生互操作橋。它當前最核心的角色是：

- 透過 `DllImport` 把 `cvCamera.dll`、`cvOled.dll` 的能力暴露給 C#。
- 把相機、色彩、圖卡、源表、OLED 演算法等底層介面集中到統一名稱空間裡。
- 給 `ColorVision.Engine`、外掛和裝置服務提供薄包裝呼叫面。

因此它更接近“原生能力繫結層”，而不是舊文件裡那種純託管視覺框架。

## 當前最關鍵的檔案

- `Engine/cvColorVision/cvCameraCSLib.cs`
- `Engine/cvColorVision/ConvertXYZ.cs`
- `Engine/cvColorVision/CvOledDLL.cs`
- `Engine/cvColorVision/PG.cs`
- `Engine/cvColorVision/PassSx.cs`
- `Engine/cvColorVision/Algorithms.cs`

如果只是想弄清模組如何對接底層 DLL、當前暴露了哪些能力，這幾處程式碼已經覆蓋主體。

## 當前控制面怎麼分塊

### 相機與通用視覺介面

`cvCameraCSLib.cs` 是當前最大的繫結面。按程式碼看，它覆蓋的並不只是相機開關，而是大量原生入口的總集合，包括：

- 相機開啟、關閉、實時預覽、取幀
- 配置 JSON 讀寫
- 自動曝光、ROI、回撥註冊
- XYZ/xy/uv/CCT/Wave 取樣
- TIFF 匯出與資料拆分/合併
- 自動對焦、鏡頭位置、Canon 相關控制
- 各類視覺檢測與影像處理函式

因此這不是一個只有幾十個相機 API 的小封裝，而是當前最密集的 P/Invoke 匯聚點。

### 色彩與色度取樣

`ConvertXYZ.cs` 進一步把 `cvCamera.dll` 的 XYZ 相關入口拆成更聚焦的繫結面，當前主要圍繞：

- XYZ 緩衝初始化與釋放
- Circle / Rect 區域取樣
- xyz、uv、CCT、主波長等匯出
- 批次點位取樣

這說明當前色彩取樣鏈並不是獨立 C# 計算器，而是圍繞原生緩衝和取樣函式執行。

### OLED 專用演算法

`CvOledDLL.cs` 當前專門繫結 `cvOled.dll`，提供：

- 參數載入
- 圖片讀入
- 畫素點查詢
- 畫素重建
- 摩爾紋濾波

因此 OLED 相關能力當前是單獨 DLL 面，而不是混在相機介面內部實現。

### 圖卡與外設介面

`PG.cs` 當前是圖卡裝置控制的薄包裝，提供：

- PG 初始化
- TCP/串列埠連線
- Start / Stop / Reset
- 上下切換與指定幀切換

`PassSx.cs` 則提供源表/電源側的原生呼叫包裝，覆蓋：

- 開啟和關閉裝置
- 設定源模式
- 設定 2 線 / 4 線與前後埠
- 讀取電壓電流
- 執行步進和掃描

這說明 `cvColorVision` 當前不是隻有影像處理，也承接了多類外設的底層繫結。

### 極薄的演算法入口

`Algorithms.cs` 這種檔案展示了模組的另一個特點：有些封裝非常薄，只是把單個底層函式以最直接的形式暴露出來。

所以這一層的職責不是統一設計所有 API 風格，而是儘可能把底層能力完整對映進來。

## 當前幾個最容易寫錯的點

### 它不是純 C# 演算法中心

當前大多數關鍵能力都來自原生 DLL，C# 程式碼主要負責宣告、少量輔助包裝以及資料型別橋接。繼續把它寫成“主要演算法實現在託管層”，會和真實程式碼結構相反。

### `cvCameraCSLib` 不是隻管 camera

檔名容易讓人誤判，但當前它實際還暴露了很多色彩取樣、影像處理、自動對焦和檢測函式，是總繫結入口之一。

### 這裡的介面粒度並不統一

有些檔案像 `cvCameraCSLib.cs` 非常厚，有些像 `Algorithms.cs`、`PG.cs`、`CvOledDLL.cs` 非常薄。文件不應該再把它們硬寫成一個整齊劃一的分層 API 體系。

### 它更像“被上層呼叫”的基礎層

當前 `ColorVision.Engine`、裝置服務和部分外掛會呼叫這裡暴露的原生介面；`cvColorVision` 自己並不負責宿主級視窗、模板或工作流編排。

## 推薦閱讀順序

1. `Engine/cvColorVision/cvCameraCSLib.cs`
2. `Engine/cvColorVision/ConvertXYZ.cs`
3. `Engine/cvColorVision/CvOledDLL.cs`
4. `Engine/cvColorVision/PG.cs`
5. `Engine/cvColorVision/PassSx.cs`

這樣能先看最厚的總繫結面，再往 OLED、圖卡和源表這些專用介面擴充套件。

## 繼續閱讀

- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)
- [docs/04-api-reference/engine-components/ColorVision.FileIO.md](./ColorVision.FileIO.md)