#EventVWR プラグイン

このページでは、現在ウェアハウスにある実際の EventVWR プラグイン実装についてのみ説明します。「完全なサブシステム マニュアル + 理想化された API テーブル」の古いドラフトは今後保守されません。

## まず、このプラグインが今何をしているのか見てみましょう

現在のソース コードから判断すると、EventVWR は主に 2 つのことを行います。

- 読み取り専用の Windows アプリケーション イベント エラー表示ウィンドウを提供します。
- Windows エラー報告の LocalDumps レジストリ キーを書き込んだりクリアしたりするための一連のダンプ構成メニューを提供します。

したがって、これは複雑な診断プラットフォームではなく、「イベント ウィンドウ + ダンプ構成メニュー」という 2 つの非常に直接的な機能チェーンです。

## 現時点で最も重要なファイル

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/ExportEventWindow.cs`
- `Plugins/EventVWR/EventWindow.xaml(.cs)`
- `Plugins/EventVWR/Dump/DumpConfig.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/EventVWR/manifest.json`

プラグインがどのようにホストに入るのか、イベント ウィンドウを開く方法、およびダンプ設定を変更する方法を理解したいだけの場合は、これらのいくつかのコードで十分です。

## 現在ホストに接続されている 2 つのメニュー チェーン

### イベントウィンドウのエントリ

`ExportEventWindow` は `MenuItemBase` を継承しており、現在 `Help` メニューの下でハングしています。

- `OwnerGuid = "Help"`
- `GuidId = "EventWindow"`
- `Order = 1000`

実行すると、`EventWindow` ダイアログ ボックスが開きます。

このエントリには重要な制約もあります。`Execute()` には現在 `RequiresPermission(PermissionMode.Administrator)` があり、これは純粋なローカル補助メニューではなく、ホスト許可モードの影響を受けることを示しています。

### ダンプ設定エントリ

`MenuDump` は `Help` メニューの下の親メニュー項目でもあり、`MenuThemeProvider` は引き続きそのサブメニューを提供します。

- 各 `DumpType` 列挙項目
- クリアDMP
- DMP の保存

したがって、EventVWR には現在、ウィンドウ エントリが 1 つだけではなく、ヘルプ メニューの下に 2 つの独立した機能があります。

## イベント ウィンドウの現在の動作方法

`EventWindow.xaml.cs` のロジックは非常に単純です。

1. ウィンドウの初期化時に Windows `Application` イベント ログを開きます。
2. `EventLogEntry` をすべて読みます。
3. `EntryType == Error` のイベントのみを保持します。
4. `TimeGenerated` に従って逆の順序で配置します。
5. 結果を左側のリストにバインドします。
6. レコードを選択すると、詳細領域に `Message` が表示されます。

これは、現在のウィンドウには複雑なフィルター、サーチャー、非同期ページング ロジックがなく、本質的には「エラー イベント高速ブラウザ」であることを意味します。

## ダンプ構成は現在どのように実装されていますか?

`DumpConfig` は、実際のシステム設定を書き込む役割を果たします。現在のコアポイントは次のとおりです。

- ターゲットのレジストリ パスは `HKLM\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps` です。
- デフォルトの LocalDumps 構成が最初に読み取られ、次に現在のプロセスに対応する `LocalDumps\{Name}.exe` が上書きされます。
- 現在管理されている主要なフィールドは次のとおりです。
  - `DumpFolder`
  - `DumpCount`
  - `DumpType`
  - `CustomDumpFlags`

設定の書き込みと設定のクリアにはどちらも管理者権限が必要です。現在の管理者ではない場合は、続行せずにポップアップ ウィンドウが表示されます。

レジストリ構成に加えて、`SaveDump()` は `DumpHelper.WriteMiniDump(...)` を呼び出して、現在のプロセスをターゲット ディレクトリにダンプします。

## 現在のマニフェスト情報

`manifest.json` によると、このプラグインによって現在公開されている基本情報は次のとおりです。

- `Id = "EventVWR"`
- `name = "事件插件"`
- `version = "1.0"`
- `dllpath = "EventVWR.dll"`
- `requires = "1.3.15.10"`

これは、古いドキュメントの「ターゲット フレームワーク、依存関係マトリックス、完全な API テーブル」よりも、現在のプラグイン読み込みモデルが実際に考慮している情報に近いです。

## 現時点で間違いやすいいくつかの点

### これは完全なインシデント診断センターではありません

現在の実装では、Windows アプリケーション ログのエラー エントリを読み取り、メッセージ テキストを表示するだけです。複数のログ ソースの高度な取得、エクスポート、分析を備えたプラットフォームとしてこれを書き続けないでください。

### ダンプ構成はシステム レベルで記述されます

`DumpConfig` 現在の操作は、アプリケーションの内部構成ファイルではなく、HKLM の下の LocalDumps レジストリ キーです。このため、書き込みとクリーニングの両方に管理者権限が必要です。

### プラグインのエントリークラス自体は非常に軽いです

`EventVWRPlugins` は、主にヘッダーと説明を提供する非常に薄い `IPluginBase` シェルになりました。本当の関数の入り口はここではなく、メニュー項目と対応するウィンドウ/構成クラスにあります。

### 権限境界は 2 つの層に分かれています

- イベント ウィンドウのメニュー エントリ自体は `RequiresPermission(PermissionMode.Administrator)` の対象となります。
- Dump Registry Write and Clean は、実行時に管理者権限も二重チェックします。

1 つのレイヤーのみが文書化されている場合、その文書は現在の動作を過度に単純化することになります。

## 推奨される読む順序

1. `Plugins/EventVWR/ExportEventWindow.cs`
2. `Plugins/EventVWR/EventWindow.xaml.cs`
3. `Plugins/EventVWR/Dump/DumpConfig.cs`
4. `Plugins/EventVWR/Dump/MenuDump.cs`
5. `Plugins/EventVWR/manifest.json`

このようにして、最初にホストの入り口を確認し、次にウィンドウの動作とシステムレベルの構成ポイントを確認できます。

## 続きを読む

- [Plugins/README.md](../../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/system-monitor.md](./system-monitor.md)
