# 既存プラグイン機能

この章では、現在の `Plugins/` ディレクトリに実在するプラグインだけを現在の機能として扱います。`Plugins/<Name>/`、`.csproj`、`manifest.json` がない名前は、現在のプラグイン入口には入れません。

## 現在のプラグイン

| プラグイン | ソース | manifest Id | 主な機能 | ドキュメント |
| --- | --- | --- | --- | --- |
| Conoscope | `Plugins/Conoscope/` | `Conoscope` | VAM/コノスコープ画像、フォーカスポイント、色域、コントラスト分析 | [Conoscope](./standard-plugins/conoscope.md) |
| Spectrum | `Plugins/Spectrum/` | `Spectrum` | 分光器接続、校正、測定、EQE、SQLite 結果 | [Spectrum](./standard-plugins/spectrum.md) |
| SystemMonitor | `Plugins/SystemMonitor/` | `SystemMonitor` | 性能監視、ステータスバー、ディスク/ネットワーク/プロセス情報 | [SystemMonitor](./standard-plugins/system-monitor.md) |
| EventVWR | `Plugins/EventVWR/` | `EventVWR` | Windows イベントエラー表示、Dump 設定 | [EventVWR](./standard-plugins/eventvwr.md) |
| WindowsServicePlugin | `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin` | CVWindowsService インストール、登録、MySQL/MQTT 設定 | [WindowsServicePlugin](./standard-plugins/windows-service.md) |

## 最初に読むページ

| 目的 | ページ |
| --- | --- |
| 現在のプラグインと文書の対応確認 | [現在のプラグイン文書カバレッジ](./current-plugin-coverage.md) |
| 機能、入口、リスクの横比較 | [プラグイン機能と引き継ぎマトリクス](./plugin-capability-matrix.md) |
| ロード、DLL、権限、Socket、パッケージの調査 | [プラグイン実行と引き継ぎプレイブック](./plugin-handoff-playbook.md) |
| リリースまたは現地置換の検収 | [既存プラグイン現地検収チェックリスト](./plugin-field-acceptance.md) |
| manifest、DLL version、`.cvxp`、native file、rollback を記録 | [プラグインリリース証跡とバージョン確認表](./plugin-release-evidence.md) |
| 新規プラグイン開発 | [プラグイン開発マニュアル](../../02-developer-guide/plugin-development/README.md) |

## ロードと納品モデル

プラグインは `UI/ColorVision.UI/Plugins/PluginLoader.cs` によりロードされます。出力ディレクトリの `Plugins/` 直下をスキャンし、`manifest.json`、`dllpath`、必要に応じて `.deps.json` の `ColorVision.*` 依存関係を確認してから `Assembly.LoadFrom(...)` で読み込みます。

推奨される納品形態:

```text
ColorVision/bin/x64/<Config>/net10.0-windows/Plugins/<PluginName>/
  <PluginName>.dll
  manifest.json
  README.md
  CHANGELOG.md
  PackageIcon.png        # optional
```

## 現在の一覧に含めない名前

Pattern、ImageProjector、ScreenRecorder は、現在 `Plugins/<Name>/` ソース、`.csproj`、`manifest.json` がないため、現在の機能入口ではありません。復帰させる場合は、ソース、manifest、README、CHANGELOG、ビルドコピー、パッケージ検証を先に復元してください。
