# データのエクスポートとインポート

現在の repository には、「すべてのデータをここから import/export する」統一センターはありません。実際には、settings、Flow template、result data はそれぞれ別の入口を持ちます。

## まず確認すること

import/export を行う前に、次の 3 点を確認します。

1. 対象は settings、Flow template、または特定 result module の data か。
2. 必要なのは全体 config migration か、単一 business object の export か。
3. その機能は data management center ではなく、特定 window に属していないか。

## 現在確認できる入口

### settings import/export

明確な menu entry があります。

- Tools -> Import/Export Settings

ここでは少なくとも次の操作を扱います。

- settings を `.cvsettings` に export
- `.cvsettings` から settings を import

目的が result data の移行ではなく software config の移行であれば、ここから始めます。

### Flow template import/export

Flow template の import/export は data management page ではなく、Flow designer 側で扱います。

- export current Flow
- import Flow
- import module

Flow 内容を移行する場合は、まず [Flow 設計](../workflow/design.md) を確認します。

### module 内 result export

一部の business window は独自の export を持ちます。

- Flow node analysis window の CSV export
- 一部 plugin または image/measurement window の CSV/image export

この種の export は特定 business object に強く結び付いているため、統一された global data export center として説明しません。

## object と entry の対応

| 交付対象 | 優先入口 | 交付前確認 |
| --- | --- | --- |
| software settings | Tools -> Import/Export Settings | `.cvsettings` を import でき、restart 後も key settings が残る |
| Flow template | Flow designer import/export | import 後に start node、device binding、template parameter が正しい |
| database record | database browser または business result page | SN、time、batch で同じ run を検索できる |
| CSV/Excel | 対象 business window または plugin export | field order、unit、PASS/FAIL、encoding が顧客要求と一致 |
| PDF/report | project window または plugin report entry | header、customer mark、result image、judgement item が正しい |
| image/overlay | image editor または result window | original image、ROI/POI、annotation coordinate、file name が対応する |
| Socket/MES response | project window、SocketProtocol、integration tool | request/response sample が保存され、status/Data field が正しい |

## export 交付前の受入

export は「button は押せるが、交付 file が違う」問題が起きやすいです。交付前に一度 end-to-end で確認します。

| 手順 | 操作 | 合格基準 |
| --- | --- | --- |
| 1 | 明確な SN または test batch で最小 Flow を実行 | query、export、external response が同じ識別子を使う |
| 2 | database または result window で source data を確認 | 空データや旧 batch ではない |
| 3 | target window から export | file が生成され、path/name を説明できる |
| 4 | export file を開いて fields を確認 | field order、unit、judgement、time、SN が顧客形式と一致 |
| 5 | sample file と screenshot を保存 | upgrade 後の再テスト基準にできる |

## export failure triage

| 現象 | 先に見る | 次に見る |
| --- | --- | --- |
| export button が見つからない | 対象が settings、Flow、business window のどれか | plugin/project docs が export support を明記しているか |
| file は生成されるが空 | source data があり、batch/SN が正しいか | export filter と field mapping |
| field がない/順序が違う | 正しい object/window を export しているか | project exporter、customer format version |
| image/overlay がずれる | result image と original image が同じ run か | ROI/POI coordinate、scaling、template version |
| external system が受け取らない | ColorVision が完了し result を生成したか | protocol、port、project handler、response field |
| migration 後に動作が変わる | export したのが settings だけではないか | old config、Flow template、database backup の同期 |

## Handoff Record Template

```text
export object:
source window:
source SN/batch/time:
database evidence:
export file path:
file format/version:
required fields:
sample screenshot:
external response sample:
known limitations:
owner/date:
```

## よくある利用順序

1. export する object を確認します。
2. global settings なら Import/Export Settings を使います。
3. Flow content なら [Flow 設計](../workflow/design.md) の import/export を使います。
4. result data または business data なら、対応 module window の export entry を探します。
5. database data が関係する場合は、[データベース操作](./database.md) で source range を確認します。

## このページで保証しないこと

特定 module window で確認できない限り、次の機能を統一機能として宣言しません。

- unified Excel export center
- unified JSON export center
- unified XML export center
- unified PDF report export center
- generic column mapping import wizard
- generic batch folder import wizard

plugin または window がこれらを持つ場合は、その module 自身のページに記載します。

## よくある問題

### export 入口が見つからない

- top-level menu を探し続ける前に、対象が settings、Flow、business result window のどれか確認します。
- 対応 module page で export entry を確認します。

### settings は export したが business result が移行されない

- `.cvsettings` は主に config migration 用であり、database result migration ではありません。
- actual result data は [データベース操作](./database.md) または対応 business module で扱います。

### Flow import は成功したが結果が違う

- 正しい Flow version を import したか確認します。
- [Flow 実行とデバッグ](../workflow/execution.md) で dependent device と template を確認します。
- 必要に応じて module import と Flow parameter を再確認します。

## 続けて読む

- [データ管理概要](./README.md)
- [データベース操作](./database.md)
- [Flow 設計](../workflow/design.md)
- [よくある問題](../troubleshooting/common-issues.md)
- [現場操作受入チェックリスト](../field-operation-acceptance.md)

## 説明

- このページは現在確認できる import/export path だけを扱います。
- settings import/export の実装は主に `UI/ColorVision.UI.Desktop/Settings/ExportAndImport/` にあります。
