# 現在の UI DLL 文書カバレッジ

このページは `UI/` ディレクトリのモジュール台帳です。引き継ぎ担当者は、現在存在する UI プロジェクト、対応する文書ページ、DLL をリリースまたは置換するときの証跡をここで確認してから、個別コンポーネントページやリリース手順に進みます。

更新日: 2026-06-10。

## 現在の結論

- `UI/` には現在 10 個の実プロジェクト ディレクトリがあり、各ディレクトリに `.csproj` と `README.md` があります。
- 10 個すべてのプロジェクトに `docs/04-api-reference/ui-components/` 配下の対応文書ページがあります。
- 10 個すべてのプロジェクトで `GeneratePackageOnBuild` が有効です。リリース時はホスト出力だけでなく NuGet パッケージの内容も確認します。
- `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI`、`ColorVision.Core`、`ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` は `net8.0-windows7.0` と `net10.0-windows7.0` を対象にします。
- `ColorVision.ImageEditor`、`ColorVision.UI.Desktop`、`ColorVision.Solution` は現在 `net10.0-windows7.0` のみを対象にします。
- `ColorVision.Core` は native/runtime リスクが最も高い UI パッケージです。`ColorVision.ImageEditor` は画像操作と結果 overlay のリスクが最も高く、`ColorVision.UI.Desktop` はデスクトップ ツールと現地運用入口のリスクが最も高いパッケージです。

## カバレッジ表

| UI プロジェクト | プロジェクトファイル | ソース README | 文書ページ | リリース形態 | 引き継ぎ重点 |
| --- | --- | --- | --- | --- | --- |
| `UI/ColorVision.Common/` | `ColorVision.Common.csproj` | あり | [ColorVision.Common](./ColorVision.Common.md) | DLL + NuGet | MVVM 基盤、プラグイン インターフェイス、共有契約、ステータスバー メタデータ |
| `UI/ColorVision.Themes/` | `ColorVision.Themes.csproj` | あり | [ColorVision.Themes](./ColorVision.Themes.md) | DLL + NuGet | テーマ リソース、ウィンドウ スタイル、ライト/ダーク切替 |
| `UI/ColorVision.UI/` | `ColorVision.UI.csproj` | あり | [ColorVision.UI](./ColorVision.UI.md) | DLL + NuGet | 設定、メニュー、プラグイン読み込み、PropertyGrid、ショートカット、多言語 |
| `UI/ColorVision.Core/` | `ColorVision.Core.csproj` | あり | [ColorVision.Core](./ColorVision.Core.md) | DLL + NuGet + native runtime | `HImage`、OpenCV helper、画像/動画相互運用、`runtimes/win-x64/native` |
| `UI/ColorVision.Database/` | `ColorVision.Database.csproj` | あり | [ColorVision.Database](./ColorVision.Database.md) | DLL + NuGet | SqlSugar DAO、データベース ブラウザ、MySQL/SQLite 接続 |
| `UI/ColorVision.SocketProtocol/` | `ColorVision.SocketProtocol.csproj` | あり | [ColorVision.SocketProtocol](./ColorVision.SocketProtocol.md) | DLL + NuGet | ローカル TCP サービス、JSON/Text 配信、メッセージ履歴 |
| `UI/ColorVision.Scheduler/` | `ColorVision.Scheduler.csproj` | あり | [ColorVision.Scheduler](./ColorVision.Scheduler.md) | DLL + NuGet | Quartz スケジュール、タスク復旧、履歴、管理ウィンドウ |
| `UI/ColorVision.ImageEditor/` | `ColorVision.ImageEditor.csproj` | あり | [ColorVision.ImageEditor](./ColorVision.ImageEditor.md) | DLL + NuGet + 画像リソース | `ImageView`、`DrawCanvas`、ツールバー、結果 overlay、3D/CIE ビュー |
| `UI/ColorVision.UI.Desktop/` | `ColorVision.UI.Desktop.csproj` | あり | [ColorVision.UI.Desktop](./ColorVision.UI.Desktop.md) | WinExe + NuGet | 設定ウィンドウ、ウィザード、マーケットプレイス、ダウンロード ツール、DLL バージョン表示 |
| `UI/ColorVision.Solution/` | `ColorVision.Solution.csproj` | あり | [ColorVision.Solution](./ColorVision.Solution.md) | DLL + NuGet | ワークスペース、エディター、ターミナル、複数画像表示、ローカル RBAC、プロジェクト管理 |

## リリース境界

| 境界 | モジュール | 最初に読むページ | 現地リスク |
| --- | --- | --- | --- |
| 共有基盤層 | `ColorVision.Common`、`ColorVision.Themes`、`ColorVision.UI` | [UI DLL コンポーネントハンドブック](./component-handbook.md)、[UI DLL リリースマトリクス](./release-matrix.md) | メニュー、設定、プラグイン入口、テーマ リソースの失敗が多数の上位ウィンドウに影響します |
| 画像と native 層 | `ColorVision.Core`、`ColorVision.ImageEditor` | [UI DLL リリース証跡と現地確認表](./dll-release-evidence.md)、[UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md) | native DLL、画像リソース、overlay 登録の欠落は結果表示に直接影響します |
| データとサービスウィンドウ層 | `ColorVision.Database`、`ColorVision.SocketProtocol`、`ColorVision.Scheduler` | [UI DLL リリースプレイブック](./ui-dll-release-playbook.md)、[UI コンポーネントカタログ](./control-catalog.md) | データソース、Socket リスナー、スケジュール履歴、バックグラウンドタスク診断がこれらのウィンドウに依存します |
| デスクトップツールとワークスペース層 | `ColorVision.UI.Desktop`、`ColorVision.Solution` | [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md)、各モジュールページ | マーケットプレイス、ダウンロード ツール、Solution ワークスペース、RBAC、ローカルプロジェクト管理がここに集約されます |

## 証跡

この監査では次の証跡を使用しました。

- `Get-ChildItem UI -Directory`: 現在の実 UI プロジェクト ディレクトリを確認。
- 各 `UI/ColorVision.*/` の `.csproj`: target framework、`VersionPrefix`、`GeneratePackageOnBuild`、`PackageReadmeFile`、リソースコピー規則を確認。
- 各 `UI/ColorVision.*/README.md`: パッケージに入る README の出所を確認。
- `docs/04-api-reference/ui-components/*.md`: 各 UI プロジェクトの専用文書ページを確認。
- `Directory.Build.props`: 共通メタデータ、作者、リポジトリ URL、`ColorVision.snk` による条件付き strong-name signing を確認。

## 重点リスク

| リスク | 影響モジュール | 確認方法 |
| --- | --- | --- |
| native runtime の欠落 | `ColorVision.Core` | NuGet パッケージとホスト出力に `runtimes/win-x64/native` 配下の OpenCV/helper DLL が含まれるか確認 |
| 結果 overlay が表示されない | `ColorVision.ImageEditor`、Engine 結果表示チェーン | まず [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md)、次に Engine の [結果引き継ぎチェーン](../engine-components/result-handoff-chain.md) を確認 |
| メニューまたはプラグイン入口が表示されない | `ColorVision.UI`、`ColorVision.Common`、`ColorVision.UI.Desktop` | メニュー登録、プラグイン discovery、権限、設定読み込みを確認 |
| デスクトップツールのファイル欠落 | `ColorVision.UI.Desktop` | `OutputType=WinExe`、WebView2、CSS、`aria2c.exe`、リソースコピー規則を確認 |
| ワークスペース機能の異常 | `ColorVision.Solution` | エディター、ターミナル、複数画像表示、ローカル RBAC、プロジェクト ディレクトリ権限を確認 |
| net8/net10 の混在 | すべての UI DLL | ホスト target framework、プラグイン target framework、Engine package fallback version を確認 |

## 保守ルール

UI DLL を追加、削除、またはリネームする場合は、必ず次を更新します。

1. このカバレッジページ。
2. `docs/04-api-reference/ui-components/README.md` の package map。
3. `ColorVision.Xxx.md` などの専用モジュールページ。
4. [UI DLL コンポーネントハンドブック](./component-handbook.md)。
5. control、window、Provider、extension point が変わる場合は [UI コンポーネントカタログ](./control-catalog.md)。
6. [UI DLL リリースマトリクス](./release-matrix.md)。
7. [UI DLL リリース証跡と現地確認表](./dll-release-evidence.md)。
8. runtime discovery、menu、settings、service window が変わる場合は [UI ランタイムコンポーネント引き継ぎ](./ui-runtime-handoff.md)。
9. `docs/.vitepress/i18n/navigation-data.json` のサイドバー。

## 簡易監査コマンド

```powershell
Get-ChildItem UI -Directory | Sort-Object Name | Select-Object -ExpandProperty Name

Get-ChildItem docs/04-api-reference/ui-components -File |
  Sort-Object Name |
  Select-Object -ExpandProperty Name

Get-ChildItem UI -Directory | Sort-Object Name | ForEach-Object {
  $csproj = Get-ChildItem $_.FullName -Filter *.csproj -File | Select-Object -First 1
  $readme = Test-Path (Join-Path $_.FullName 'README.md')
  "$($_.Name): csproj=$($csproj.Name) readme=$readme"
}
```

ソース プロジェクトに文書ページがない場合、または文書ページが存在しないプロジェクトを指す場合は、まずこのページとサイドバーを修正し、その後翻訳版を同期します。
