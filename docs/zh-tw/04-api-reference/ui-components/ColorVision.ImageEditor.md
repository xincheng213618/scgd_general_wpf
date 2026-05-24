# ColorVision.ImageEditor

本頁只描述 UI/ColorVision.ImageEditor 當前已經落地的主控鏈和擴充套件點，不再繼續維護舊文件裡那種“功能大全 + 教程示例 + 效能數字承諾”的寫法。

## 模組定位

ColorVision.ImageEditor 當前不是單純的圖片顯示控制元件，而是一套“影像宿主 + 可縮放畫布 + 繪圖工具 + 開啟器 + 執行時工具覆蓋 + 設定系統”的組合模組。

它的主線更接近：

- `ImageView` 作為宿主
- `EditorContext` 作為當前檢視執行時容器
- `DrawCanvas` 作為真實繪製畫布
- `IEditorToolFactory` 負責發現和裝配工具、選單、開啟器
- `IImageOpen` 負責不同檔案型別的開啟鏈

## 當前最關鍵的目錄

從專案目錄看，最值得優先閱讀的是：

- `ImageView.xaml(.cs)`：主控制元件和執行時編排入口
- `EditorContext.cs`：每個檢視例項的執行時容器
- `DrawCanvas.cs`：視覺樹和繪圖畫布
- `EditorToolFactory.cs`：工具、選單、開啟器發現與裝配
- `Abstractions/`：編輯器擴充套件點邊界
- `Draw/`：圖元、工具、選擇框、註釋匯入匯出
- `EditorTools/`：非圖元類工具，例如縮放、偽彩色、3D、演算法、全屏
- `Video/`：影片開啟器
- `Layers/`：圖層/通道切換語義
- `Realtime/`、`Settings/`：實時影像和設定相關支援

## 關鍵入口型別

### ImageView

`ImageView` 是當前編輯器模組的主入口。它負責：

- 初始化 `EditorContext`
- 繫結 `DrawCanvas`、`Zoombox`、上下文選單和狀態列
- 執行 `IImageComponent` 一次性初始化
- 管理標準檔案命令
- 接線配置變化、縮放變化和 overlay 重新整理
- 處理影像開啟、清理、儲存、匯入匯出註釋等流程

如果要理解這個模組，首要入口就是 `ImageView.xaml.cs`。

### EditorContext

`EditorContext` 是每個 `ImageView` 例項的執行時容器，當前會集中儲存：

- 當前 `ImageView`
- `DrawCanvas`
- `Zoombox`
- `ImageViewConfig`
- `DrawEditorManager`
- `IEditorToolFactory`
- 當前 opener、上下文選單、圖元列表
- 一組輕量 service 登錄檔

它既是執行時狀態容器，也帶一點區域性 service locator 性質，這也是當前模組的重要實現邊界。

### DrawCanvas

`DrawCanvas` 是真實的繪圖承載層。它不只是顯示控制元件，還負責：

- 維護視覺物件集合
- 執行命中測試
- 處理圖元增刪
- 維護撤銷/重做
- 作為大量繪圖工具掛接滑鼠事件的目標

### IEditorToolFactory

名字雖然叫 `IEditorToolFactory`，當前它其實是一個具體類，不是介面。它會在構造時反射掃描並裝配：

- `IDVContextMenu`
- `IIEditorToolContextMenu`
- `IEditorTool`
- `IImageComponent`
- `IImageOpen`

同時，它還會維護“全域性工具 + 當前 opener runtime tools”的生效檢視，並按 `GuidId` 做覆蓋重建工具欄。

這也是當前 ImageEditor 初始化成本和擴充套件能力最集中的地方。

### IImageOpen 及其擴充套件介面

當前開啟鏈不靠一個統一檔案管理器，而是由各個 `IImageOpen` 實現負責。

另外，`IImageOpen` 還可以可選實現：

- `IImageOpenEditorToolProvider`
- `IImageOpenEditorToolLifecycle`

這樣某些特殊檔案型別就能在開啟後臨時接管或覆蓋工具欄能力，而不是把所有分支都堆進全域性工具裡。

### VideoOpen / Window3D / ModelViewer3DControl

影片和 3D 能力當前是編輯器模組裡的真實子功能，但它們屬於附加開啟器或工具，不是整個模組唯一主線：

- `Video/VideoOpen.cs`：影片開啟器
- `EditorTools/ThreeD/Window3D.xaml.cs`：影像轉 3D 表面視窗
- `EditorTools/ThreeD/ModelViewer3DControl.xaml.cs`：OBJ/STL 檢視控制元件

舊文件裡對這些能力展開得很重，但更可靠的讀法仍然是先搞清楚 `ImageView` 和工具工廠主鏈。

## 當前執行時主鏈

現有控制鏈大致是：

1. 建立 `ImageView`。
2. 初始化 `EditorContext`、`SelectionVisual`、`CompactInspectorPresenter`。
3. 建立 `IEditorToolFactory` 並反射裝配全域性工具、上下文選單、影像元件、開啟器。
4. 執行所有 `IImageComponent.Execute(ImageView)`。
5. 使用者開啟檔案後，根據副檔名選中 `IImageOpen`。
6. opener 呼叫 `SetImageSource(...)`，並可選提供自己的 runtime tools。
7. `DrawCanvas`、overlay、狀態列、圖層切換、偽彩色等圍繞當前影像上下文繼續工作。

## 當前實現有哪些邊界

### ImageView 不是純顯示控制元件

`SetImageSource(...)` 當前不只是設定 `ImageShow.Source`，還可能觸發偽彩色配置、標定服務和其他編輯器執行時副作用。

如果只是純顯示場景，需要注意 `EnableEditorImageServices` 這類開關，不能預設把整個 ImageView 當成無副作用圖片框。

### 工具發現是反射驅動的

`IEditorToolFactory` 當前每個檢視例項都會做多輪掃描和建立，這是一條真實而重要的控制鏈。舊文件裡那種“靜態工具架構圖”會掩蓋掉這件事。

### EditorContext 既是狀態容器也帶服務定位器屬性

當前設計並沒有把“配置”“工具狀態”“執行時服務”徹底解耦，而是部分集中在 `EditorContext`、`ImageViewConfig` 和少量 service 上。這是閱讀和後續重構時必須承認的現狀。

### 註釋匯入匯出已經有實際落點

圖元持久化不是停留在概念層，當前實際落在 `Draw/Annotations/` 以及 `ImageView` 的匯入匯出入口上。閱讀標註能力時，應該直接看這條鏈，而不是泛化成“任意繪圖工具可自動持久化”。

## 當前更適合怎樣讀這個模組

### 想看主控鏈和初始化編排

先看：

- `ImageView.xaml.cs`
- `EditorContext.cs`
- `EditorToolFactory.cs`

### 想看繪圖和選擇邏輯

先看：

- `DrawCanvas.cs`
- `Draw/`
- `Abstractions/Draw/`

### 想看檔案開啟鏈和 runtime tool 覆蓋

先看：

- `Abstractions/IImageEditor.cs`
- `Video/VideoOpen.cs`
- 具體 opener 實現所在目錄

### 想看註釋、偽彩色、3D 這些子能力

先看：

- `Draw/Annotations/`
- `EditorTools/PseudoColor/`
- `EditorTools/ThreeD/`

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 大段效能數字承諾
- 覆蓋全模組的教程式示例集
- 把影片或 3D 寫成整個模組的唯一主線
- 編造與當前程式碼不匹配的統一檢視模型或抽象介面

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
- [ColorVision.Themes](./ColorVision.Themes.md)