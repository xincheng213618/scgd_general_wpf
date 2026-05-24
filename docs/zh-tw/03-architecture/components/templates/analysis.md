# Templates 模組分析

本頁只分析當前倉庫裡的 Templates 系統現狀，不再保留“補了多少文件”“規劃了多少最佳化方案”這類已經失效的總結內容。

## 先看這個模組到底是什麼

`Engine/ColorVision.Engine/Templates/` 不是單一演算法目錄，而是一組同時承擔這幾類職責的程式碼：

- 模板抽象和註冊
- 模板管理與編輯視窗
- 不同業務域的模板實現
- 流程模板相關能力
- 模板搜尋、建立、樣例儲存等輔助工具

因此它既有純模型程式碼，也有明顯的 WPF 視窗和編輯器程式碼，閱讀時不要把它誤判成“只有演算法參數定義”。

## 當前最值得先認識的檔案

### 核心入口

- `ITemplate.cs`：模板抽象能力的主入口
- `ModelBase.cs`、`ParamModBase.cs`：模板模型和參數模型的基礎型別
- `TemplateContorl.cs`：模板註冊中心與裝載入口

### 管理與編輯 UI

- `TemplateManagerWindow.xaml(.cs)`：模板管理視窗
- `TemplateEditorWindow.xaml(.cs)`：模板編輯視窗
- `TemplateCreate.xaml(.cs)`：模板建立入口
- `TemplateSettingEdit.xaml(.cs)`：模板配置編輯入口

### 搜尋與樣例

- `TemplateSearchProvider.cs`
- `TemplateSampleLibrary.cs`
- `TemplateSampleSaveWindow.xaml(.cs)`

如果只是想弄清“模板怎麼被發現、怎麼被開啟、怎麼被編輯”，先看這些檔案通常比直接扎進某個演算法子目錄更有效。

## 當前目錄怎麼分

從倉庫現狀看，這個目錄至少可以分成幾類：

### 1. 核心框架層

這部分負責模板抽象、註冊、基礎模型和公共 UI。

典型檔案包括：

- `ITemplate.cs`
- `ModelBase.cs`
- `ParamModBase.cs`
- `TemplateContorl.cs`
- `TemplatesExtension.cs`

### 2. 流程模板層

`Flow/` 下承接流程模板和流程編輯執行相關能力，它和一般演算法模板處於同一大系統中，但使用場景明顯不同。

### 3. 業務模板族

當前倉庫裡仍能直接看到多組業務模板目錄，例如：

- `ARVR/`
- `POI/`
- `Compliance/`
- `JND/`
- `Matching/`
- `FindLightArea/`
- `FocusPoints/`
- `ImageCropping/`
- `LedCheck/`
- `LEDStripDetection/`
- `Validate/`
- `DataLoad/`

這些目錄並不是完全按統一規則切分的。有的是按演算法域分組，有的是按處理環節分組，也有一部分更接近歷史演進留下的功能包。

### 4. JSON 模板族

`Jsons/` 是當前最容易讓人誤讀的區域之一。它通常承接一批以 JSON 配置為核心的模板實現，與傳統目錄式模板並存。

如果看到名稱相近但目錄不同的模板實現，不要先假定是重複程式碼，更可能是歷史版本、配置方式或業務接入方式不同。

## 當前執行時是怎麼把模板接進系統的

模板系統當前最重要的執行時鏈路很直接：

1. 主程式和外掛先把程式集裝載進來。
2. `TemplateContorl` 在資料庫連線可用後掃描當前已載入程式集。
3. 它查詢實現了 `IITemplateLoad` 的非抽象型別。
4. 這些型別透過 `Load()` 把模板註冊進 `ITemplateNames`。
5. 模板管理視窗、編輯視窗、流程視窗和業務功能再去消費這些已註冊模板。

這條鏈路說明兩個實際約束：

- 模板不是在一個靜態總表裡手寫宣告完的。
- 模板能不能出現，受程式集裝載和資料庫可用狀態共同影響。

## 讀這個模組時最常見的誤區

### 誤區 1：把它當成純演算法層

這裡同時包含視窗、編輯器、搜尋、樣例儲存、模板建立和流程相關 UI，不是單純的演算法參數定義倉庫。

### 誤區 2：以為所有目錄都是同一時期按同一規則設計的

不是。當前目錄結構有明顯演進痕跡，不能期待它天然滿足一套完全一致的分層模型。

### 誤區 3：模板缺失時先查編輯器

很多模板問題更早出在註冊鏈：程式集沒裝載、資料庫沒連上、`IITemplateLoad` 沒跑到，或者模板名重複被覆蓋。

## 如果現在要繼續往下讀

推薦順序是：

1. 先看 `TemplateContorl.cs`，理解模板如何被發現和註冊。
2. 再看 `ITemplate.cs`、`ModelBase.cs`、`ParamModBase.cs`，理解模板物件本身長什麼樣。
3. 然後看 `TemplateManagerWindow` 和 `TemplateEditorWindow`，理解使用者如何操作模板。
4. 最後再進入具體業務目錄，比如 `ARVR/`、`POI/` 或 `Flow/`。

這樣比一開始就鑽進某個模板子目錄更容易建立整體認知。

## 這頁不再做什麼

本頁不再繼續維護這些內容：

- 文件補全成果統計
- 過於具體但未落地的重構路線圖
- 與當前倉庫狀態脫節的統一分層模型

如果後續要做真正的重建置議，應在獨立設計頁裡單獨論證，而不是混在“現狀分析”裡。

## 繼續閱讀

- [Templates 架構設計](./design.md)
- [元件互動](../../overview/component-interactions.md)
- [架構執行時](../../overview/runtime.md)
