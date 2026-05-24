# ScreenRecorder 狀態說明

本頁不再把 ScreenRecorder 寫成當前倉庫裡的標準外掛實現，因為在當前 `scgd_general_wpf` 工作區裡，已經找不到與之對應的原始碼工程。

## 當前工作區裡的實際狀態

按當前倉庫結構核對：

- `Plugins/` 目錄下沒有 `ScreenRecorder/` 原始碼目錄。
- 工作區裡沒有對應的外掛工程檔案。
- 當前外掛索引頁 [Plugins/README.md](../../../../Plugins/README.md) 沒有把它列為現存外掛目錄。
- 當前文件側邊欄中保留它，只是為了讓歷史狀態說明頁仍然可達，而不是表示當前倉庫裡存在對應外掛實現。

因此，這一頁不能繼續保留舊版那種“高效能錄屏外掛 API 手冊”的寫法，否則會把歷史描述誤寫成當前實現。

## 這頁現在保留什麼資訊

當前只保留一個結論：

ScreenRecorder 相關文件在這個工作區裡屬於歷史殘留頁，而不是基於當前原始碼可核對的 API 參考頁。

如果後續重新引入這個外掛，新的文件應至少基於這些真實錨點重寫：

- 外掛目錄與工程檔案
- `manifest.json`
- 選單或 provider 接入點
- 錄製視窗與錄製源管理實現
- 配置和輸出落點

在這些程式碼重新出現之前，不應繼續補充編碼格式、錄製源型別或覆蓋層 API 之類的描述。

## 為什麼不繼續維護舊稿

舊版頁面把 ScreenRecorder 描述成當前真實存在的錄屏外掛，並給出了錄製源、編碼器、覆蓋層和高階功能列表。但在當前原始碼樹裡，這些內容已經沒有可核對的實現。

繼續潤色那份舊稿，只會讓文件看起來更完整，卻和當前倉庫越來越脫節。

## 繼續閱讀

- [Plugins/README.md](../../../../Plugins/README.md)
- [docs/02-developer-guide/plugin-development/overview.md](../../../02-developer-guide/plugin-development/overview.md)
- [docs/04-api-reference/plugins/standard-plugins/pattern.md](./pattern.md)