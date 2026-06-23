# 外掛市場後端 (Plugin Marketplace Backend)

ColorVision 外掛市場後端是一個基於 Python Flask 的輕量級服務，用於管理外掛的釋出、下載和版本控制。

## 功能概述

後端服務提供以下核心功能：

- **React Web 門戶** - 瀏覽、搜尋、下載、後台釋出和運維管理
- **REST API** - 為 WPF 桌面客戶端提供介面
- **下載統計** - 基於 SQLite 的下載統計

## 專案結構

```
Web/Backend/
├── app.py              # Flask 應用主入口 (React SPA + API + 檔案服務)
├── app_changelog.py    # 更新日誌管理模組
├── app_releases.py     # 應用版本釋出管理
├── catalog_view_models.py  # 外掛目錄檢視模型
├── config.json         # 配置檔案
├── download_stats.py   # 下載統計模組
├── feedback_service.py # 使用者反饋服務
├── marketplace.db      # SQLite 資料庫 (自動建立，gitignored)
├── marketplace_services.py # 市場資料服務
├── package_publish.py  # 包釋出驗證和處理
├── page_contexts.py    # 頁面上下文建置
├── plugin_marketplace.py   # 外掛市場核心邏輯
├── plugin_queries.py   # 外掛查詢介面
├── requirements.txt    # Python 依賴
├── runtime_health.py   # 執行時健康檢查
├── storage_browser.py  # 儲存瀏覽器
├── storage_paths.py    # 儲存路徑管理
├── storage_uploads.py  # 上傳處理
├── update_retention.py # 更新包保留策略
├── routes/             # Flask 藍圖：站點資料、認證、後台 API、檔案服務
└── services/           # 索引、鑑權、任務排程和儲存事件服務
```

## 安裝和執行

### 環境要求

- Python 3.9+
- pip

### 安裝依賴

```bash
cd Web/Backend
pip install -r requirements.txt
```

### 配置檔案

編輯 `config.json`：

```json
{
    "storage_path": "H:\\ColorVision",
    "host": "0.0.0.0",
    "port": 9998,
    "debug": false,
    "secret_key": "your-secret-key",
    "app_release_keep_count": 5,
    "plugin_package_keep_count": 3,
    "upload_auth": {
        "username": "admin",
        "password": "admin"
    }
}
```

配置項說明：

| 配置項 | 說明 | 預設值 |
|--------|------|--------|
| `storage_path` | 外掛和應用的儲存路徑 | `storage/` |
| `host` | 監聽地址 | `0.0.0.0` |
| `port` | 監聽埠 | `9998` |
| `debug` | 除錯模式 | `false` |
| `secret_key` | Flask 金鑰 | 需修改 |
| `upload_auth` | 上傳認證憑據 | 需修改 |

### 啟動服務

```bash
# 使用預設配置
python app.py

# 指定儲存路徑
python app.py --storage H:\ColorVision

# 指定埠
python app.py --port 9999
```

## API 介面

### React Web 路由

| 路由 | 功能 |
|------|------|
| `GET /` | 首頁 — 儲存概覽、快速連結 |
| `GET /plugins` | 外掛市場 — 搜尋、分類、排序 |
| `GET /plugins/{id}` | 外掛詳情 — 版本列表、README、下載 |
| `GET /browse[/path]` | 檔案瀏覽器 |
| `GET /releases` | 釋出版本列表 |
| `GET /updates` | 更新包列表 |
| `GET /tools` | 工具下載列表 |
| `GET /admin[/path]` | 後台管理系統 |

### REST API

| 方法 | 路徑 | 說明 |
|------|------|------|
| GET | `/api/plugins` | 搜尋外掛（keyword, category, sort, pagination） |
| GET | `/api/plugins/{id}` | 外掛詳情 + 所有版本 |
| GET | `/api/plugins/{id}/latest-version` | 純文字最新版本 |
| POST | `/api/plugins/batch-version-check` | 批次版本檢查 |
| GET | `/api/plugins/categories` | 獲取所有分類 |
| GET | `/api/packages/{id}/{version}` | 下載外掛包 |
| POST | `/api/packages/publish` | 釋出新版本（需 Basic Auth） |
| GET | `/api/stats` | 下載統計 |
| GET | `/api/health` | 健康檢查端點 |
| GET | `/api/ready` | 就緒檢查端點 |

### 建置指令碼與客戶端下載路由

| 路由模式 | 說明 |
|----------|------|
| `PUT /upload/{path}` | 建置指令碼直接上傳製品 |
| `/D%3A/ColorVision/Plugins/{path}` | 桌面客戶端版本檢查和下載 |

## 認證

上傳介面使用 HTTP Basic Auth 進行保護：

```bash
# 使用 curl 示例
curl -u username:password -X POST http://localhost:9998/api/packages/publish \
  -F "PluginId=Spectrum" \
  -F "Version=1.0.0.1" \
  -F "package=@Spectrum-1.0.0.1.cvxp"
```

## 儲存結構

後端直接使用現有的檔案系統結構：

```
{storage_path}/
├── LATEST_RELEASE              # 應用最新版本號
├── CHANGELOG.md                # 應用更新日誌
├── History/                    # 歷史完整安裝包
├── Update/                     # 增量更新包
├── Plugins/                    # 外掛目錄
│   ├── Spectrum/
│   │   ├── LATEST_RELEASE
│   │   ├── manifest.json
│   │   ├── PackageIcon.png
│   │   ├── README.md
│   │   ├── CHANGELOG.md
│   │   └── Spectrum-1.0.0.1.cvxp
│   └── ...
└── Tool/                       # 工具下載
```

## 測試

後端包含完整的測試套件：

```bash
# 執行所有測試
python -m pytest

# 執行特定測試檔案
python test_app.py
python test_app_releases.py
python test_page_contexts.py
python test_upload_services.py
```

## 與建置指令碼整合

後端與 `Scripts/` 目錄下的建置指令碼整合：

- `publish_plugin.py` - 使用 `/api/packages/publish` 釋出外掛
- `build.py` - 上傳主程式安裝包
- `build_update.py` - 上傳增量更新包
- `build_spectrum.py` - 上傳 Spectrum 外掛

## 技術棧

| 層次 | 選型 | 版本 |
|------|------|------|
| 語言 | Python | 3.9+ |
| 框架 | Flask | >=3.0 |
| 前端 | React + TypeScript + Ant Design | 見 `Web/Frontend` |
| 資料庫 | SQLite | 內建 |
| Markdown 渲染 | markdown | >=3.8 |

## 訪問地址

服務啟動後：

- Web UI: http://localhost:9998
- 外掛市場: http://localhost:9998/plugins
- API: http://localhost:9998/api/plugins
- 檔案瀏覽: http://localhost:9998/browse

## 部署建議

### 生產環境部署

1. **使用 Gunicorn/uWSGI**

```bash
pip install gunicorn
gunicorn -w 4 -b 0.0.0.0:9998 app:app
```

2. **Nginx 反向代理**

```nginx
server {
    listen 80;
    server_name marketplace.example.com;

    location / {
        proxy_pass http://localhost:9998;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

3. **啟用 HTTPS**

使用 Let's Encrypt 或自簽名證書啟用 HTTPS。

4. **監控和日誌**

- 配置日誌輪轉
- 設定監控告警
- 定期檢查磁碟空間

## 故障排查

### 服務無法啟動

檢查埠是否被佔用：
```bash
netstat -an | findstr 9998
```

### 上傳失敗

- 確認 `upload_auth` 配置正確
- 檢查儲存路徑權限
- 檢視日誌錯誤資訊

### 資料庫錯誤

刪除自動生成的 `marketplace.db` 檔案，重啟服務後會自動重建。
