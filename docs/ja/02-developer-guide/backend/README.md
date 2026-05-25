# プラグイン マーケットプレイスのバックエンド

ColorVision プラグイン マーケット バックエンドは、プラグインの公開、ダウンロード、バージョン管理を管理するための Python Flask に基づく軽量のサービスです。

## 機能の概要

バックエンド サービスは、次のコア機能を提供します。

- **Web 管理インターフェイス** - プラグインの参照、検索、ダウンロード、アップロード
- **REST API** - WPF デスクトップ クライアント用のインターフェイスを提供します
- **レガシー互換性** - 古いバージョンのクライアントとの互換性のあるルーティングをサポートします。
- **ダウンロード統計** - SQLite ベースのダウンロード統計

## プロジェクトの構造


```
Backend/marketplace/
├── app.py              # Flask 应用主入口 (Web UI + API + 旧版兼容)
├── app_changelog.py    # 更新日志管理模块
├── app_releases.py     # 应用版本发布管理
├── catalog_view_models.py  # 插件目录视图模型
├── config.json         # 配置文件
├── download_stats.py   # 下载统计模块
├── feedback_service.py # 用户反馈服务
├── marketplace.db      # SQLite 数据库 (自动创建，gitignored)
├── marketplace_services.py # 市场数据服务
├── package_publish.py  # 包发布验证和处理
├── page_contexts.py    # 页面上下文构建
├── plugin_marketplace.py   # 插件市场核心逻辑
├── plugin_queries.py   # 插件查询接口
├── requirements.txt    # Python 依赖
├── runtime_health.py   # 运行时健康检查
├── storage_browser.py  # 存储浏览器
├── storage_paths.py    # 存储路径管理
├── storage_uploads.py  # 上传处理
├── update_retention.py # 更新包保留策略
├── static/             # 静态资源
└── templates/          # Jinja2 模板文件
    ├── base.html
    ├── index.html
    ├── plugins.html
    ├── plugin_detail.html
    ├── upload.html
    └── browse.html
```


## インストールして実行する

### 環境要件

-Python 3.9以降
- ピップ

### 依存関係をインストールする


```bash
cd Backend/marketplace
pip install -r requirements.txt
```


### 設定ファイル

`config.json` を編集します:


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


設定項目の説明:

|設定項目 |説明 |デフォルト値 |
|----------|------|----------|
| `storage_path` |プラグインとアプリケーションの保存パス | `storage/` |
| `host` |リスニングアドレス | `0.0.0.0` |
| `port` |リスニングポート | `9998` |
| `debug` |デバッグモード | `false` |
| `secret_key` |フラスコキー |修正が必要 |
| `upload_auth` |認証資格情報をアップロードする |変更する必要があります |

### サービス開始


```bash
# 使用默认配置
python app.py

# 指定存储路径
python app.py --storage H:\ColorVision

# 指定端口
python app.py --port 9999
```


## APIインターフェース

### Web UI ルーティング

|ルーティング |機能 |
|------|------|
| `GET /` |ホーム — ストレージの概要、クイック リンク |
| `GET /plugins` |プラグイン市場 - 検索、分類、並べ替え |
| `GET /plugins/{id}` |プラグインの詳細 — バージョン リスト、README、ダウンロード |
| `GET /upload` |アップロードページ |
| `POST /upload` |アップロードの処理中 |
| `GET /browse[/path]` |ファイルブラウザ |
| `GET /releases` |リリースバージョンリスト |
| `GET /updates` |パッケージリストを更新 |
| `GET /tools` |ツールダウンロードリスト |

### REST API

|方法 |パス |説明 |
|------|------|------|
|入手 | `/api/plugins` |検索プラグイン (キーワード、カテゴリ、並べ替え、ページネーション) |
|入手 | `/api/plugins/{id}` |プラグインの詳細とすべてのバージョン |
|入手 | `/api/plugins/{id}/latest-version` |プレーンテキストの最新バージョン |
|投稿 | `/api/plugins/batch-version-check` |バッチバージョンチェック |
|入手 | `/api/plugins/categories` |すべてのカテゴリを取得 |
|入手 | `/api/packages/{id}/{version}` |プラグイン パッケージをダウンロード |
|投稿 | `/api/packages/publish` |新しいバージョンを発行する (Basic 認証が必要) |
|入手 | `/api/stats` |統計をダウンロード |
|入手 | `/api/health` |ヘルスチェックエンドポイント |
|入手 | `/api/ready` |準備状況チェックエンドポイント |

### 旧バージョン互換ルーティング

|ルーティングモード |説明 |
|----------|------|
| `PUT /upload/{path}` |古いビルド スクリプトのアップロードとの互換性 |
| `/D%3A/ColorVision/Plugins/{path}` |古いクライアントのバージョン確認とダウンロードに対応 |

## 認証

アップロード インターフェイスは HTTP Basic 認証を使用して保護されています。


```bash
# 使用 curl 示例
curl -u username:password -X POST http://localhost:9998/api/packages/publish \
  -F "PluginId=Spectrum" \
  -F "Version=1.0.0.1" \
  -F "package=@Spectrum-1.0.0.1.cvxp"
```


## ストレージ構造

バックエンドは既存のファイル システム構造を直接使用します。


```
{storage_path}/
├── LATEST_RELEASE              # 应用最新版本号
├── CHANGELOG.md                # 应用更新日志
├── History/                    # 历史完整安装包
├── Update/                     # 增量更新包
├── Plugins/                    # 插件目录
│   ├── Spectrum/
│   │   ├── LATEST_RELEASE
│   │   ├── manifest.json
│   │   ├── PackageIcon.png
│   │   ├── README.md
│   │   ├── CHANGELOG.md
│   │   └── Spectrum-1.0.0.1.cvxp
│   └── ...
└── Tool/                       # 工具下载
```


## テスト

バックエンドには完全なテスト スイートが含まれています。


```bash
# 运行所有测试
python -m pytest

# 运行特定测试文件
python test_app.py
python test_app_releases.py
python test_page_contexts.py
python test_upload_services.py
```


## ビルド スクリプトと統合する

バックエンドは、`Scripts/` ディレクトリ内のビルド スクリプトと統合されています。

- `publish_plugin.py` - `/api/packages/publish` を使用してプラグインを公開します
- `build.py` - メイン プログラム インストール パッケージをアップロードします
- `build_update.py` - 増分更新パッケージをアップロードする
- `build_spectrum.py` - スペクトル プラグインのアップロード

## テクノロジースタック

|階層 |選択 |バージョン |
|------|------|------|
|言語 |パイソン | 3.9+ |
|フレームワーク |フラスコ | >=3.0 |
|テンプレートエンジン |ジンジャ2 |内蔵 |
| CSSフレームワーク |ブートストラップ 5 | 5.x |
|データベース | SQLite |内蔵 |
|マークダウンレンダリング |値下げ | >=3.8 |

## アクセスアドレス

サービスの開始後:

- Web UI: http://localhost:9998
- プラグインマーケット: http://localhost:9998/plugins
- API: http://localhost:9998/api/plugins
- ファイルの参照: http://localhost:9998/browse

## 導入に関する推奨事項

### 実稼働環境の展開

1. **Gunicorn/uWSGI の使用**


```bash
pip install gunicorn
gunicorn -w 4 -b 0.0.0.0:9998 app:app
```


2. **Nginx リバース プロキシ**


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


3. **HTTPS を有効にする**

Let's Encrypt または自己署名証明書を使用して HTTPS を有効にします。

4. **監視とロギング**

- ログローテーションの設定
-監視アラームを設定する
- ディスク容量を定期的にチェックする

## トラブルシューティング

### サービスを開始できません

ポートが占有されているかどうかを確認します。

```bash
netstat -an | findstr 9998
```


### アップロードに失敗しました

- `upload_auth` が正しく構成されていることを確認します
- ストレージパスの権限を確認する
- ログエラーメッセージの表示

### データベースエラー

自動生成された `marketplace.db` ファイルを削除すると、サービスの再起動後に自動的に再構築されます。