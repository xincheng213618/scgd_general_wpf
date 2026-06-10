# OpenCV と native 統合開発引き継ぎ

このページは現在の OpenCV/native 境界を説明します。Engine 側には `cvColorVision` の SDK/native wrapper 層があり、UI/Core 側には `opencv_helper.dll` / `opencv_cuda.dll` の P/Invoke wrapper があり、ファイル表示には `.cvraw` / `.cvcie` の解析とサムネイルがあります。

## 現在の階層

| 階層 | ディレクトリまたはファイル | 役割 |
| --- | --- | --- |
| デバイス SDK wrapper | `Engine/cvColorVision/` | カメラ、分光器、センサー、OLED アルゴリズム、MQTTMessageLib、native export |
| UI/Core native wrapper | `UI/ColorVision.Core/` | `HImage`、`OpenCVMediaHelper`、`OpenCVCuda`、`ImageCompute` |
| ファイル解析と表示 | `Engine/ColorVision.Engine/Media/` | `.cvraw`、`.cvcie`、サムネイル、CIE export、画像ツール |
| テスト | `Test/opencv_helper_test/` | C++ 検証。現在は `M_FindLuminousArea` が中心 |

## 変更箇所

| 目的 | 主な場所 |
| --- | --- |
| SDK export 追加 | `Engine/cvColorVision/` |
| WPF から呼ぶ画像処理追加 | `UI/ColorVision.Core/OpenCVMediaHelper.cs` または `OpenCVCuda.cs` |
| `.cvraw` / `.cvcie` 表示やサムネイル変更 | `Engine/ColorVision.Engine/Media/` |
| 亮点、疑似色、SFR、ホワイトバランス変更 | native `opencv_helper.dll` と C# signature |
| CUDA fusion 変更 | `opencv_cuda.dll`、`OpenCVCuda`、`ImageCompute` |

## P/Invoke ルール

- C# signature は calling convention、文字列エンコード、構造体 layout、解放方法まで native export と一致させます。
- `HImage` は native buffer を持つため、失敗時に出力を解放します。
- `IntPtr` 文字列を返す helper は `FreeResult()` が必要か確認します。
- x64 が主な配布対象です。native DLL、テスト、ホストの platform を合わせます。
- `cvColorVision` は純 managed アルゴリズムライブラリではなく、native binding とメッセージ型の層です。

## 検証コマンド

```powershell
dotnet build UI/ColorVision.Core/ColorVision.Core.csproj -c Release -p:Platform=x64
dotnet build Engine/cvColorVision/cvColorVision.csproj -c Release -p:Platform=x64
msbuild Test/opencv_helper_test/opencv_helper_test.vcxproj /p:Configuration=Debug /p:Platform=x64
Test/opencv_helper_test/build_test_find_luminous.bat
```

## 関連ドキュメント

- [cvColorVision](../../04-api-reference/engine-components/cvColorVision.md)
- [ColorVision.Core](../../04-api-reference/ui-components/ColorVision.Core.md)
- [ColorVision.ShellExtension](../../04-api-reference/engine-components/ColorVision.ShellExtension.md)
- [テストと検証の引き継ぎ](../testing.md)
