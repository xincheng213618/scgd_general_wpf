# UI コンポーネント利用手順

このページは ColorVision の一般的な UI コンポーネントを、オペレーター、テストエンジニア、現場納入担当者の視点で説明します。いつ開くか、どこから入るか、何をするか、何をもって成功とするか、失敗時に最初に何を見るかをまとめます。

DLL リリースやソース変更は [UI DLL コンポーネント手冊](../../04-api-reference/ui-components/component-handbook.md) と [UI コンポーネントカタログ](../../04-api-reference/ui-components/control-catalog.md) を参照してください。このページはユーザー操作に限定します。

## コンポーネント概要

| UI コンポーネント | 使う場面 | 入口 | 主な操作 | 成功基準 | 失敗時の最初の確認 |
| --- | --- | --- | --- | --- | --- |
| メインワークベンチ | 起動後の日常操作起点 | 起動後に自動表示 | menu、search、workspace、status bar を確認 | device、Flow、image、log、plugin 入口を見つけられる | plugin loading、permission、language、layout |
| 上部メニュー | 全体 tool、device、plugin、help を開く | main window menu | 目的の tool/plugin を選ぶ | 対象 window/機能が開く | menu permission、plugin state、hotkey conflict |
| 検索ボックス | 機能の場所が分からない | main window search | keyword 入力、command/page を開く | 入口が見つかり開ける | keyword、plugin loading、search index |
| 状態バー | service、plugin、Socket、scheduler 状態を見る | bottom status bar | 状態表示や icon click | 現場状態と一致 | provider、service config、log |
| 設定ウィンドウ | global/module config を変更 | settings/options menu | 設定を探し、編集、保存、再起動確認 | 値が再起動後も残る | config path、permission、field type、readonly |
| プロパティエディタ | device/template/Flow node/config を編集 | property panel / dialog | category ごとに編集して保存 | 対象動作が変わる | metadata、validation、readonly、save path |
| ログビューア | startup/device/Flow/plugin を診断 | Help -> Log、`Ctrl+L` | time、level、keyword filter | 最初の有意味な error を見つける | log level、timestamp、module name |
| ターミナル | 現場で command/script/file check | terminal entry/workspace | command 実行、output 確認 | 結果が明確 | current directory、permission、environment |
| 画像エディター | image、overlay、ROI、video、3D、pseudo-color | result image、file open、workspace | zoom、annotation、measure、import/export | image と overlay が一致 | file path、writing、coordinate mapping |
| DB ブラウザ | MySQL/SQLite table を確認 | Tools -> Database Browser | source/table/search/page | record が見つかる、または未書込を確認 | connection、filter、page、primary key |
| Socket 管理 | local TCP server と message を確認 | Socket status icon | enabled、port、connection、history | external system が送受信できる | port conflict、protocol mode、handler |
| Scheduler | Quartz job を管理 | Tools -> Task Manager | create/pause/resume/run/history | next fire time と履歴が正しい | Cron、assembly、exception |
| Workspace/file tree | `.cvsln` と project file を整理 | Solution workspace | create/open/edit/layout | 正しい editor で開く | file type、editor registration、layout cache |
| Plugin marketplace | plugin/DLL を install/update | Help -> Marketplace | version、download、update | host が package を load できる | admin、network、manifest、version |
| Downloader | package/resource download | marketplace/download window | task、progress、retry | file が完全に保存 | aria2c、path permission、network |
| Wizard | step-by-step 初期化/設定 | wizard entry | 入力、next、finish | 各 step が validate される | required field、device、output path |

## 基本 UI

初回起動時は、menu が出ているか、search が動くか、workspace が window/image/Flow/editor を開けるか、status bar が service 状態を出しているかを確認します。メイン画面は入口を整理する場所であり、特定の業務 Flow そのものではありません。機能がない場合は、menu 未登録、plugin 未 load、permission 不足、project package 無効のどれかを先に分けます。

| コンポーネント | 使い方 | 引き継ぎ注意 |
| --- | --- | --- |
| Menu | 固定入口と admin tool は menu から開く | host、UI module、plugin、project package 由来の item が混在 |
| Search | 初心者が入口を探す時に使う | 見つからない場合は plugin 未 load の可能性 |
| Status bar | service と background task を見る | Socket/Scheduler icon から管理 window を開ける |

ボタンが反応しない場合は [ログビューア](./log-viewer.md) でクリック時刻付近の `Error` / `Warn` を確認します。

## 設定とパラメータ

設定ウィンドウは global/module config 用です。現場で変更する前に、global/device/Flow/project config のどれかを確認し、port、path、database、Socket、file server の旧値を控えます。保存後、必要に応じて restart または service refresh を行い、status bar、log、device page、project page で確認します。

プロパティエディタは device、template、Flow node、drawing object、plugin config、project config の parameter を編集します。期待した editor control が出ない場合、開発側は `Category`、`DisplayName`、`Description`、custom editor type、visibility metadata を確認します。

## 診断コンポーネント

| 問題 | Log keyword | 次のページ |
| --- | --- | --- |
| 起動失敗 | `Error`、`DllNotFoundException`、plugin name、config file | [FAQ](../troubleshooting/common-issues.md) |
| device 接続不可 | device name、port、IP、`timeout`、service name | [デバイスサービス概要](../devices/overview.md) |
| Flow 失敗 | Flow name、node name、template name、`failed` | [Flow 実行とデバッグ](../workflow/execution.md) |
| plugin がない | plugin folder、`manifest`、`deps.json`、DLL name | [既存プラグイン能力](../../04-api-reference/plugins/README.md) |
| data 未書込 | table、batch、SN、export path | [データ管理](../data-management/README.md) |

Terminal は現場納入担当と開発支援向けです。directory、network、script、helper tool の確認に使い、通常オペレーターの日常操作には使いません。

## データ、通信、スケジューリング

| コンポーネント | 用途 | 成功基準 | 失敗時 |
| --- | --- | --- | --- |
| DB browser | 結果書込と record 検索確認 | SN/時刻/batch で検索できる | connection、filter、primary key、readonly |
| Socket manager | TCP server と message history | 外部が接続し送受信できる | port、firewall、protocol mode、handler |
| Scheduler window | Quartz job 管理 | next fire time と history が正しい | Cron、scheduler state、job assembly |

Project package が Socket で起動する場合は [プロジェクト説明](../../00-projects/README.md) と対象 project page で event name、field、response を確認します。

## 画像と Workspace

画像エディターは viewer だけではなく、result、ROI/POI、annotation、overlay、video、pseudo-color、histogram、3D、CIE を扱います。表示異常時は、file が書き終わったか、coordinate mapping、overlay source の順に確認します。

Workspace は `.cvsln`、file tree、editor tabs、terminal、multi-image view を扱います。Engine Flow を実行する場所ではありません。顧客 project を実行する場合は project window または [プロジェクト説明](../../00-projects/README.md) へ移動します。

## 境界

| 症状 | 最初に見る場所 |
| --- | --- |
| window が開かない、menu がない、button 無反応 | UI 操作と log |
| device state 異常、camera capture 不能、motor 不動 | device service |
| Flow node 失敗、result 未生成 | Workflow と Engine |
| Socket は接続するが field が違う | project protocol または Socket handler |
| plugin が出ない、version incompatible | plugin loading と marketplace |
| UI DLL/native DLL がない | UI DLL release と installer |
| DB に result がない | data management、Flow write path、project export |

## 引き継ぎチェック

- main window から log、settings、DB browser、scheduler、marketplace を開く。
- 安全な config を一つ変更し、保存/再起動後に残ることを確認する。
- image を開き、zoom、annotation、property、export を行う。
- DB browser で time または SN から result を一件検索する。
- Socket 有効時は port、connection、message history を確認する。
- plugin/project package 納入時は menu、status icon、project window が出て開けることを確認する。

## 続けて読む

- [メインウィンドウ](./main-window.md)
- [プロパティエディタ](./property-editor.md)
- [ログビューア](./log-viewer.md)
- [画像エディター概要](../image-editor/overview.md)
- [DB 操作](../data-management/database.md)
- [UI DLL コンポーネント手冊](../../04-api-reference/ui-components/component-handbook.md)

