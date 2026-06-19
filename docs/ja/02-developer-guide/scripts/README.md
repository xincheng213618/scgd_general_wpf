# スクリプトのビルドとリリース

ColorVision プロジェクトには、アプリケーションの構築、プラグインのパッケージ化、更新の公開、バックエンドのアップロードの管理のための Python スクリプトのセットが含まれています。

## スクリプトの概要

|スクリプト |機能 |
|------|------|
| `build.py` |メイン プログラムのインストール パッケージをビルドして公開します。
| `build_update.py` |増分更新パッケージをビルドする |
| `build_plugin.py` |互換性のあるエントリ、内部的に `package_cvxp.py` に転送 |
| `generate_shared_files.py` |ホスト出力ディレクトリをスキャンして `shared_files.json` を生成します。
| `package_cvxp.py` | `shared_files.json` に基づいてプラグインをストリップしてパッケージ化/アップロードする |
| `package_plugin.bat` |ウェアハウスでのプラグインのワンクリック構築と呼び出し `package_cvxp.py` |
| `package_project.bat` |ワンクリックでウェアハウス内でプロジェクトを構築および呼び出し `package_cvxp.py` |
| `package_cvxp_demo.bat` |外部プラグイン作成者向けの最小限のパッケージングの例 |
| `build_spectrum.py` | Spectrum プラグインの構築 |
| `publish_plugin.py` |プラグインをマーケット バックエンドに公開する |
| `backend_client.py` |バックエンドアップロード共有モジュール |
| `file_manager.py` |ファイル管理ツール |

## 環境設定

### 認証構成

このスクリプトは、バックエンド認証に次の環境変数を使用します。


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


::: ヒント
環境変数を設定しない場合、スクリプトはデフォルトの資格情報 `xincheng/xincheng` を使用します。
:::

### オプションの構成

|環境変数 |説明 |デフォルト値 |
|----------|------|----------|
| `COLORVISION_UPLOAD_URL` |バックエンドアップロードアドレス | `http://xc213618.ddns.me:9998` |
| `COLORVISION_UPLOAD_FOLDER` |アップロードフォルダー | `ColorVision` |
| `COLORVISION_UPLOAD_USERNAME` |ユーザー名をアップロード | `xincheng` |
| `COLORVISION_UPLOAD_PASSWORD` |アップロードパスワード | `xincheng` |
| `COLORVISION_REMOTE_UPLOAD` |リモートアップロードを有効にするかどうか | `1` (有効) |

## build.py - メインプログラムのビルド

メイン プログラムのインストール パッケージをビルドし、バックエンドにアップロードします。

### 使用法


```powershell
# 完整构建（编译 + 打包 + 上传）
py Scripts\build.py

# 跳过构建，仅上传最新安装包
py Scripts\build.py --skip-build

# 跳过远程上传
py Scripts\build.py --skip-remote-upload
```


### 機能の説明

1. MSBuild を使用してソリューションをコンパイルします。
2. Advanced Installer を使用してインストール パッケージを構築します
3. バックエンド プリフライトを実行します (`/api/health` + `/api/ready`)
4. インストール パッケージをバックエンドにアップロードします

### 前提条件

- Visual Studio 2022+ (MSBuild)
-高度なインストーラー
- Python の依存関係: `requests`、`tqdm`

## build_update.py - 増分更新ビルド

増分更新パッケージ (変更ファイルのみを含む) を作成します。

### 使用法


```powershell
py Scripts\build_update.py
```


### 動作原理

1. `ColorVision.exe` を読んで現在のバージョンを取得します
2. ベースラインとして過去のバージョンを検索する
3. ファイルの違いを比較して増分パッケージを生成する
4. 増分パッケージを `Update/` ディレクトリにアップロードします

### 出力ファイル

- `{History}/ColorVision-[{version}].zip` - 完全なパッケージ
- `{History}/update/ColorVision-Update-[{version}].cvx` - インクリメンタルパッケージ

## build_plugin.py - 互換性のあるエントリ

古いパッケージ実装は削除されました。

現在、`build_plugin.py` は互換性のあるエントリ ポイントとしてのみ予約されており、ウェアハウス内の一般的な呼び出しを `package_cvxp.py` に転送し、移行プロンプトを出力します。これを新しいスクリプトのメインのエントリ ポイントとして使用しないでください。

### 使用法


```powershell
py Scripts\build_plugin.py -t Projects -p ProjectARVR --no-upload
```


### 推奨される代替手段

- ウェアハウスのプラグイン: `Scripts\package_plugin.bat Spectrum --no-upload`
- 倉庫内のアイテム: `Scripts\package_project.bat ProjectARVR --no-upload`
- 倉庫の外: `py Scripts\package_cvxp.py --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows --no-upload`

##generate_shared_files.py - 共有ファイルテーブルの生成

ホスト プログラムの出力ディレクトリをスキャンし、`shared_files.json` を生成します。

### 使用法


```powershell
py Scripts\generate_shared_files.py

py Scripts\generate_shared_files.py `
    --root-dir C:\Users\17917\Desktop\scgd_general_wpf\ColorVision\bin\x64\Release\net10.0-windows `
    --output C:\temp\shared_files.json
```


### 出力内容

- `generated_at`: 生成時間
- `shared_files`: ホスト ディレクトリ内のすべての相対ファイル パス

### フィルタルール

- `Plugins` ディレクトリを自動的に無視します
- `Log` ディレクトリを自動的に無視します
- 通常、ホスト共有ファイルの変更後に再生成する必要があるのは 1 回だけです

## package_cvxp.py - 単一ファイルのパッケージのアップロード

単一ファイル スクリプトは `shared_files.json` を読み取り、共有ファイルと `.pdb` を削除して、直接アップロードできる `.cvxp` を生成します。

### 使用法


```powershell
# 仅本地打包
py Scripts\package_cvxp.py --project-file Plugins\Spectrum\Spectrum.csproj --build --no-upload

# 指定编译输出目录
py Scripts\package_cvxp.py `
    --src-dir Plugins\Spectrum\bin\x64\Release\net10.0-windows `
    --plugin-root Plugins\Spectrum

# 仅传编译输出目录，自动推断插件根目录
py Scripts\package_cvxp.py `
    --src-dir C:\src\MyPlugin\bin\x64\Release\net10.0-windows `
    --no-upload
```


### パラメータ

|パラメータ |説明 |デフォルト値 |
|------|------|----------|
| `--src-dir` |プラグインのコンパイル出力ディレクトリ |空 |
| `--project-file` |プラグイン `.csproj` パス |空 |
| `--plugin-root` |プラグインのルート ディレクトリ。`README.md` | などの追加ファイルを補足するために使用されます。自動推論 |
| `--plugin-name` |プラグイン名 |自動推論 |
| `--shared-files` | `shared_files.json` パス;渡されない場合、スクリプトと同じディレクトリ内のファイルが最初に読み取られます。自動検索 |
| `--output-dir` | `.cvxp` 出力ディレクトリ | `Scripts/` |
| `--build` | `dotnet build` をパッケージ化する前に実行してください。閉じる |
| `--dotnet` | `--build` `dotnet` コマンドを使用 | `dotnet` |
| `--no-upload` |パッケージのみですがアップロードはしません |閉じる |
| `--keep-package` |アップロード後にローカル パッケージを保持する |閉じる |

### パッケージ化ロジック

1. `shared_files.json` を読む
2. プラグインの出力ディレクトリを移動します。
3. すべての `.pdb` ファイルをフィルタリングする
4. `shared_files.json` に存在するすべての共有ファイルをフィルタリングします。
5. `stripped_files.json` と書き込みます
6. `.cvxp` としてパッケージ化
7. `--no-upload` が指定されていない場合は、パッケージと `LATEST_RELEASE` をアップロードします

### ディレクトリから直接転送する

`--src-dir` が `PluginName/bin/x64/Release/net10.0-windows` や `PluginName/bin/Release/net10.0-windows` のようなディレクトリを指している場合、スクリプトは自動的に `PluginName` ディレクトリを `plugin_root` として識別するため、`--plugin-root` が渡されない場合でも、プロジェクト ルート ディレクトリ内の `README.md`、`CHANGELOG.md`、`manifest.json`、`PackageIcon.png` を取得できます。

## package_plugin.bat - ウェアハウス内のプラグインに簡単にアクセスできます

このバッチ プロセスは、ウェアハウス内のプラグイン プロジェクトによってのみ使用されます。自動的に `.venv` を見つけて `package_cvxp.py --build` を自動的に呼び出すため、各プラグイン ディレクトリ内の `.bat` ファイルは転送用に 1 行のみ保持できます。

### 使用法


```powershell
Scripts\package_plugin.bat Spectrum --no-upload
```


## package_project.bat - ウェアハウス内のプロジェクトへのクイックエントリー

このバッチは `package_plugin.bat` に似ていますが、ターゲット ディレクトリが `Projects/*/*.csproj` に変更されています。顧客プロジェクトまたはプロジェクトベースのプラグインに適しています。

### 使用法


```powershell
Scripts\package_project.bat ProjectARVR --no-upload
```


## package_cvxp_demo.bat - 外部配信の例

このバッチ処理は、倉庫の外での使用シナリオを対象としています。 `package_cvxp.py`、`shared_files.json`、およびこのデモを同じディレクトリに配置します。 `SRC_DIR` の内部を変更した後、直接パッケージ化できます。

### 使用法


```powershell
Scripts\package_cvxp_demo.bat
```


## build_spectrum.py - スペクトラム プラグインのビルド

Spectrum プラグイン専用に最適化されたビルド スクリプト。

### 使用法


```powershell
# 构建并上传
py Scripts\build_spectrum.py --upload

# 仅构建不上传
py Scripts\build_spectrum.py
```


### 機能

- .zip および .cvxp 出力形式をサポート
- .cvxp パッケージがマップされたプラグイン サーバー パスにコピーされました
- 認証を使用した .zip パッケージのアップロード

## public_plugin.py - プラグインの公開

API を通じてプラグイン パッケージをプラグイン マーケットに公開します。

### 使用法


```powershell
# 基本发布
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp

# 完整参数
py Scripts\publish_plugin.py `
  -p Spectrum `
  -v 1.0.0.1 `
  -f Spectrum-1.0.0.1.cvxp `
  -n "Spectrum Plugin" `
  -d "光谱分析插件" `
  -a "Author Name" `
  -c "Analysis" `
  --changelog CHANGELOG.md `
  --icon PackageIcon.png

# 指定后端地址
py Scripts\publish_plugin.py -p Spectrum -v 1.0.0.1 -f Spectrum-1.0.0.1.cvxp --api-url http://localhost:9999
```


### パラメータ

|パラメータ |説明 |必須 |
|------|------|------|
| `-p, --plugin-id` |プラグインの一意の ID |はい |
| `-v, --version` |バージョン番号 (例: 1.0.0.1) |はい |
| `-f, --file` |パッケージファイルのパス |はい |
| `-n, --name` |表示名 |いいえ |
| `-d, --description` |説明 |いいえ |
| `-a, --author` |著者 |いいえ |
| `-c, --category` |分類 |いいえ |
| `-r, --requires` |エンジンの最小バージョン |いいえ |
| `--changelog` |ログ ファイルまたはテキストを更新する |いいえ |
| `--icon` |アイコンファイルのパス |いいえ |
| `--api-url` |バックエンドアドレス |いいえ |
| `--username` |ユーザー名 |いいえ |
| `--password` |パスワード |いいえ |

### 認証

公開インターフェイスには基本認証認証が必要です。


```powershell
# 方式1: 环境变量
$env:COLORVISION_UPLOAD_USERNAME = "your-user"
$env:COLORVISION_UPLOAD_PASSWORD = "your-password"

# 方式2: 命令行参数
py Scripts\publish_plugin.py ... --username your-user --password your-password
```


## backend_client.py - バックエンドクライアント

他のスクリプトの認証およびアップロード機能を提供する共有バックエンド アップロード モジュール。

### 主な機能

- 認証資格情報の解決 (環境変数 -> デフォルト値)
- URLビルドをアップロード
- バックエンド事前チェック (ヘルスチェック + 準備状況チェック)
- ストリーミングPUTアップロード
- 認証マルチパート POST

### 使用例


```python
from backend_client import (
    RemoteUploadSettings,
    preflight_remote_upload,
    upload_file,
    resolve_upload_credentials,
)

# 解析凭据
username, password = resolve_upload_credentials()

# 配置上传设置
settings = RemoteUploadSettings(
    base_url="http://localhost:9998",
    folder_name="Plugins/MyPlugin",
    username=username,
    password=password,
)

# 预检
if preflight_remote_upload(settings):
    # 上传文件
    upload_file(settings, "path/to/file.cvxp")
```


### プリフライトロジック

アップロードする前に、2 段階のチェックが実行されます。

1. **ヘルスチェック** (`GET /api/health`) - バックエンド サービスが利用可能であることを確認します
2. **準備状況チェック** (`GET /api/ready`) - バックエンドがアップロードを受信する準備ができていることを確認します

バックエンドが 404 (古いバージョンのバックエンド) を返した場合、互換モードであると見なされ、アップロードが続行されます。

## file_manager.py - ファイル管理

ファイル管理ツールのクラス。### 関数

- ファイルアップロード管理
- パスの処理
- 進行状況表示

### 使用例


```python
from file_manager import FileManager

fm = FileManager()

# 上传文件
fm.upload_file("path/to/file.zip", "ColorVision/Update")
```


## スクリプトテスト

各スクリプトには、対応するテスト ファイルがあります。

|テストファイル |説明 |
|----------|------|
| `test_backend_client.py` |バックエンドクライアントのテスト |
| `test_build.py` |ビルドスクリプトのテスト |
| `test_file_manager.py` |ファイル管理テスト |
| `test_build_update.py` |ビルド テストを更新する |
| `test_publish_plugin.py` |プラグインのリリーステスト |

### テストを実行する


```powershell
# 运行单个测试
python Scripts\test_backend_client.py

# 使用 pytest
pytest Scripts\test_*.py -v
```


## トラブルシューティング

### アップロードに失敗しました (401 不正)

- 環境変数またはデフォルトの認証情報が正しいかどうかを確認します
- バックエンド `config.json` の `upload_auth` 構成を確認します。

### アップロードに失敗しました (接続エラー)

- バックエンドサービスが実行されているかどうかを確認します
- ネットワーク接続を確認する
- `COLORVISION_UPLOAD_URL` 構成を確認する

### ビルドに失敗しました

- MSBuild パスが正しいことを確認します。
- Advanced Installerがインストールされているかどうかを確認します
- ソリューションが適切にコンパイルされることを確認します

### バージョン番号の読み取りに失敗しました

- 対象のDLL/EXEが存在することを確認してください。
・ファイルのバージョン情報が正しく埋め込まれているか確認する

## ベストプラクティス

1. **環境変数を使用する** - スクリプト内の機密情報のハードコーディングを避ける
2. **プリフライト障害の処理** - バックエンドが利用できない場合、スクリプトは明確なエラー メッセージを提供します。
3. **バージョン番号管理** - DLL/EXE のバージョン情報がリリースされたバージョンと一致していることを確認します。
4. **最初にテスト** - 正式リリース前にテスト スクリプトを使用して機能を検証します。
