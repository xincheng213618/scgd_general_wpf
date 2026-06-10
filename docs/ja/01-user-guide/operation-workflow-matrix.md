# ユーザー操作ワークフローマトリクス

このページは、オペレーター、テストエンジニア、現場納入担当者が「何をしたいか」から文書入口を探すためのページです。ソース実装ではなく、日常操作、合格基準、失敗時の最初の確認点をまとめます。

## まず読む場面

| 場面 | このページで分かること |
| --- | --- |
| 初回セットアップ | インストール、起動、メイン画面、デバイス、Flow、データ確認の順序 |
| 現場納入 | プロジェクト、デバイス、Flow、出力、外部システムの受入確認 |
| 生産操作 | 実行、プロジェクト切替、結果確認、データ出力の入口 |
| トラブル対応 | UI、デバイス、Flow、データ、外部システムのどこから見るか |

## 操作目標から探す

| 操作目標 | 最初に読む | 主な操作 | 合格基準 | 失敗時の最初の確認 |
| --- | --- | --- | --- | --- |
| インストールと初回起動 | [インストール](../00-getting-started/installation.md)、[初回実行](../00-getting-started/first-steps.md) | 環境を入れ、ホストを起動し、設定/ログを確認 | メイン画面が開き、起動エラーがない | 要件、DLL 欠落、権限、ログ |
| メイン UI と部品を理解 | [メインウィンドウ](./interface/main-window.md)、[UI コンポーネント利用手順](./interface/ui-component-handbook.md) | メニュー、状態バー、設定、ログ、DB、Socket、Scheduler を確認 | デバイス、Flow、プラグイン、データ、診断入口を見つけられる | メニュー登録、権限、言語、状態バー provider |
| 設定を変更 | [プロパティエディタ](./interface/property-editor.md) | 設定オブジェクトを開き、編集、保存、再起動確認 | 値が再起動後も残る | 保存パス、readonly、型、権限 |
| ログと現場エラーを確認 | [ログビューア](./interface/log-viewer.md) | 時刻、レベル、キーワードで絞る | 最初の有意味な例外を見つける | ログレベル、ログフォルダ、対象モジュール |
| デバイス追加/設定 | [デバイスの追加と構成](./devices/configuration.md)、[デバイスサービス概要](./devices/overview.md) | デバイスリソースを作成し通信/パスを保存 | 一覧に出て状態が更新される | 種別、driver、port/IP、有効状態 |
| カメラ撮像 | [カメラサービス](./devices/camera.md)、[カメラ管理](./devices/camera-management.md) | 接続、露光/ゲイン設定、撮像/preview | 画像ファイルが生成され開ける | 実機、driver、露光、file server |
| Flow 設計 | [ワークフロー概要](./workflow/README.md)、[Flow 設計](./workflow/design.md) | node を追加し、接続、デバイス/テンプレートを紐付け | 保存して再度開ける | node parameter、device list、template name |
| Flow 実行/デバッグ | [Flow 実行とデバッグ](./workflow/execution.md) | Flow を選び、実行し、node 状態を見る | 完了または最初の失敗 node が明確 | start node、device state、template binding、log |
| 画像と overlay を確認 | [画像エディター概要](./image-editor/overview.md) | 結果画像を開き、layer、ROI/POI、pseudo-color を見る | 画像、layer、注釈が正しく表示 | file path、書き込み完了、overlay coordinate |
| DB/履歴を確認 | [データ管理](./data-management/README.md)、[DB 操作](./data-management/database.md) | DB/結果画面を開き SN/時刻で検索 | batch、Flow、result、project data が見つかる | 接続、filter、batch id、template name |
| データ出力/入力 | [データのエクスポートとインポート](./data-management/export-import.md) | 出力先、形式、範囲を選ぶ | CSV/Excel/PDF/image があり、field が正しい | path permission、field mapping、project exporter |
| 顧客プロジェクト実行 | [プロジェクト説明](../00-projects/README.md)、[プロジェクト能力マトリクス](../04-api-reference/projects/project-capability-matrix.md) | project window を開き、SN、flow group/template を選んで実行 | project が完了し顧客結果が出る | project config、ProcessGroup、Recipe/Fix、Socket/MES |
| プラグイン利用 | [既存プラグイン能力](../04-api-reference/plugins/README.md)、[プラグイン能力マトリクス](../04-api-reference/plugins/plugin-capability-matrix.md) | plugin window を開き、デバイス接続または機能実行 | menu/window/result/export が動く | manifest、plugin DLL、device dependency、admin |
| 外部システム連携 | [プロジェクト能力マトリクス](../04-api-reference/projects/project-capability-matrix.md)、[SocketProtocol](../04-api-reference/ui-components/ColorVision.SocketProtocol.md) | protocol、port、event/command、SN、response を確認 | 外部から trigger でき結果を受信 | port conflict、protocol mode、Socket/MES/Modbus |
| よくある問題 | [FAQ](./troubleshooting/common-issues.md) | 症状を分類し、ログと設定を確認 | 次の確認項目が明確 | log、config、device、Flow、project boundary |

## 役割別の日常フロー

| 役割 | 日常作業 | よく使う文書 |
| --- | --- | --- |
| オペレーター | 起動、project/Flow 選択、SN 入力、実行、PASS/FAIL、出力 | 本ページ、メイン画面、プロジェクト説明、データ出力 |
| テストエンジニア | デバイス設定、カメラ調整、Flow 調整、結果 field 確認 | デバイス、Flow、画像エディター、データ管理 |
| 現場納入 | インストール、plugin/project 受入、Socket/MES 連携、教育 | インストール、本ページ、project/plugin matrix、FAQ |
| 保守開発 | UI、Engine、plugin、project のどこを見るか判断 | 本ページ、UI コンポーネント、Engine matrix、UI catalog |

## トラブルの振り分け

| 症状 | 最初に分類 | 次の確認 |
| --- | --- | --- |
| メニュー/画面がない | plugin/project package loading | plugin matrix、project matrix、log |
| デバイス offline | service か hardware か | device page、driver、port/IP、service log |
| Flow が終わらない | node か device command が止まっている | execution page と最初の未完了 node |
| 結果はあるが出力が空 | project result mapping | project Process/Recipe/Fix/exporter |
| 画像に overlay がない | result display handler | image editor と Engine result chain |
| 外部へ結果が返らない | protocol、port、project window state | project matrix、SocketProtocol、log |

## 章の境界

- 操作手順と現場確認はユーザーガイドに置きます。
- コード構造と拡張点は [モジュール参照](../04-api-reference/README.md) に置きます。
- プロジェクト業務は [プロジェクト説明](../00-projects/README.md) に置きます。
- プラグイン開発は [プラグイン開発手順](../02-developer-guide/plugin-development/README.md) に置きます。
- UI DLL リリースは [UI コンポーネントと DLL リリース](../04-api-reference/ui-components/README.md) に置きます。

