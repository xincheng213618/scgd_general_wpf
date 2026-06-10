# Flow 実行とデバッグ

このページは、現在の実装で確認できる Flow 実行入口と切り分け手順を扱います。重要なのは高度なデバッグ用語ではなく、正しい Flow template を選んだか、service が online か、start node があるか、失敗時にどこから狭めるかです。

## 現在使える実行入口

現在の実装では、少なくとも次の操作が明確です。

- 実行開始: `F6`
- 実行停止: `F7`

古い文書に `F5`、`F10`、breakpoint、single step と書かれていても、現在の UI と code binding を優先します。

## 実行前に確認すること

### 有効な Flow template が選択されている

有効な template が選択されていない場合、実行は開始されません。Flow window が開いているだけでなく、dropdown で実行対象の template が選ばれていることを確認します。

### start node が存在する

実行前に start node が確認されます。start node がない Flow は実行段階に入る前に失敗します。

### registry center と service token が利用できる

実装では registry center 接続と service list を先に確認します。service token が空の場合は、service refresh 後に再試行します。

### preprocessing が通る

Flow 開始前には preprocessing があります。ここで失敗すると Flow は cancel され、次の node には進みません。

## よくある実行順序

1. [Flow 設計](./design.md) で template 内容と start node を確認します。
2. 実行画面で同じ template を選択します。
3. 関連 device service が online であることを確認します。
4. `F6` または実行ボタンで開始します。
5. 実行中は log area、current node、progress を観察します。
6. 中止が必要な場合は `F7` または停止操作を使います。

## 実行中に見るもの

### current running node

実行中は現在動いている node 名が表示されます。止まって見える場合は、まずどの node で止まっているかを見ます。

### progress と duration

現在の実装は duration、last run time、前回 duration に基づく progress estimate を記録します。これは大まかな段階確認用であり、厳密な業務完了判定ではありません。

### result と status

完了後は batch status、duration、result summary が記録されます。停止した場合は canceled として記録されます。

## デバッグ時の切り分け

### 最初に失敗した node を探す

「Flow 全体が失敗」と捉えるより、最初に赤くなった node、または進まなくなった node を特定してから device、template、input data に戻ります。

### 開始前失敗か途中失敗かを分ける

- 開始前に止まる: template 未選択、start node missing、registry center disconnected、service token empty、preprocessing failed
- 開始後に止まる: node error、timeout、message mismatch

### log を先に見る

失敗時は最後の node、preprocessing failed、canceled、status message の有無を確認し、戻る層を決めます。

## Flow 引き継ぎで記録すること

Flow 引き継ぎはファイルを渡すだけでは不十分です。device、template、input、result、external system との関係を残します。

| 記録項目 | 書く内容 | 目的 |
| --- | --- | --- |
| Flow template | name、version、import source、last editor | 古い Flow の実行を防ぐ |
| start condition | start node、SN/batch input、project window、external trigger | 開始しない原因を判断する |
| device dependency | camera、motor、SMU、file service binding | device layer failure を切り分ける |
| template dependency | image template、calibration template、threshold | result drift を説明する |
| data destination | database table、export file、image folder、Socket/MES response | 結果がどこに出るか確認する |
| failure evidence | first failed node、log timestamp、error message | 次の担当者が再現できるようにする |

## 最小再テスト手順

現場再テストや upgrade 後は、いきなり full production chain を流さず、最小 Flow で確認します。

1. Flow design を開き、template と start node を確認します。
2. execution 画面で同じ template を選びます。
3. 関連 device service が online であることを確認し、必要なら device smoke action を行います。
4. production に影響しない SN、image、test input を準備します。
5. `F6` で実行し、start time、current node、final state、duration を記録します。
6. log、image、database、export file、external response のいずれかで同じ run を確認します。
7. 停止する場合は `F7` 後に canceled と記録されたことを確認します。

## 失敗切り分け表

| 失敗位置 | 典型症状 | 優先確認 |
| --- | --- | --- |
| 実行前 | run が始まらない、service refresh prompt、start node missing | registry center、service list、Flow template、start node |
| preprocessing | すぐ cancel、preprocessing failed | input parameter、template validity、project window context |
| device node | timeout、device no response、abnormal return code | device page、hardware、device Code、MQTT/serial/IP |
| template node | 完了するが結果が違う | template version、threshold、image source、calibration data |
| data node | Flow 完了後に結果が見えない | database write、batch/SN、export target、permission |
| external system | ColorVision は完了するが MES/Socket に届かない | protocol、port、project handler、response field |

## よくある問題

### Run しても開始しない

- registry center が connected か確認します。
- service list が refresh されているか確認します。
- 有効な Flow template が選択されているか確認します。
- start node が存在するか確認します。

### 開始後すぐ停止する

- preprocessing failed か確認します。
- log の最後の node と status を確認します。
- device 関連 node の場合は、対応する device page で connection と config を確認します。

### progress が動かず停止して見える

- current running node を確認します。
- device または message response を待っているか判断します。
- 必要なら `F7` で停止し、その node が依存する device service を単独で確認します。

### 手動では成功するが Flow では失敗する

- Flow が同じ device と template を参照しているか確認します。
- 実行前環境が手動テストと同じか確認します。
- 必要に応じて [デバイスサービス概要](../devices/overview.md) と [ログビューア](../interface/log-viewer.md) を確認します。

## 続けて読む

- [Flow 設計](./design.md)
- [デバイスサービス概要](../devices/overview.md)
- [ログビューア](../interface/log-viewer.md)
- [データ管理](../data-management/README.md)
- [現場操作受入チェックリスト](../field-operation-acceptance.md)

## 説明

- このページは現在確認できる実行入口と切り分け手順だけを扱います。
- 関連実装は主に `Engine/ColorVision.Engine/Templates/Flow/DisplayFlow.xaml.cs`、`ViewFlow.xaml.cs`、`FlowControl.cs`、`EngineCommands.cs` にあります。
