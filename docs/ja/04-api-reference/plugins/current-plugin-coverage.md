# 現在のプラグイン文書カバレッジ

このページは、現在の `Plugins/` にある実在プラグインが、能力ページ、引き継ぎページ、検収チェック、runtime README/CHANGELOG を持っているか確認するためのものです。

## カバレッジ一覧

| plugin directory | project | manifest | capability page | handoff / acceptance |
| --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | `Conoscope.csproj` | `Conoscope` / `1.4.6.1` | [Conoscope](./standard-plugins/conoscope.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/EventVWR/` | `EventVWR.csproj` | `EventVWR` / `1.0` | [EventVWR](./standard-plugins/eventvwr.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/Spectrum/` | `Spectrum.csproj` | `Spectrum` / `1.0` | [Spectrum](./standard-plugins/spectrum.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/SystemMonitor/` | `SystemMonitor.csproj` | `SystemMonitor` / `1.0.1` | [SystemMonitor](./standard-plugins/system-monitor.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |
| `Plugins/WindowsServicePlugin/` | `WindowsServicePlugin.csproj` | `WindowsServicePlugin` / `1.0` | [WindowsServicePlugin](./standard-plugins/windows-service.md) | [Matrix](./plugin-capability-matrix.md), [Playbook](./plugin-handoff-playbook.md), [Acceptance](./plugin-field-acceptance.md) |

## 現在の作業ツリー監査

2026-06-10 時点の作業ツリーでは、5 個の plugin directory すべてに `.csproj`、`manifest.json`、runtime `README.md`、runtime `CHANGELOG.md`、docs plugin page があります。

| plugin directory | `.csproj` | `manifest.json` | runtime README | runtime CHANGELOG | result |
| --- | --- | --- | --- | --- | --- |
| `Plugins/Conoscope/` | present | `Conoscope` / `1.4.6.1` | present | present | complete |
| `Plugins/EventVWR/` | present | `EventVWR` / `1.0` | present | present | complete |
| `Plugins/Spectrum/` | present | `Spectrum` / `1.0` | present | present | complete |
| `Plugins/SystemMonitor/` | present | `SystemMonitor` / `1.0.1` | present | present | complete |
| `Plugins/WindowsServicePlugin/` | present | `WindowsServicePlugin` / `1.0` | present | present | complete |

runtime README/CHANGELOG は package と現場ディレクトリで読まれます。docs site page は能力、境界、リスク、検収方法を引き継ぎ担当者向けに説明します。両方を同期して更新します。

## 現在の一覧に含まれない名前

Pattern、ImageProjector、ScreenRecorder は現在のプラグインではありません。復帰前に `Plugins/<Name>/`、`.csproj`、`manifest.json`、README、CHANGELOG、ビルドコピー、パッケージ検証、文書ナビゲーションを復元する必要があります。

## カバレッジ確認

```powershell
Get-ChildItem Plugins -Directory | Sort-Object Name | Select-Object -ExpandProperty Name
Get-ChildItem docs/ja/04-api-reference/plugins/standard-plugins -File | Sort-Object Name | Select-Object -ExpandProperty Name
```

結果は現在の 5 plugin だけを current capability として扱う必要があります。historical name は restore check の文脈に限定します。
