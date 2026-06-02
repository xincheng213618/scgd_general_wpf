# ColorVision.Themes

本頁只描述 UI/ColorVision.Themes 當前已經落地的主題能力，不再延續舊文件裡那種“主題開發框架 + 自訂主題平台 + 完整 FAQ 教程”的寫法。

## 模組定位

ColorVision.Themes 當前更接近一個 WPF 主題資源與視窗外觀支援庫，核心職責主要有四類：

- 定義 Theme 列舉和主題切換入口
- 向 Application 注入資源字典
- 跟隨 Windows 主題變化更新介面
- 處理視窗標題欄顏色和圖示聯動

它不是一個已經抽象完成的“任意自訂主題平台”。舊文件裡提到的 Theme.Custom、ResourceDictionaryCustom、完整自訂主題註冊流程，在當前程式碼中都沒有對應實現。

## 當前最關鍵的檔案

從當前專案結構看，最值得優先閱讀的是：

- ThemeManager.cs：主題切換主入口
- ThemeManagerExtensions.cs：Application 和 Window 擴充套件方法
- Theme.cs：主題列舉定義
- Themes/ 下的 XAML：基礎樣式和各主題資源字典
- Controls/、Converter/、Utilities/：主題庫附帶的控制元件、轉換器和工具程式碼

## 關鍵入口型別

### ThemeManager

ThemeManager 是當前主題模組的中心物件。它負責：

- 維護 CurrentTheme 和 CurrentUITheme
- 處理 UseSystem、Light、Dark、Pink、Cyan 五種主題
- 根據主題裝載對應的 ResourceDictionary 列表
- 監聽 Windows 主題變化
- 在切換主題時觸發主題變更事件
- 調整視窗標題欄顏色

當前資源字典按幾組固定列表組織：

- ResourceDictionaryBase：基礎共享樣式
- ResourceDictionaryDark：深色主題資源
- ResourceDictionaryWhite：淺色主題資源
- ResourceDictionaryPink：粉色主題資源
- ResourceDictionaryCyan：青色主題資源

這說明現階段的主題機制是“固定主題列舉 + 固定資源字典集合”的實現方式，而不是執行時可任意註冊新主題型別的開放模型。

### Theme

當前主題列舉只有五個值：

- UseSystem
- Light
- Dark
- Pink
- Cyan

其中 UseSystem 並不是單獨的一套資源，而是在 ApplyTheme 時被對映成當前 AppsTheme 對應的淺色或深色主題。

### ThemeManagerExtensions

ThemeManagerExtensions 提供了兩個實際很常用的入口：

- Application.ApplyTheme：應用主題
- Application.ForceApplyTheme：強制重新裝載主題資源

另外，Window.ApplyCaption 會在視窗 Loaded 後：

- 設定標題欄顏色
- 根據當前主題切換視窗圖示
- 訂閱主題變化並在視窗關閉時解綁

所以這個模組不僅管資源字典，也負責一部分視窗殼層外觀行為。

## 當前執行時主鏈

現有主題鏈路更接近下面這條：

1. 上層 UI 選擇主題。
2. Application.ApplyTheme 調到 ThemeManager.Current.ApplyTheme。
3. 如果當前選擇是 UseSystem，則先解析成 AppsTheme。
4. ThemeManager 按主題把本模組的資源字典加入 Application.Resources.MergedDictionaries。
5. CurrentTheme 和 CurrentUITheme 更新，並觸發變更事件。
6. 已呼叫 ApplyCaption 的視窗跟隨更新標題欄顏色和圖示。

## 系統主題跟隨是怎樣做的

ThemeManager 在構造時會啟動一個延遲初始化流程。當前實現會在較晚時機再掛接系統事件，而不是在應用啟動最早階段就同步處理。

它主要監聽：

- SystemEvents.UserPreferenceChanged
- SystemParameters.StaticPropertyChanged

然後透過讀取登錄檔中的 Personalize 項判斷：

- AppsUseLightTheme
- SystemUsesLightTheme

因此“跟隨系統”當前依賴的是 Windows 登錄檔值和系統事件，並不是框架層自動提供的完整主題同步服務。

## 標題欄顏色和視窗圖示

ThemeManager 還負責呼叫 DWM API 更新視窗外觀：

- 深色主題啟用沉浸式暗色標題欄
- 粉色和青色主題直接設定標題欄和邊框顏色
- 淺色和跟隨系統模式重置為系統預設標題欄顏色

Window.ApplyCaption 還會根據當前主題切換視窗圖示資源。這部分行為是當前模組很實際的一層價值，舊文件裡反而沒有把它講清楚。

## 當前實現的邊界

### 主題持久化不由 ThemeManager 自己完成

當前主題配置雖然使用 ColorVision.Themes 名稱空間，但配置類 ThemeConfig 實際位於 UI/ColorVision.UI/Themes。

這意味著：

- 主題資源和切換核心在 UI/ColorVision.Themes
- 選單、快捷鍵、配置項編輯等整合邏輯在 UI/ColorVision.UI

不要把整個“主題配置系統”都歸到 Themes 專案自身。

### 選單和快捷鍵入口在 UI 整合層

當前主題選單和快捷鍵入口主要在：

- UI/ColorVision.UI/Themes/ThemesHotKey.cs

它負責：

- 生成主題選單項
- 在切換時寫入 ThemeConfig.Instance.Theme
- 呼叫 Application.ApplyTheme
- 提供 Ctrl + Shift + T 快捷鍵輪換主題

所以 Themes 模組本身提供的是能力底座，真正和桌面選單系統對接的是 UI 層。

### 舊文件裡的自訂主題擴充套件點並不存在

當前程式碼裡並沒有這些舊文件聲稱可用的介面：

- Theme.Custom
- ThemeManager.ResourceDictionaryCustom
- ThemeConfig.FollowSystem

這類內容已經不能繼續作為現有能力寫在 API 參考裡。

## 當前更適合怎樣讀這個模組

### 想看主題如何切換

先看：

- ThemeManager.cs
- ThemeManagerExtensions.cs
- Theme.cs

### 想看主題如何接入應用選單和配置

先看：

- UI/ColorVision.UI/Themes/ThemeConfig.cs
- UI/ColorVision.UI/Themes/ThemesHotKey.cs

### 想看主題資源長什麼樣

先看：

- Themes/Base.xaml
- Themes/Dark.xaml
- Themes/White.xaml
- Themes/Pink.xaml
- Themes/Cyan.xaml

## 這頁不再做什麼

本頁不再繼續維護這些高風險內容：

- 不存在的自訂主題註冊 API
- 偽造的 ThemeConfig 配置欄位
- 教程式的完整主題開發流程
- 大段版本號、框架相容矩陣、效能數字承諾

如果後續要補充主題相關內容，應優先補真實資源字典、視窗行為或 UI 接入點，而不是再恢復成一篇泛化教程。

## 繼續閱讀

- [UI元件概覽](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)
