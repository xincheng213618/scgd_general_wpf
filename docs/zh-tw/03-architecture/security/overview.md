# 安全與權限控制

本章只描述當前倉庫裡已經落地的權限與會話實現，不再繼續維護把 ColorVision 寫成一套覆蓋網路、資料、審計、認證全鏈路的通用安全白皮書。

## 當前真正存在的兩層權限邊界

從程式碼看，當前安全相關能力主要分成兩層：

- `UI/ColorVision.Common/Authorizations/` 下的粗粒度 `PermissionMode`
- `UI/ColorVision.Solution/Rbac/` 下的本地 RBAC 子系統

這兩層不是互斥關係，而是並存。

## 第一層：全域性粗粒度權限

`Authorization.Instance.PermissionMode` 仍然是當前很多視窗和操作的第一道邊界。

它提供的級別包括：

- `SuperAdministrator`
- `Administrator`
- `PowerUser`
- `User`
- `Guest`

很多 UI 入口當前直接用這層做判斷，例如“只有管理員可以開啟使用者管理和權限管理視窗”。

所以如果只看 RBAC 服務層，很容易高估當前系統已經完成的細粒度接入範圍。

## 第二層：Solution 側本地 RBAC

更細的使用者、角色、權限、會話和審計能力，當前集中在 `UI/ColorVision.Solution/Rbac/`。

這個子系統當前的特點是：

- 使用本地 SQLite 資料庫
- 資料庫預設位於 `%AppData%/ColorVision/Config/Rbac.db`
- 透過 SqlSugar CodeFirst 初始化表結構
- 提供登入、使用者管理、權限管理、會話和審計服務

它更像“Solution 側本地帳戶與權限模組”，而不是整個產品所有安全能力的唯一總入口。

## 當前安全章最該關注什麼

### 登入與會話

當前 `AuthService` 負責使用者名稱密碼登入和基於 SessionToken 的自動登入恢復，`SessionService` 負責建立、校驗、撤銷和清理會話。

這部分是當前程式碼裡最明確的認證鏈。

### 角色與權限

當前 `RbacManager` 會初始化角色、權限、使用者和角色-權限對映，並透過 `PermissionChecker` 做細粒度權限碼校驗。

### 審計

當前 `AuditLogService` 已經存在，但它是圍繞 RBAC 相關動作記錄本地審計日誌，而不是一套覆蓋整個應用所有操作的全域性審計平台。

## 當前沒有證據支撐的內容

以下內容不應繼續在本章裡作為既有能力陳述：

- 多因素認證
- 全域性網路通訊加密策略
- 證書驗證體系
- IP 白名單
- 防火牆策略
- 覆蓋所有模組的統一審計與攔截鏈

如果將來這些能力真的落地，應基於實際程式碼另開專題頁，而不是提前寫進架構概覽。

## 推薦閱讀順序

推薦按這條線讀：

1. `UI/ColorVision.Common/Authorizations/PermissionMode.cs`
2. `UI/ColorVision.Common/Authorizations/AccessControl.cs`
3. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
4. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
6. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
7. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
8. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 繼續閱讀

- [RBAC 模組](./rbac.md)
- [架構執行時](../overview/runtime.md)
- [元件互動](../overview/component-interactions.md)

## 說明

- 本頁只保留當前實現裡可驗證的權限與會話邊界，不再繼續維護泛化安全能力清單。