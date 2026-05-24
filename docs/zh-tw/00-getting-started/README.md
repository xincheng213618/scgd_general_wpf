# 入門指南

本章節只保留首次接觸 ColorVision 時最需要的入口，避免和後續章節重複。

## 建議閱讀順序

1. [什麼是 ColorVision](./what-is-colorvision.md)
2. [系統要求](./prerequisites.md)
3. [安裝指南](./installation.md)
4. [首次執行](./first-steps.md)
5. [快速上手](./quick-start.md)

## 適用範圍

- 新使用者想完成安裝、啟動和基礎驗證
- 新同事想快速知道主程式、裝置、流程和外掛分別在哪裡
- 開發者想確認原始碼建置的最短路徑

## 你會在這裡找到什麼

- 產品定位和典型使用場景
- Windows 環境要求與安裝前準備
- 主程式首次啟動後的基礎操作路徑
- 從原始碼執行主程式的最小步驟

## 從原始碼啟動

當前倉庫以 Windows WPF 和 x64 為主，建議先完成依賴恢復，再建置主程式：

```powershell
dotnet restore
dotnet build -p:Platform=x64
dotnet run --project ColorVision/ColorVision.csproj
```

## 繼續閱讀

- 想看介面和日常操作：前往 [使用者指南](../01-user-guide/README.md)
- 想看系統設計和模組邊界：前往 [架構設計](../03-architecture/README.md)
- 想看倉庫目錄與模組分工：前往 [專案結構總覽](../05-resources/project-structure/README.md)
- 想進行二次開發：前往 [開發指南](../02-developer-guide/README.md)

## 說明

- 舊版入門頁中與架構、安裝器實現、釋出指令碼相關的長篇內容，已經收斂到對應章節，不再在這裡重複維護。
- 若文件與當前程式碼行為不一致，以原始碼和實際建置結果為準。

