# 部署概覽

本頁只保留當前倉庫仍在使用的部署入口，重點覆蓋 Windows 桌面應用、安裝器和更新機制。

## 當前部署物件

- `ColorVision/`：主程式本體
- `ColorVisionSetup/`：安裝與更新相關程式
- `Scripts/`：建置、打包、釋出輔助指令碼
- `Plugins/`：執行時載入的外掛目錄

## 當前推薦路徑

### 開發或測試環境

直接從原始碼建置並執行主程式：

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

### 交付環境

- 使用安裝器交付完整桌面程式
- 按需攜帶外掛目錄和執行時依賴
- 若涉及線上更新，繼續閱讀 [自動更新系統](./auto-update.md)

## 部署前確認項

- 目標環境為 Windows
- 主應用按 x64 建置
- 執行時依賴和本地 DLL 已正確隨包輸出
- 需要的配置檔案已複製到輸出目錄

## 配套文件

- [入門指南](../../00-getting-started/README.md)
- [系統要求](../../00-getting-started/prerequisites.md)
- [自動更新系統](./auto-update.md)
- [建置與釋出指令碼](../scripts/README.md)

## 說明

- 舊的 Docker、雲部署、生產叢集等說明不再作為預設部署路徑。
- 如果某個專案有特殊交付方式，應在對應專案目錄或專案文件中單獨維護，而不是繼續堆在通用部署頁裡。