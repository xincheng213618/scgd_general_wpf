# ImageProjector 狀態說明

本頁不再把 ImageProjector 寫成當前倉庫裡的標準外掛實現，因為在當前 `scgd_general_wpf` 工作區裡，已經找不到與之對應的原始碼工程。

## 當前工作區裡的實際狀態

按當前倉庫結構核對：

- `Plugins/` 目錄下沒有 `ImageProjector/` 原始碼目錄。
- 工作區裡沒有對應的外掛工程檔案。
- 當前外掛索引頁 [Plugins/README.md](../../../../Plugins/README.md) 也沒有把它列為現存外掛目錄。
- 當前文件側邊欄中保留它，只是為了讓歷史狀態說明頁仍然可達，而不是表示當前倉庫裡存在對應外掛實現。

因此，這一頁不能繼續保留舊版那種“多顯示器投影工具完整手冊”的寫法，否則會把歷史功能寫成當前原始碼事實。

## 這頁現在保留什麼資訊

當前只保留一個結論：

ImageProjector 相關文件在這個工作區裡屬於歷史殘留頁，而不是基於當前原始碼可核對的 API 參考頁。

如果後續重新引入這個外掛，新的文件應至少基於這些真實錨點重寫：

- 外掛目錄與工程檔案
- `manifest.json`
- 選單或 provider 接入點
- 主視窗或投影視窗實現
- 配置落點

在這些程式碼重新出現之前，不應繼續補充功能介紹、配置表或 API 清單。

## 為什麼不繼續維護舊稿

舊版頁面把 ImageProjector 描述成當前真實存在的外掛，並給出了完整功能列表、顯示模式說明和元件結構。但在當前原始碼樹裡，這些描述已經沒有可核對的實現承載。

文件如果繼續這麼寫，會把“過去可能存在過的功能”偽裝成“當前倉庫事實”。這正是本輪清理要避免的事情。

## 繼續閱讀

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)