---
layout: home

hero:
  name: "ColorVision"
  text: "光電技術與色彩管理一體化平台"
  tagline: 基於 Windows WPF 技術的專業應用程式，專注於提供先進的色彩管理和光電技術解決方案。支援多裝置整合、流程自動化、外掛擴充套件，滿足光電技術研發與工業自動化需求。
  image:
    src: /images/ColorVision.png
    alt: ColorVision
  actions:
    - theme: brand
      text: 開始安裝
      link: /zh-tw/00-getting-started/README
    - theme: alt
      text: 日常使用
      link: /zh-tw/01-user-guide/README
    - theme: alt
      text: 設計與架構
      link: /zh-tw/03-architecture/README

features:
  - icon: 🚀
    title: 安裝與首次使用
    details: 先確認系統要求，再完成安裝、首次啟動和最小閉環體驗
    link: /zh-tw/00-getting-started/README
  
  - icon: 📖
    title: 日常使用
    details: 按介面、裝置、工作流程和故障排查組織的使用者文件入口
    link: /zh-tw/01-user-guide/README
  
  - icon: 🧩
    title: 開發與交付
    details: 面向外掛、Engine、部署、更新和建置指令碼的開發者入口
    link: /zh-tw/02-developer-guide/README
  
  - icon: 🏗️
    title: 設計與架構
    details: 從系統級視角理解執行時、元件互動和模板系統設計
    link: /zh-tw/03-architecture/README
  
  - icon: 📚
    title: API 與原始碼導讀
    details: 查介面、看模組入口、定位模板和外掛實現位置
    link: /zh-tw/04-api-reference/README
  
  - icon: 🗂️
    title: 結構與附錄
    details: 用專案結構總覽和模組文件對照表快速定位倉庫內容
    link: /zh-tw/05-resources/README
---

## 📚 如何選文件

| 如果你現在想做什麼 | 應該先看哪裡 | 說明 |
|------|------|----------|
| 安裝程式或確認機器能不能跑 | [入門指南](/zh-tw/00-getting-started/README) | 覆蓋系統要求、安裝、首次執行和快速上手 |
| 已經裝好程式，想學介面和操作 | [使用者指南](/zh-tw/01-user-guide/README) | 面向日常使用，不展開內部實現 |
| 要改程式碼、做外掛、打包釋出 | [開發指南](/zh-tw/02-developer-guide/README) | 面向擴充套件點、建置、部署和交付流程 |
| 想理解系統為什麼這樣設計 | [架構設計](/zh-tw/03-architecture/README) | 關注執行時關係、元件邊界和設計思路 |
| 想直接查模組、介面和實現入口 | [API 參考](/zh-tw/04-api-reference/README) | 面向原始碼導航，不是使用者手冊 |
| 想看倉庫目錄、附錄和文件對映 | [附錄與資源](/zh-tw/05-resources/README) | 用於快速定位資料，不承載主教程 |

## 當前整理原則

- 安裝與首次使用，集中放在 `00-getting-started/`
- 日常操作，集中放在 `01-user-guide/`
- 二次開發、部署和交付，集中放在 `02-developer-guide/`
- 設計邊界與執行時理解，集中放在 `03-architecture/`
- 模組、介面和實現導讀，集中放在 `04-api-reference/`
- 專案結構、附錄和穩定索引，集中放在 `05-resources/`

## 技術棧

![.NET Version](https://img.shields.io/badge/.NET-10.0-blue.svg)
![Platform](https://img.shields.io/badge/Platform-Windows-lightgrey.svg)
![WPF](https://img.shields.io/badge/UI-WPF-blue.svg)
![License](https://img.shields.io/github/license/xincheng213618/scgd_general_wpf.svg)
![Stars](https://img.shields.io/github/stars/xincheng213618/scgd_general_wpf.svg)
