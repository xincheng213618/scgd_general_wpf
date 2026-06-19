# 建置與釋出指令碼

ColorVision 專案包含一組 Python 指令碼，用於建置應用程式、打包外掛、釋出更新和管理後端上傳。

## 指令碼概覽

| 指令碼 | 功能 |
|------|------|
| `build.py` | 建置主程式安裝包併發布 |
| `build_update.py` | 建置增量更新包 |
| `build_plugin.py` | 相容入口，內部轉發到 `package_cvxp.py` |
| `generate_shared_files.py` | 掃描宿主輸出目錄生成 `shared_files.json` |
| `package_cvxp.py` | 基於 `shared_files.json` 剝離並打包/上傳外掛 |
| `package_plugin.bat` | 倉庫內外掛一鍵建置並呼叫 `package_cvxp.py` |
| `package_project.bat` | 倉庫內專案一鍵建置並呼叫 `package_cvxp.py` |
| `package_cvxp_demo.bat` | 給外部外掛作者的最小打包示例 |
| `build_spectrum.py` | 建置 Spectrum 外掛 |
| `publish_plugin.py` | 釋出外掛到市場後端 |
| `backend_client.py` | 後端上傳共享模組 |
| `file_manager.py` | 檔案管理工具 |

## 環境配置

### 認證配置

指令碼使用以下環境變數進行後端認證：

```powershell
# PowerShell
$env:COLORVISION_UPLOAD_URL = "http://xc213618.ddns.me:9998"
$env:COLORVISION_UPLOAD_USERNAME = "xincheng"
$env:COLORVISION_UPLOAD_PASSWORD = "xincheng"
```

```bash
# Bash (Git Bash/WSL)
export COLORVISION_UPLOAD_URL="http://xc213618.ddns.me:9998"
export COLORVISION_UPLOAD_USERNAME="xincheng"
export COLORVISION_UPLOAD_PASSWORD="xincheng"
```

::: tip
如果不設定環境變數，指令碼將使用預設憑據 `xincheng/xincheng`。
:::

### 可選配置

| 環境變數 | 說明 | 預設值 |
|----------|------|--------|
| `COLORVISION_UPLOAD_URL` | 後端上傳地址 | `http://xc213618.ddns.me:9998` |
| `COLORVISION_UPLOAD_FOLDER` | 上傳資料夾 | `ColorVision` |
| `COLORVISION_UPLOAD_USERNAME` | 上傳使用者名稱 | `xincheng` |
| `COLORVISION_UPLOAD_PASSWORD` | 上傳密碼 | `xincheng` |

## build.py - 主程式建置

建置主程式安裝包並上傳到後端。

### 用法

```powershell
# 完整建置（編譯 + 打包 + 上傳）
py Scripts\build.py

```

### 功能說明

1. 使用 MSBuild 編譯解決方案
2. 使用 Advanced Installer 建置安裝包
3. 執行後端預檢（`/api/health` + `/api/ready`）
4. 上傳安裝包到後端

### 前置要求

- Visual Studio 2022+ (MSBuild)
- Advanced Installer
- Python 依賴：`requests`, `tqdm`

## build_update.py - 增量更新建置

建立增量更新包（僅包含變更檔案）。

### 用法

```powershell
py Scripts\build_update.py
```

### 工作原理

1. 讀取 `ColorVision.exe` 獲取當前版本
2. 查詢歷史版本作為基線
3. 對比檔案差異生成增量包
4. 上傳增量包到 `Update/` 目錄

### 輸出檔案

- `{History}/ColorVision-[{version}].zip` - 完整包
- `{History}/update/ColorVision-Update-[{version}].cvx` - 增量包

## build_plugin.py - 相容入口

舊的打包實現已經移除。

當前 `build_plugin.py` 只保留為相容入口，會將常見倉庫內呼叫轉發到 `package_cvxp.py`，並輸出遷移提示。新指令碼不要再以它作為主入口。

### 用法

```powershell
py Scripts\build_plugin.py -t Projects -p ProjectARVR --no-upload
```

### 推薦替代

- 倉庫內外掛：`Scripts\package_plugin.bat Spectrum --no-upload`
- 倉庫內專案：`Scripts\package_project.bat ProjectARVR --no-upload`
- 倉庫外部：`py Scripts\package_cvxp.py --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows --no-upload`

## generate_shared_files.py - 共享檔案表生成

掃描宿主程式輸出目錄，生成 `shared_files.json`。

### 用法

```powershell
py Scripts\generate_shared_files.py

py Scripts\generate_shared_files.py `
    --root-dir C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Release\net10.0-windows `
    --output C:\temp\shared_files.json
```

### 輸出內容

- `generated_at`: 生成時間
- `shared_files`: 宿主目錄下的全部相對檔案路徑

### 過濾規則

- 自動忽略 `Plugins` 目錄
- 自動忽略 `Log` 目錄
- 通常只需要在宿主共享檔案發生變化後重新生成一次

## package_cvxp.py - 單檔案打包上傳

單檔案指令碼，讀取 `shared_files.json`，剔除共享檔案和 `.pdb` 後生成 `.cvxp`，並可直接上傳。

### 用法

```powershell
# 僅本地打包
py Scripts\package_cvxp.py --project-file Plugins\Spectrum\Spectrum.csproj --build --no-upload

# 指定編譯輸出目錄
py Scripts\package_cvxp.py `
    --src-dir Plugins\Spectrum\bin\x64\Release\net10.0-windows `
    --plugin-root Plugins\Spectrum

# 僅傳編譯輸出目錄，自動推斷外掛根目錄
py Scripts\package_cvxp.py `
    --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows `
    --no-upload
```

### 參數

| 參數 | 說明 | 預設值 |
|------|------|--------|
| `--src-dir` | 外掛編譯輸出目錄 | 空 |
| `--project-file` | 外掛 `.csproj` 路徑 | 空 |
| `--plugin-root` | 外掛根目錄，用於補充 `README.md` 等額外檔案 | 自動推斷 |
| `--plugin-name` | 外掛名稱 | 自動推斷 |
| `--shared-files` | `shared_files.json` 路徑；不傳時優先讀取指令碼同目錄檔案 | 自動查詢 |
| `--output-dir` | `.cvxp` 輸出目錄 | `Scripts/` |
| `--build` | 打包前先執行 `dotnet build` | 關閉 |
| `--dotnet` | `--build` 使用的 `dotnet` 命令 | `dotnet` |
| `--no-upload` | 只打包不上傳 | 關閉 |
| `--keep-package` | 上傳後保留本地包 | 關閉 |

### 打包邏輯

1. 讀取 `shared_files.json`
2. 遍歷外掛輸出目錄
3. 過濾所有 `.pdb` 檔案
4. 過濾所有存在於 `shared_files.json` 中的共享檔案
5. 寫入 `stripped_files.json`
6. 打包為 `.cvxp`
7. 未指定 `--no-upload` 時上傳包和 `LATEST_RELEASE`

### 直接傳輸出目錄

當 `--src-dir` 指向類似 `PluginName/bin/x64/Release/net10.0-windows` 或 `PluginName/bin/Release/net10.0-windows` 的目錄時，指令碼會自動把 `PluginName` 目錄識別為 `plugin_root`，這樣即使不傳 `--plugin-root`，也仍然可以帶上專案根目錄裡的 `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png`。

## package_plugin.bat - 倉庫內外掛快捷入口

這個批處理只給倉庫內外掛專案使用。它會自動定位 `.venv`、自動呼叫 `package_cvxp.py --build`，因此各外掛目錄下的 `.bat` 檔案可以只保留一行轉發。

### 用法

```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```

## package_project.bat - 倉庫內專案快捷入口

這個批處理與 `package_plugin.bat` 類似，但目標目錄改為 `Projects/*/*.csproj`。適用於客戶專案或專案化外掛。

### 用法

```powershell
Scripts\package_project.bat ProjectARVR --no-upload
```

## package_cvxp_demo.bat - 外部交付示例

這個批處理面向倉庫外部使用場景。把 `package_cvxp.py`、`shared_files.json` 和這個 demo 放在同一個目錄，修改裡面的 `SRC_DIR` 後就可以直接打包。

### 用法

```powershell
Scripts\package_cvxp_demo.bat
```

## build_spectrum.py - Spectrum 外掛建置

專門為 Spectrum 外掛最佳化的建置指令碼。

### 用法

```powershell
# 建置並上傳
py Scripts\build_spectrum.py --upload

# 僅建置不上傳
py Scripts\build_spectrum.py
```

### 特性

- 支援 .zip 和 .cvxp 兩種格式輸出
- .cvxp 包複製到對映的外掛伺服器路徑
- .zip 包使用認證上傳

## publish_plugin.py - 外掛釋出

透過 API 釋出外掛包到外掛市場。

### 用法

```powershell
# 基本釋出
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp

# 完整參數
py Scripts\publish_plugin.py `
  -p Spectrum `
  -v 1.0.0.1 `
  -f Spectrum-1.0.0.1.cvxp `
  -n "Spectrum Plugin" `
  -d "光譜分析外掛" `
  -a "Author Name" `
  -c "Analysis" `
  --changelog CHANGELOG.md `
  --icon PackageIcon.png

# 指定後端地址
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://localhost:9999
```

### 參數

| 參數 | 說明 | 必需 |
|------|------|------|
| `-p, --plugin-id` | 外掛唯一 ID | 是 |
| `-v, --version` | 版本號 (如 1.0.0.1) | 是 |
| `-f, --file` | 包檔案路徑 | 是 |
| `-n, --name` | 顯示名稱 | 否 |
| `-d, --description` | 描述 | 否 |
| `-a, --author` | 作者 | 否 |
| `-c, --category` | 分類 | 否 |
| `-r, --requires` | 最低引擎版本 | 否 |
| `--changelog` | 更新日誌檔案或文字 | 否 |
| `--icon` | 圖示檔案路徑 | 否 |
| `--api-url` | 後端地址 | 否 |
| `--username` | 使用者名稱 | 否 |
| `--password` | 密碼 | 否 |

### 認證

釋出介面需要 Basic Auth 認證：

```powershell
# 方式1: 環境變數
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

# 方式2: 命令列參數
py Scripts\publish_plugin.py ... --username your-user --password your-password
```

## backend_client.py - 後端客戶端

共享的後端上傳模組，為其他指令碼提供認證和上傳功能。

### 主要功能

- 認證憑據解析（環境變數 -> 預設值）
- 上傳 URL 建置
- 後端預檢（健康檢查 + 就緒檢查）
- 流式 PUT 上傳
- 認證 multipart POST

### 使用示例

```python
from backend_client import (
    RemoteUploadSettings,
    preflight_remote_upload,
    upload_file,
    resolve_upload_credentials,
)

# 解析憑據
username, password = resolve_upload_credentials()

# 配置上傳設定
settings = RemoteUploadSettings(
    base_url="http://localhost:9998",
    folder_name="Plugins/MyPlugin",
    username=username,
    password=password,
)

# 預檢
if preflight_remote_upload(settings):
    # 上傳檔案
    upload_file(settings, "path/to/file.cvxp")
```

### 預檢邏輯

上傳前會進行兩步檢查：

1. **健康檢查** (`GET /api/health`) - 確認後端服務可用
2. **就緒檢查** (`GET /api/ready`) - 確認後端已準備好接收上傳

如果後端返回 404（舊版本後端），則視為相容模式繼續上傳。

## file_manager.py - 檔案管理

檔案管理工具類。

### 功能

- 檔案上傳管理
- 路徑處理
- 進度顯示

### 使用示例

```python
from file_manager import FileManager

fm = FileManager()

# 上傳檔案
fm.upload_file("path/to/file.zip", "ColorVision/Update")
```

## 指令碼測試

每個指令碼都有對應的測試檔案：

| 測試檔案 | 說明 |
|----------|------|
| `test_backend_client.py` | 後端客戶端測試 |
| `test_build.py` | 建置指令碼測試 |
| `test_file_manager.py` | 檔案管理測試 |
| `test_build_update.py` | 更新建置測試 |
| `test_publish_plugin.py` | 外掛釋出測試 |

### 執行測試

```powershell
# 執行單個測試
python Scripts\test_backend_client.py

# 使用 pytest
pytest Scripts\test_*.py -v
```

## 故障排查

### 上傳失敗 (401 Unauthorized)

- 檢查環境變數或預設憑據是否正確
- 確認後端 `config.json` 中的 `upload_auth` 配置

### 上傳失敗 (Connection Error)

- 檢查後端服務是否執行
- 確認網路連線
- 驗證 `COLORVISION_UPLOAD_URL` 配置

### 建置失敗

- 確認 MSBuild 路徑正確
- 檢查 Advanced Installer 是否安裝
- 驗證解決方案是否能正常編譯

### 版本號讀取失敗

- 確認目標 DLL/EXE 存在
- 檢查檔案版本資訊是否正確嵌入

## 最佳實踐

1. **使用環境變數** - 避免在指令碼中硬編碼敏感資訊
2. **預檢失敗處理** - 指令碼會在後端不可用時提供清晰的錯誤資訊
3. **版本號管理** - 確保 DLL/EXE 的版本資訊與釋出版本一致
4. **測試先行** - 在正式釋出前使用測試指令碼驗證功能
