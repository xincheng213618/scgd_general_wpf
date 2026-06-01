# RBAC 模組

本頁描述當前倉庫中已經實現的 RBAC 子系統，不再繼續維護那份把它寫成完整企業安全平台的 draft 文件。

## 模組位置

當前 RBAC 實現集中在 `UI/ColorVision.Solution/Rbac/`，而不是 `Engine/`。

這點很重要，因為它說明當前權限系統更接近桌面 Solution 側的本地使用者與權限模組，而不是引擎層統一安全核心。

## 初始化時會發生什麼

`RbacManager` 是這套子系統的總入口。當前初始化鏈大致是：

1. 建立 `%AppData%/ColorVision/Config/` 目錄。
2. 開啟或建立本地 SQLite 資料庫 `Rbac.db`。
3. 用 SqlSugar CodeFirst 初始化實體表。
4. 初始化 `AuthService`、`UserService`、`RoleService`、`PermissionService`、`AuditLogService`、`SessionService`、`PermissionChecker`、`TenantService`。
5. 建立預設管理員角色和管理員使用者。
6. 寫入預置權限，並把全部權限分配給 `admin` 角色。
7. 如果已有登入快取，則把當前使用者的 `PermissionMode` 同步回全域性授權狀態。

這條鏈說明當前 RBAC 的啟動是本地、自舉式的，不依賴外部認證伺服器。

## 當前真實存在的核心實體

從 `Entity/` 目錄看，當前最重要的表模型包括：

- `UserEntity` 對應 `sys_user`
- `UserDetailEntity` 對應 `sys_user_detail`
- `RoleEntity` 對應 `sys_role`
- `PermissionEntity` 對應 `sys_permission`
- `RolePermissionEntity` 對應 `sys_role_permission`
- `SessionEntity` 對應 `sys_session`
- `AuditLogEntity` 對應 `sys_audit_log`

另外還有 `TenantEntity` 和 `UserTenantEntity`，說明這套實現已經預留了租戶維度，但這不是當前桌面權限入口頁最主要的閱讀重點。

## 這些實體現在分別承擔什麼

### 使用者與使用者詳情

`UserEntity` 存使用者名稱、密碼雜湊、啟用狀態和軟刪除狀態。

`UserDetailEntity` 額外存：

- `PermissionMode`
- 郵箱、電話、地址、公司、部門、崗位
- 使用者頭像和備註

這裡要特別注意：當前全域性粗粒度權限級別不是從角色表即時推匯出來的，而是直接儲存在 `UserDetailEntity.PermissionMode` 裡，並在登入後同步到 `Authorization.Instance.PermissionMode`。

### 角色與權限

`RoleEntity` 管角色基本資訊，`PermissionEntity` 管權限碼，`RolePermissionEntity` 管角色到權限的關聯。

當前權限服務裡已經有一組預置權限碼，例如：

- `user.create`
- `user.edit`
- `role.assign_permissions`
- `permission.manage`
- `audit.view`

這說明當前細粒度權限已經不只是“管理員/訪客”幾檔，而是開始進入按動作編碼控制。

### 會話

`SessionEntity` 儲存：

- `SessionToken`
- 使用者 ID
- 裝置資訊和 IP
- 建立時間、過期時間、最後活躍時間
- 是否已撤銷

`SessionService` 負責生成 64 位元組隨機 Token、校驗會話、更新活躍時間、撤銷會話和清理過期會話。

### 審計

`AuditLogEntity` 當前記錄：

- 使用者 ID / 使用者名稱
- 動作程式碼
- 明細說明
- 時間
- IP

`AuditLogService` 當前提供的是面向 RBAC 側的本地審計記錄，而不是覆蓋所有業務模組的統一審計匯流排。

## 當前登入鏈怎麼走

當前認證鏈大致是：

1. 使用者在 `LoginWindow` 輸入使用者名稱和密碼。
2. `AuthService.LoginAndGetDetailAsync(...)` 查詢啟用且未軟刪除的使用者。
3. 密碼透過 `PasswordHasher` 校驗，舊格式密碼會在登入時升級。
4. 系統確保 `UserDetailEntity` 存在，並載入角色列表。
5. 登入結果寫入 `LoginResultDto`。
6. `SessionService` 可進一步建立和維護 SessionToken。
7. 登入成功後把 `UserDetailEntity.PermissionMode` 同步到 `Authorization.Instance.PermissionMode`。

因此當前登入結果既影響 RBAC 子系統內部狀態，也直接影響全域性 UI 的粗粒度權限判斷。

## 當前權限檢查怎麼做

當前權限檢查有兩層：

### 粗粒度入口判斷

很多視窗先直接判斷：

- `Authorization.Instance.PermissionMode > PermissionMode.Administrator`

例如使用者管理和權限管理視窗都會先攔這一步。

### 細粒度權限碼判斷

更細的權限校驗由 `PermissionChecker` 負責。它會：

- 查詢使用者關聯的角色 ID
- 聯表查出對應權限碼
- 使用帶過期時間和 LRU 驅逐的快取儲存結果

所以當前系統並不是“只有 RBAC”或“只有 PermissionMode”，而是粗細兩層並存。

## 當前可見的管理介面

從模組目錄看，RBAC 目前已經有一組明確的桌面視窗：

- `LoginWindow`
- `RegisterWindow`
- `ChangePasswordWindow`
- `UserManagerWindow`
- `PermissionManagerWindow`
- `RbacManagerWindow`

其中：

- `UserManagerWindow` 負責使用者列表、角色檢視、啟停、刪除、重置密碼等管理動作
- `PermissionManagerWindow` 負責按角色分配權限，並在儲存後失效權限快取

## 當前設計最需要注意的邊界

### 它是本地權限系統，不是統一遠端身份平台

當前實現依賴本地 SQLite 和本地視窗，不是外部認證中心。

### 粗粒度 `PermissionMode` 還沒有被完全替換

很多關鍵入口依然先看 `PermissionMode`，再進入 RBAC 管理邏輯。

### 細粒度權限接入是區域性的

當前能確認的細粒度權限能力，主要集中在 RBAC 自己的管理視窗和服務層，還不能把整個產品都描述成已經全面接入權限碼控制。

## 這頁不再做什麼

本頁不再繼續維護這些與當前實現不符的內容：

- 虛構的通用 `AuthService`/`AuditService` 平台分層圖
- 假定所有業務模組都被 RBAC 統一攔截
- 多因素認證、網路證書、IP 白名單等未見落地實現的安全能力

如果後續要擴充套件到全系統安全架構，應基於真實接入點另寫專題頁。

## 推薦閱讀順序

1. `UI/ColorVision.Solution/Rbac/RbacManager.cs`
2. `UI/ColorVision.Solution/Rbac/Entity/`
3. `UI/ColorVision.Solution/Rbac/Services/Auth/AuthService.cs`
4. `UI/ColorVision.Solution/Rbac/Services/SessionService.cs`
5. `UI/ColorVision.Solution/Rbac/Services/PermissionChecker.cs`
6. `UI/ColorVision.Solution/Rbac/UserManagerWindow.xaml.cs`
7. `UI/ColorVision.Solution/Rbac/PermissionManagerWindow.xaml.cs`

## 繼續閱讀

- [安全與權限控制](./overview.md)
- [架構執行時](../overview/runtime.md)
- [元件互動](../overview/component-interactions.md)