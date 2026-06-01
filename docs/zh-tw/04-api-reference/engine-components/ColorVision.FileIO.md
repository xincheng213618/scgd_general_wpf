# ColorVision.FileIO

本頁只描述當前倉庫裡真實可用的 `ColorVision.FileIO` 模組，不再繼續維護“通用檔案框架 + JSON/YAML/批處理平台”式舊稿。

## 先看這個模組現在是什麼

按當前原始碼狀態，`ColorVision.FileIO` 不是一個泛化的檔案處理框架，而是一個圍繞 ColorVision 自訂影像檔案格式組織起來的專用 I/O 模組。當前最清晰的落點是：

- 識別和讀取 `CVCIE` / `CVRAW` / `CVSRC` 類檔案。
- 解析檔案頭、版本、曝光、尺寸、通道和原始資料區。
- 按通道或檔案型別開啟本地 ColorVision 影像檔案。
- 用 `CVCIEFile` 作為最核心的資料載體。

因此它比舊文件裡描述的“支援通用 JSON/XML/YAML/影像批處理”要窄得多，也更貼近當前實際程式碼。

## 當前最關鍵的檔案

- `Engine/ColorVision.FileIO/CVFileUtil.cs`
- `Engine/ColorVision.FileIO/CVCIEFile.cs`

至少從當前模組的可讀實現看，這兩處已經覆蓋了最核心的格式識別、頭部解析和資料載體定義。

## 當前真正處理的是什麼格式

`CVCIEFile.cs` 當前定義的 `CVType` 包括：

- `Raw`
- `Src`
- `CIE`
- `Calibration`
- `Tif`
- `Dat`

但從 `CVFileUtil` 當前最密集的實現來看，真正被重點展開的是 `CVCIE` / `CVRAW` 這一組 ColorVision 自訂影像檔案，而不是通用辦公或配置檔案格式。

## 當前讀取鏈怎麼工作

`CVFileUtil` 當前的核心流程大致是：

1. 用檔案頭判斷是不是 `CVCIE` 體系檔案。
2. 讀取 header 與 version。
3. 按不同版本解析檔名、增益、通道數、曝光、尺寸等後設資料。
4. 再按資料區偏移讀取原始位元組塊。
5. 把這些資訊收斂到 `CVCIEFile`。

它當前同時支援：

- 從檔案路徑讀取
- 從位元組陣列讀取

並且大量細節都圍繞“防止越界、異常、OOM、無效頭部”展開，這些都是典型的格式解析程式碼，而不是通用檔案服務層。

## `CVCIEFile` 當前承擔什麼角色

`CVCIEFile` 現在是模組裡最核心的資料結構，負責承載：

- 檔案版本
- 檔案型別
- 行列與位深
- 通道數
- 增益與曝光陣列
- 原始檔名
- 原始資料位元組
- 檔案路徑

它本身還實現了 `IDisposable`，在釋放時會主動清空大塊資料陣列。這再次說明當前模組主要面對的是大體積影像資料，而不是輕量文字配置。

## 當前有哪些實用入口

從 `CVFileUtil` 當前實現看，比較關鍵的入口包括：

- `IsCIEFile(...)`
- `IsCVCIEFile(...)`
- `ReadCIEFileHeader(...)`
- `ReadCIEFileData(...)`
- `Read(...)`
- `OpenLocalFileChannel(...)`
- `OpenLocalCVFile(...)`

這些入口基本都圍繞兩個目標：

- 判定檔案是不是 ColorVision 專有格式
- 把專有格式安全地解析成記憶體物件

## 當前幾個最容易寫錯的點

### 它不是泛化的檔案系統中臺

當前原始碼沒有實現舊文件裡那套通用 `FileIOManager`、JSON 處理器、YAML 處理器、批次執行器等完整體系。繼續沿用那套寫法，會把並不存在的抽象層寫成事實。

### 核心物件是二進位制影像載體，不是文字配置模型

`CVCIEFile` 當前主要承載的是影像後設資料和大塊原始位元組陣列。這和舊稿裡偏重配置、壓縮、序列化服務的敘述完全不是一個重心。

### 版本分支是實現重點

`ReadCIEFileHeader(...)` 對不同 version 的分支解析是當前真實複雜度來源之一。文件如果只寫“讀取一個自訂格式檔案”，會把這層細節抹掉。

### 安全性主要體現在防禦式讀取

當前程式碼大量檢查檔案長度、偏移、陣列分配和異常，而不是實現所謂的“熱更新型非同步檔案框架”。理解這一點，才能看懂為什麼程式碼重心在 header/data 解析上。

## 推薦閱讀順序

1. `Engine/ColorVision.FileIO/CVCIEFile.cs`
2. `Engine/ColorVision.FileIO/CVFileUtil.cs`

先看資料載體，再看具體解析邏輯，會比先翻舊文件有效得多。

## 繼續閱讀

- [docs/04-api-reference/engine-components/cvColorVision.md](./cvColorVision.md)
- [docs/04-api-reference/engine-components/ColorVision.Engine.md](./ColorVision.Engine.md)
- [docs/03-architecture/overview/system-overview.md](../../03-architecture/overview/system-overview.md)