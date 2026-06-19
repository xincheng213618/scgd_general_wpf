# テンプレートメニュー入口

`Templates/Menus/` はアルゴリズム実行モジュールではなく、テンプレート機能をメインメニューへ配置する骨格です。どのグループに表示し、クリック時にどの `TemplateEditorWindow` を開くかを決めます。

## クイック情報

| 項目 | クラス |
| --- | --- |
| トップメニュー | `MenuTemplate` |
| アルゴリズムテンプレートグループ | `MenuITemplateAlgorithm` |
| 汎用テンプレートメニュー基底 | `MenuItemTemplateBase` |
| アルゴリズムテンプレートメニュー基底 | `MenuITemplateAlgorithmBase` |
| 既定動作 | `new TemplateEditorWindow(Template).Show()` |

## 階層

```text
MenuTemplate
  -> MenuITemplateAlgorithm
       -> MenuITemplateAlgorithmBase 派生メニュー
```

`MenuItemTemplateBase` は既定で `MenuTemplate` の下にぶら下がり、`Execute()` から `ShowTemplateWindow()` を呼びます。具体メニューは `Template` プロパティで開くテンプレートを返します。

## 引き継ぎ注意

- `Menus/` は入口の整理だけを行い、アルゴリズムは実行しません。
- `OwnerGuid` を変えるとメニュー位置が変わり、テンプレート入口が見えなくなることがあります。
- 既定ウィンドウは非モーダル `Show()` です。特殊な流れが必要な場合のみ `ShowTemplateWindow()` を上書きします。
- 保存、インポート、エクスポートは具体 `ITemplate` 実装の責任です。
- 検索時は `MenuDefalutDicAlg` など現在のクラス名を使います。

## 関連ページ

- [テンプレート管理](./template-management.md)
- [Templates API リファレンス](./api-reference.md)
- [プラグイン開発ガイド](../../../02-developer-guide/plugin-development/README.md)
- [拡張ポイント](../../extensions/README.md)
