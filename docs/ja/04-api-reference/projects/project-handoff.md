# プロジェクト引き継ぎマニュアル

プロジェクトパッケージは通常のツールプラグインではありません。顧客の検査順序、FlowEngine テンプレート、デバイス操作、Recipe/Fix、Socket/MES、結果エクスポートを一つの生産フローにまとめます。引き継ぎでは、単一の `Process` クラスではなく、誰が起動し、どの Flow が動き、どこに結果を書き、外部へどう返すかを先に確認します。リリースと現地置換の証跡は [プロジェクトパッケージリリース証跡とバージョン確認表](./project-release-evidence.md) に残します。

## 種別

| 種別 | プロジェクト | 重点 |
| --- | --- | --- |
| AR/VR ProcessGroup | `ProjectARVRPro/`, `ProjectLUX/` | `ProcessGroup`, `ProcessMeta`, FlowTemplate, Recipe, Socket |
| 軽量/履歴 AR/VR | `ProjectARVR/`, `ProjectARVRLite/` | 互換性、固定順序、CSV |
| 業務アルゴリズム | `ProjectBlackMura/`, `ProjectKB/` | パラメータ、結果モデル、レポート |
| 顧客固有 | `ProjectHeyuan/`, `ProjectShiyuan/` | 顧客プロトコル、現場設定、デバイス |
| 連携デモ | `ProjectARVRPro.IntegrationDemo/` | 外部 JSON 送受信 |

## 共通チェーン

| 手順 | コード入口 | 確認点 |
| --- | --- | --- |
| ロード | `manifest.json`, `PluginConfig/` | `Id`, `dllpath`, メニュー、最低ホスト版 |
| 初期化 | `InitTest()` | SN、旧結果リセット |
| フロー選択 | `ProcessManager`, `ProcessGroup` | active group、enabled steps、順序 |
| テンプレート | `ProcessMeta.FlowTemplate` | `TemplateFlow.Params` と一致 |
| 実行 | `RunTemplate()`, `RunAllAsync()` | batch、前処理、timeout、retry |
| 解析 | `IProcess.Execute(ctx)` | Engine 結果、Recipe/Fix |
| 集約 | `ObjectiveTestResult` | customer field が埋まるか |
| 保存/出力 | `ViewResultManager`, exporter | SQLite、CSV/XLSX/PDF、Legacy |
| 外部応答 | `Services/SocketControl.cs` | JSON/Text、status、final event |

## 高リスクフィールド

| フィールド | リスク |
| --- | --- |
| `ProcessMeta.FlowTemplate` | 名称不一致で Flow が起動しない |
| `ProcessMeta.ProcessTypeFullName` | クラス名変更で旧 config が読めない |
| `ProcessMeta.IsEnabled` | 自動化と最終結果 completeness に影響 |
| `ProcessMeta.SocketCode` | LUX の `T00XX` と対応 |
| `PictureSwitchConfig` | ARVRPro の Serial 切替、timeout、delay |

## チェックリスト

| 項目 | 合格条件 |
| --- | --- |
| manifest | `Id`, `dllpath`, `requires`, package name が一致 |
| メニュー | ホストからプロジェクトウィンドウを開ける |
| ProcessGroup | 有効 group と重要 steps がある |
| テンプレート | すべての `FlowTemplate` が実在する |
| Recipe/Fix | 開く、保存、再起動後も維持される |
| 外部プロトコル | init、switch/run、result を確認 |
| 結果 | SQLite、CSV/XLSX/PDF、upload path が書ける |
| 互換性 | 旧 config、旧 output、旧 protocol が記録済み |
