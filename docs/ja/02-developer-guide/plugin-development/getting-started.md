# プラグイン開発を始める

このページでは、現在のリポジトリで実行可能な最短のプラグイン開発パスを提供します。このリポジトリでは、古いユニバーサル ホスト、非同期ライフサイクル、`plugin.json` サンプルは使用されなくなりました。

## 最初に準備するもの

- Windows開発環境
- .NET 8.0 SDK
- WPF開発ツールチェーン
- 現在のウェアハウスのソースコードとメインプログラムを実行して出力できます

## 最小開発パス

### 1. 新しいプラグイン プロジェクトを作成します。

プラグイン プロジェクトを `Plugins/<PluginId>/` の直下にビルドし、ターゲット フレームワークを `net8.0-windows` のままにすることをお勧めします。プラグインにインターフェイスがある場合は、WPF を有効にします。


```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <OutputType>Library</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\UI\ColorVision.Common\ColorVision.Common.csproj" Private="false" />

如果需要显式指定入口类型，可以继续补 `entry_point`。

## 4. 把产物复制到主程序插件目录

主程序运行时会从自己的输出目录扫描 `Plugins/`，所以调试时需要把插件产物复制进去。

```
XML
<ターゲット名="PostBuild" AfterTargets="PostBuildEvent">
  <Exec Command="xcopy /Y /E /I $(TargetDir)* $(SolutionDir)ColorVision\bin\$(ConfigurationName)\net8.0-windows\Plugins\MyPlugin\" />
</ターゲット>
「」

ローカル出力ディレクトリが異なる場合は、実際のメイン プログラムの出力パスに応じて調整する必要があります。

## 5. 実行とデバッグ

1. メインプログラムをビルドします。
2. プラグイン プロジェクトをビルドし、DLL と `manifest.json` がプラグイン ディレクトリにコピーされていることを確認します。
3. `ColorVision/ColorVision.csproj` を起動します。
4. プラグインが対応するメニュー、ツール ページ、またはプラグイン管理インターフェイスにロードされているかどうかを確認します。

## 推奨されるリファレンス実装

- `Plugins/EventVWR/EventVWRPlugins.cs`
- `Plugins/EventVWR/Dump/MenuDump.cs`
- `Plugins/SystemMonitor/SystemMonitorControl.xaml.cs`
- `Plugins/README.md`

これらの例では、基本的なプラグイン エントリとメニュー拡張という 2 つの一般的なパターンを取り上げました。

## よくある質問

### プラグインが見つかりません

- `manifest.json` が存在するかどうかを確認する
- `dllpath` が指す DLL が実際に存在するかどうかを確認する
- プラグイン ディレクトリがメイン プログラム出力ディレクトリ下の `Plugins/<PluginId>/` にコピーされているかどうかを確認します。

### プラグインは見つかりましたが、関数が表示されませんでした

- 基本的なプラグイン クラスのみが実装されており、必要なプロバイダー インターフェイスが実装されていないことを確認します。
- エントリ型に引数のない public 構造があるかどうかを確認します。
- 型が非抽象、非ジェネリックのオープン型であるかどうかを確認します。

### 依存関係の競合

- プラットフォームに付属する `ColorVision.*.dll` を再パッケージ化しないでください。
- プラグインに `.deps.json` がある場合、依存バージョンがターゲット プラットフォームより高くないことを確認してください

## 次のステップ

- プラットフォームがプラグインをスキャンしてロードする方法を理解するには: [プラグインのライフサイクル](./lifecycle.md) を参照してください。
- 最初に全体の構造を理解したい場合: [プラグイン開発の概要](./overview.md)を参照してください。