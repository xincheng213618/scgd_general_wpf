# Engine ランタイムオブジェクトマップ

このページは調査時のクラス索引です。ログや呼び出しスタックに出た名前から、どの業務チェーンに属するかを判断します。

| オブジェクト / クラス | 責任 | 主な発生元 | 最初の確認 |
| --- | --- | --- | --- |
| Startup initializers | 起動時に Engine 能力を登録 | アプリ起動、モジュール読込 | 実行有無、例外 |
| `ServiceManager` | デバイスサービス集合 | `Services/` | サービス存在、状態 |
| `DeviceServiceFactoryRegistry` | デバイスサービスの検索と作成 | 資源読込 | Type に Factory があるか |
| `DeviceService<TConfig>` | 単一デバイス実行時サービス | `Services/Devices/**` | Config、Resource ID、接続状態 |
| `MQTTControl` | MQTT 接続と命令通道 | `MQTT/`, デバイスサービス | Topic、Token、応答、タイムアウト |
| `TemplateControl` | テンプレート管理の入り口 | `Templates/` | 読込状態、辞書 ID |
| `TemplateModel<T>` | 具体テンプレートモデル | 各テンプレート配下 | `Code`, `Title`, 既定値 |
| `TemplateFlow` | Flow テンプレート管理 | `Templates/Flow/` | `.cvflow` 保存/インポート |
| `FlowControl` | Flow UI と実行接続 | Flow 画面 | ノード、接続、状態 |
| `FlowEngineControl` | Flow 実行制御 | `FlowEngineLib/` | 開始/終了ノード、`FlowCompleted` |
| `NodeConfiguratorRegistry` | ノード設定器登録 | `Templates/Flow/NodeConfigurator/` | テンプレート/デバイスを選べるか |
| `AlgResultMasterModel` | アルゴリズム主結果 | DAO / 結果サービス | 結果 ID、バッチ番号、状態 |
| `ViewResultAlg` | 結果表示の入り口 | 結果画面 | 明細と画像を取れるか |
| `IResultHandleBase` | 結果ハンドラ interface | `ViewHandleXxx` | `CanHandle` 命中 |
| `IViewResult` | 可視化結果モデル | 各結果種別 | 座標、ROI、表示項目 |
| `MeasureBatchModel` | バッチデータ | バッチ / 結果サービス | バッチ番号と結果関連 |
| `ObjectiveTestResult` | プロジェクト最終判定 | `Projects/*` | 顧客項目、結果マッピング、出力 |

## 使い方

1. ログまたはエラーメッセージからクラス名を取ります。
2. 表でデバイス、テンプレート、Flow、結果、プロジェクト出力のどれかを判断します。
3. 対応する専門ページへ移動します。
4. その後コードの具体メソッドを確認します。
