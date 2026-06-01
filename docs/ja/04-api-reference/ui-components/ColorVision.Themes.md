#ColorVision.テーマ

このページでは、現在 UI/ColorVision.Themes に実装されているテーマ機能についてのみ説明します。古いドキュメントの「テーマ開発フレームワーク + カスタム テーマ プラットフォーム + 完全な FAQ チュートリアル」という書き方を継続することはなくなりました。

## モジュールの配置

ColorVision.Themes は現在、WPF テーマ リソースおよびウィンドウ外観サポート ライブラリに近いものです。中核となる責任には主に 4 つのカテゴリがあります。

- テーマの列挙とテーマの切り替え入り口を定義
- リソース辞書をアプリケーションに挿入する
- Windows テーマの変更に応じてインターフェースを更新
- プロセスウィンドウのタイトルバーの色とアイコンのリンク

抽象化されて完成された「任意のカスタムテーマプラットフォーム」ではありません。古いドキュメントに記載されている Theme.Custom、ResourceDictionaryCustom、および完全なカスタム テーマの登録プロセスは、現在のコードに対応する実装がありません。

## 現時点で最も重要なファイル

現在のプロジェクトの構造から判断すると、最初に読む価値のあるものは次のとおりです。

- ThemeManager.cs: テーマ切り替えのメイン入口
- ThemeManagerExtensions.cs: アプリケーションおよびウィンドウの拡張メソッド
- Theme.cs: テーマ列挙定義
- Themes/ の下の XAML: 各テーマの基本スタイルとリソース ディクショナリ
- Controls/、Converter/、Utilities/: テーマ ライブラリに含まれるコントロール、コンバーター、およびツール コード

## キー入力タイプ

### テーママネージャー

ThemeManager は、現在のテーマ モジュールの中心的なオブジェクトです。それは次のことを担当します。

- CurrentTheme と CurrentUITheme を維持する
- 5 つのテーマを処理します: UseSystem、Light、Dark、Pink、Cyan
- テーマに応じて対応する ResourceDictionary リストをロードします
- Windows テーマの変更を監視する
- テーマを切り替えるときにテーマ変更イベントをトリガーします
- ウィンドウのタイトルバーの色を調整する

現在のリソース ディクショナリは、いくつかの固定リストのセットに編成されています。

- ResourceDictionaryBase: 基本的な共有スタイル
- ResourceDictionaryDark: ダークテーマのリソース
- ResourceDictionaryWhite: ライトテーマのリソース
- ResourceDictionaryPink: ピンクのテーマのリソース
- ResourceDictionaryCyan: シアンのテーマのリソース

これは、現在のトピック メカニズムが、実行時に新しいトピック タイプを登録できるオープン モデルではなく、「固定トピック列挙 + 固定リソース ディクショナリ コレクション」の実装であることを示しています。

### テーマ

現在のトピック列挙には 5 つの値のみがあります。

-UseSystem
-ライト
-ダーク
-ピンク
-シアン

UseSystem は別個のリソース セットではありませんが、ApplyTheme のときに現在の AppsTheme に対応するライト テーマまたはダーク テーマにマップされます。

### ThemeManagerExtensions

ThemeManagerExtensions には、実際に非常によく使用される 2 つのエントリ ポイントが用意されています。

- Application.ApplyTheme: テーマを適用します
- Application.ForceApplyTheme: テーマ リソースの強制再読み込み

さらに、Window.ApplyCaption はウィンドウの後に読み込まれます。

- タイトルバーの色を設定する
- 現在のテーマに応じてウィンドウアイコンを切り替えます
- トピックの変更をサブスクライブし、ウィンドウが閉じられたときにバインドを解除します

したがって、このモジュールはリソース ディクショナリを管理するだけでなく、ウィンドウ シェルの外観動作の一部も担当します。

## 現在のランタイムのメインチェーン

既存のトピックのリンクは次のリンクに近いです。

1. 上部の UI でテーマを選択します。
2. Application.ApplyTheme が ThemeManager.Current.ApplyTheme に転送されます。
3. 現在の選択が UseSystem の場合、最初に AppsTheme に解析されます。
4. ThemeManager は、Wpf.Ui とこのモジュールのリソース ディクショナリをテーマごとに Application.Resources.MergedDictionaries に追加します。
5. CurrentTheme と CurrentUITheme が更新され、変更イベントがトリガーされます。
6.ApplyCaption が呼び出されたウィンドウでは、それに応じてタイトル バーの色とアイコンが更新されます。

## システムテーマをフォローする方法

ThemeManager は、構築時に遅延初期化プロセスを開始します。現在の実装では、システム イベントをアプリケーション起動の初期段階で同期処理するのではなく、後でフックアップします。

主に次のものをリッスンします。

- SystemEvents.UserPreferenceChanged
- SystemParameters.StaticPropertyChanged

次に、レジストリの Personalize 項目を読んで判断します。

-AppsUseLightTheme
- SystemUsesLightTheme

したがって、「Follow the System」は現在、Windows レジストリ値とシステム イベントに依存しており、フレームワーク層によって自動的に提供される完全なトピック同期サービスではありません。

## タイトル バーの色とウィンドウ アイコン

ThemeManager は、DWM API を呼び出してウィンドウの外観を更新する役割も担います。

- ダークテーマにより、没入感のあるダークタイトルバーが可能になります
- ピンクとシアンのテーマのタイトル バーと境界線の色を直接設定
- ライトおよびフォローシステムモードはシステムのデフォルトのタイトルバーの色にリセットされます

Window.ApplyCaption は、現在のテーマに基づいてウィンドウ アイコン リソースも切り替えます。動作のこの部分は現在のモジュールの非常に実用的な値ですが、古いドキュメントでは明確に説明されていませんでした。

## 現在の実装の境界

### テーマの永続化は ThemeManager 自体によって行われるわけではありません

現在のテーマ設定では ColorVision.Themes 名前空間が使用されていますが、設定クラス ThemeConfig は実際には UI/ColorVision.UI/Themes にあります。

これは次のことを意味します。

- テーマのリソースとスイッチング コアは UI/ColorVision.Themes にあります。
- メニュー、ショートカット キー、設定項目の編集などの統合ロジックは UI/ColorVision.UI にあります

「テーマ構成システム」全体をテーマ プロジェクト自体に委譲しないでください。

### メニューとショートカット キーのエントリは UI 統合レイヤーにあります

現在のテーマのメニューとショートカット キーの入り口は主に次の場所にあります。

- UI/ColorVision.UI/テーマ/ThemesHotKey.cs

それは次のことを担当します。

- テーマメニュー項目の生成
- 切り替え時にThemeConfig.Instance.Themeに書き込む
- Application.ApplyTheme を呼び出す
- テーマを回転するための Ctrl + Shift + T ショートカット キーを提供します

したがって、テーマ モジュール自体が機能ベースを提供し、デスクトップ メニュー システムと実際にインターフェイスするのは UI レイヤーです。

### 古いドキュメントのカスタム テーマ拡張ポイントは存在しません

これらの古いドキュメントで主張されているインターフェイスは、現在のコードでは使用できません。

-テーマ.カスタム
- ThemeManager.ResourceDictionaryCustom
-ThemeConfig.FollowSystem

このタイプのコンテンツは、既存の機能として API リファレンスに記述することができなくなりました。

## このモジュールの読み方は現在、より適切です

### テーマを切り替える方法を知りたい

まずはご覧ください:

- テーママネージャー.cs
-ThemeManagerExtensions.cs
- テーマ.cs

### テーマがアプリケーション メニューと構成にどのようにアクセスするかを確認したい

まずはご覧ください:

- UI/ColorVision.UI/テーマ/ThemeConfig.cs
- UI/ColorVision.UI/テーマ/ThemesHotKey.cs

### テーマのリソースがどのようなものかを確認したい

まずはご覧ください:

-テーマ/Base.xaml
-テーマ/Dark.xaml
-テーマ/White.xaml
-テーマ/Pink.xaml
-テーマ/Cyan.xaml

## このページはそれ以上何もしません

このページでは、次のような危険性の高いコンテンツは管理されなくなります。

- 存在しないカスタムテーマ登録API
- 偽の ThemeConfig 設定フィールド
- チュートリアル形式の完全なテーマ開発プロセス
- 大きなセグメントのバージョン番号、フレームワーク互換性マトリックス、パフォーマンス数値のコミットメント

将来、トピック関連のコンテンツを追加したい場合は、一般的なチュートリアルに戻るのではなく、実際のリソース ディクショナリ、ウィンドウの動作、または UI アクセス ポイントの追加を優先する必要があります。

## 続きを読む

- [UIコンポーネントの概要](./README.md)
- [ColorVision.UI](./ColorVision.UI.md)