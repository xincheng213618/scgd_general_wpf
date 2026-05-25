---
layout: home

hero:
  name: "ColorVision"
  text: "光電技術と色管理のための統合プラットフォーム"
  tagline: Windows WPF テクノロジに基づくプロフェッショナル アプリケーション。高度なカラー マネージメントと光電テクノロジ ソリューションの提供に重点を置いています。マルチデバイスの統合、プロセスの自動化、プラグインの拡張をサポートし、オプトエレクトロニクス技術の研究開発と産業オートメーションのニーズに対応します。
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: インストールの開始
      link: /ja/00-getting-started/README
    - theme: alt
      text: 日常使用
      link: /ja/01-user-guide/README
    - theme: alt
      text: デザインとアーキテクチャ
      link: /ja/03-architecture/README

features:
  - icon: 🚀
    title: インストールと最初の使用
    details: 最初にシステム要件を確認し、次にインストールを完了し、最初の起動と最小限の閉ループ エクスペリエンスを実行します。
    link: /ja/00-getting-started/README

  - icon: 📖
    title: 日常使い
    details: インターフェイス、デバイス、ワークフロー、トラブルシューティング別に整理されたユーザー ドキュメント ポータル
    link: /ja/01-user-guide/README

  - icon: 🧩
    title: 開発と配信
    details: プラグイン、エンジン、デプロイメント、アップデート、ビルド スクリプトの開発者ポータル
    link: /ja/02-developer-guide/README

  - icon: 🏗️
    title: デザインとアーキテクチャ
    details: ランタイム、コンポーネントの相互作用、およびテンプレート システム設計に関するシステム レベルの視点
    link: /ja/03-architecture/README

  - icon: 📚
    title: API とソースコードの紹介
    details: インターフェースの確認、モジュールの入り口の表示、テンプレートとプラグインの実装場所の検索
    link: /ja/04-api-reference/README

  - icon: 🗂️
    title: 構造と付録
    details: プロジェクト構造の概要とモジュールドキュメントの比較表を使用して、ウェアハウスのコンテンツをすばやく見つけます。
    link: /ja/05-resources/README
---

## 📚 書類の選び方

|今すぐ何かしたいなら |最初にどこを見るべきか |説明 |
|------|------|----------|
|プログラムをインストールするか、マシンが実行できるかどうかを確認します。 [はじめに](/ja/00-getting-started/README) |システム要件、インストール、最初の実行、クイック スタートについて説明します。
|すでにプログラムをインストールしていて、インターフェイスと操作を学びたい | [ユーザーガイド](/ja/01-user-guide/README) |日常使用のために、内部実装は拡張されません。
|コードを変更するには、プラグインを作成し、パッケージ化してリリースします。 [開発ガイド](/ja/02-developer-guide/README) |拡張ポイント、ビルド、デプロイメント、配信プロセスを中心とした |
|システムがこのように設計されている理由を理解したい | [アーキテクチャ設計](/ja/03-architecture/README) |実行時の関係、コンポーネントの境界、設計上のアイデアに注意を払う |
|モジュール、インターフェイス、実装エントリを直接確認したい | [API リファレンス](/ja/04-api-reference/README) |ユーザーマニュアルではなくソースコードナビゲーション用 |
|ウェアハウス ディレクトリ、付録、ドキュメント マッピングを確認したい | [付録とリソース](/ja/05-resources/README) |情報をすばやく見つけるために使用されます。メインのチュートリアルはホストされません。

## 現在の組織原則

- インストールと初回使用は `00-getting-started/` に集中します
- 日々の業務は `01-user-guide/` に集中しています
- 二次開発、展開、配信は `02-developer-guide/` に集中します
- 設計の境界と実行時の理解は `03-architecture/` に集中しています
- `04-api-reference/` を中心としたモジュール、インターフェイス、実装の紹介
- プロジェクト構造、付録、安定したインデックス、`05-resources/` に集中化

## テクノロジースタック

![.NET バージョン](https://img.shields.io/badge/.NET-10.0-blue.svg)
![プラットフォーム](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)
![ライセンス](https://img.shields.io/github/license/xincheng213618/scgd_general_wpf.svg)
![スター](https://img.shields.io/github/stars/xincheng213618/scgd_general_wpf.svg)