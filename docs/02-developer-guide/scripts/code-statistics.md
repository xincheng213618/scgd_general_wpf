---
knowledge_id: "delivery.code-statistics"
knowledge_type: "guide"
status: "current"
summary: "统计当前工作区的项目、目录和文件规模，并分析 Git 提交历史与时段；内置离线网页模板，工作区含未提交修改，历史快照不含。"
aliases: ["代码行数", "代码统计", "代码历史", "历史仪表盘", "count_code_lines.py", "generate_code_history_dashboard.py", "tracked-only", "改写估算", "自然日均变更", "Pillow is required to generate --share-card", "Portable dashboard builder not found"]
code_paths: ["Scripts/count_code_lines.py", "Scripts/generate_code_history_dashboard.py", "Scripts/code_history_analysis.py", "Scripts/code_history_dashboard", ".gitignore"]
test_paths: ["Scripts/tests/test_code_history_analysis.py", "Scripts/tests/test_code_history_dashboard.mjs"]
related: ["delivery.scripts", "governance.maintenance"]
---

# 代码行数与 Git 历史统计

这两个脚本用于回顾仓库规模和文本变更。`count_code_lines.py` 读取当前工作区文件；`generate_code_history_dashboard.py` 同时读取指定 Git 提交的第一父链和当前磁盘工作区，将两者作为独立数据范围展示。它们不调用产品发布链，但保存报告、缓存或图表会写入本地文件。

## 选择统计范围

| 需要回答的问题 | 工具与数据来源 |
| --- | --- |
| 当前目录有多少代码、注释和空行？ | 行数统计器；默认含已跟踪文件与未被 Git 忽略的未跟踪文件，读取磁盘内容 |
| 只统计已跟踪文件？ | 行数统计器加 `--tracked-only`；仍读取工作区修改后的内容，不是 HEAD 快照 |
| 某个分支或提交的规模如何变化？ | 历史图表；默认 `--ref HEAD`，不含未提交或未跟踪内容 |

两者使用同一套文件类型和注释识别规则，但数据来源不同。比较结果时同时确认提交、工作区状态、排除规则和统计列。

## 统计当前目录

需要 Python；使用 Git 文件筛选时还需要可用的 Git。统计器本身仅依赖 Python 标准库。从仓库根目录运行：

```powershell
# 输出语言分类表，不写报告文件
py Scripts\count_code_lines.py
# 缩小到 UI 目录，输出 JSON；该文件会被创建或覆盖
py Scripts\count_code_lines.py UI --tracked-only --format json --output .codex-artifacts\code-lines.json
```

| 参数 | 默认值与作用 |
| --- | --- |
| 位置参数 `path` | 脚本所在仓库根目录；可指定子目录或其它目录 |
| `--tracked-only` | 默认关闭；Git 可用时只选已跟踪文件 |
| `--exclude-generated` | 默认关闭；排除已列出的 Designer、AssemblyInfo、`.g.cs`、压缩 JS/CSS 等后缀，不是完整生成文件识别 |
| `--format` | `table`，也支持 `json`、`csv` |
| `--output` | 默认标准输出；指定时创建父目录并写 UTF-8 文件，扫描会排除这一个输出路径 |

Git 正常时使用 `ls-files --cached`，默认另加 `--others --exclude-standard`。已跟踪文件不会仅因后来加入 `.gitignore` 而自动消失。Git 不可用或 Git 查询失败时，脚本回退目录遍历，**不再应用 Git 忽略规则或 `--tracked-only` 筛选**；不能把这种回退报告当作已跟踪文件清单。

两种扫描都排除 `bin`、`obj`、`node_modules`、`packages` 等固定目录，完整集合在 `SKIP_DIRECTORIES`。文件类型由扩展名及少量特殊文件名决定；Markdown、JSON、XAML、工程文件和文本也属于统计对象，`Code` 并不只代表可执行语句。

### 行数如何分类

- 非空且不只是注释的行计为 `Code`；同一行同时有代码和注释时只计代码。
- 纯注释计为 `Comments`，其余空行计为 `Blank`；三者之和为物理文本行数。
- 这是字符标记扫描，不是各语言的完整解析器。字符串转义、多行字符串、嵌套注释等复杂语法不能按编译器精度理解；Python 文档字符串也不会自动归入注释。
- UTF-8/BOM、UTF-16 BOM 和 cp1252 回退由解码函数处理。无法读取、未识别或被判为二进制的文件会跳过；报告的跳过数不包含所有目录/生成文件排除项。

## 生成历史图表

历史分析需要 Python、Git、所选 ref 的本地历史，以及生成 PNG 的 Pillow。PNG 优先使用 Windows 中文字体，缺少时回退默认字体，中文显示需另行检查。

HTML 默认使用仓库内的 `Scripts/code_history_dashboard/` 模板，由 Python 将数据、样式和脚本打包为单个文件，不需要 Node.js、外部网页构建器或已有 HTML。打开后可离线使用；页面不会联网拉取数据，重新运行脚本才刷新统计。

新版页面包含总览、项目规模、提交节奏和统计口径。总览区分当前工作区规模与 Git 周快照；项目规模支持顶层目录筛选、项目搜索、按行数/文件数排序，以及项目 → 目录 → 文件下钻。项目详情显示语言构成、代码/注释/空行比例及较大文件。提交节奏支持日期范围、日/周/月/半年/年粒度、活跃日历、小时分布、星期分布、星期与小时热力图、目录变更和可搜索的提交记录。CSV 导出遵循当前视图和筛选：总览导出周快照，项目列表导出筛选后的项目，项目详情导出当前目录文件，提交页导出筛选后的提交记录。

工作区使用与行数统计器相同的 Git 文件选择和文本分类，包含未提交修改和未被忽略的未跟踪文件；排除本次生成的输出路径。工程目录通过 `.csproj`、`.vcxproj`、`.fsproj`、`.vbproj`、`.shproj` 识别。每个文件归最近的祖先工程目录，嵌套工程单独归属，同目录多个工程定义合并；没有工程定义的文件按顶层目录归组，因此分组可加总且不重复。这里是物理目录规模，不执行 MSBuild，不展开链接文件，也不代表某个配置的实际编译输入。Git 枚举失败会报错，不静默扫描整个目录；扫描期间发生的并发修改可能使文件来自相邻写入时点。

显式使用 `--builder` 或 `--verify` 时仍走旧版 portable artifact 构建链，需要 PATH 中的 Node.js 以及仓库外的 `deliver_portable_artifact.mjs` 和同目录配套构建器。`--verify` 未指定 builder 时从 Codex 插件缓存查找，缺失会报错，不会被普通 HTML 生成冒充验证成功。这些参数用于旧版兼容，不用于新版页面的浏览器验收。

```powershell
# 生成数据、PNG 和内置离线 HTML（首次运行也不需要外部网页构建器）
py Scripts\generate_code_history_dashboard.py
# 仅跳过 HTML 打包，仍写缓存、artifact.json 和 PNG，仍需要 Pillow
py Scripts\generate_code_history_dashboard.py --no-build
```

默认输出都位于脚本所在仓库的 `.codex-artifacts/code-history-dashboard/`，该目录已被 Git 忽略：

| 文件 | 用途 |
| --- | --- |
| `index.html` | 用于离线阅读的历史仪表盘；重新运行才刷新数据 |
| `artifact.json` | 历史图表的数据与展示声明，以及 analysis 中的工作区和提交明细 |
| `share-card.png` | 1080 × 1440 的摘要图片 |
| `history-cache.json` | 复用提交、文件和周快照统计的本地缓存 |

输出文件可被覆盖；缓存/数据/图片先于 HTML 生成，后续打包失败不会撤销已经写入的文件。`artifact.json` 存在不代表 HTML 或浏览器检查成功。

| 参数 | 作用 |
| --- | --- |
| `--repo`、`--ref` | 选择仓库和提交，默认脚本仓库与 `HEAD`；不自动拉取远端历史 |
| `--output`、`--artifact`、`--share-card`、`--cache` | 分别指定四种输出路径；仅设置 `--repo` 不会把默认输出迁到目标仓库，设置 `--output` 也不改变其余三项 |
| `--exclude-generated` | 与行数统计器共用生成文件后缀规则 |
| `--refresh-cache` | 忽略可复用缓存并重新统计，无需手工删除目录 |
| `--builder` | 可选，改用旧版外部 `deliver_portable_artifact.mjs` 路径；普通新版生成不需要 |
| `--no-build` | 跳过 HTML 打包及 `--verify`/`--open` 分支，不跳过数据和图片生成 |
| `--verify` | 调用旧版外部构建器的浏览器验证；不表示新版内置页面已通过浏览器验收 |
| `--open` | HTML 生成后在 Windows 打开它；默认不打开 |

## 历史图表的统计口径

历史来自 `git log --first-parent --reverse --diff-merges=first-parent --no-renames --numstat`。合并提交按第一父提交的差异计一次；不逐一展开另一条分支的内部提交。重命名按删除加新增处理，二进制 numstat 和不在识别范围的路径不计文本变更。浅克隆或缺失历史只能支持实际可读的范围。

周期和小时/星期分布均按提交者时间 `%cI` 的自带日期、时刻与 UTC 偏移统计，没有统一转换为读者时区；它不等同于作者最初编写时间，也不能反推工作时长。日期快捷范围以所选历史最后日期为终点；活跃日历只显示筛选范围末尾最多 365 天，其余区间统计保留完整筛选。周从星期一开始，另支持日、月、半年和年；空周期保留，日均分母使用与历史范围相交的自然天数。切换粒度改变周期表和相应图表；周快照、语言规模、目录汇总等数据仍保持各自口径，不是所有组件都重算成所选粒度。

| 指标 | 解释 |
| --- | --- |
| 新增、删除、总变更、净增长 | Git 文本 diff 行数；总变更＝新增＋删除，净增长＝新增－删除，包含被识别文件中的注释和空行 |
| 周末代码/内容行 | 读取每周最后一个可用提交的 Git blob，按上述字符规则分类；没有新提交的周沿用上一提交 |
| 最近 1 周变更 | 新版取历史最后日期所在周之前的完整周一至周日，与再前一周比较；历史未覆盖完整周时不比较，零基数不计算百分比。新增、删除、净增长分别列出 |
| 改写/重构估算 | 每周期 `2 × min(新增, 删除)`；没有配对具体文件或语句，不能据此认定发生了语义重构 |
| 8 周趋势和节奏对比 | 取完整周的变更量；近期窗口最多 8 周，早期基准最多 26 周，起始残周和末尾未完整周不进入基准 |
| 持续节奏变化 | 前后各 8 个完整周的分位值相差至少 1.6 倍且较小值大于零，再筛选间隔至少 12 周的候选，最多 5 个 |
| 规模跃变与版本背景 | 比较相邻周的代码/内容规模并筛选较大变化；附近 21 天内的 CHANGELOG 条目只是背景线索，不解释因果 |

界面称为“中位数”的值由 `percentile` 选取排序后 `round((n - 1) × 0.5)` 位置的一个样本；偶数样本时没有对中间两项求平均。周快照是按规则重新分类的结果，普通周期的 `total_lines` 则由选定提交的总行数和 Git 净变化反推，不能把每个中间点都称作重新读取后的精确代码规模。

这些指标反映文本规模和变动，不直接衡量功能质量、个人工作量或 AI 带来的效率。图表没有预设 AI 工具时代；语义归因需结合实际修改另行分析。

## 缓存、失败与验证

缓存核对仓库路径、缓存版本、计数器规则和生成文件选项。同一提交可以复用；旧缓存提交位于新 ref 第一父链时增量补算，否则重建。更改历史分析算法时仍需检查 `CACHE_RULES_VERSION` 与缓存指纹覆盖范围，不能假定脚本任何修改都会使旧缓存失效。

| 现象 | 检查顺序 |
| --- | --- |
| 当前行数与历史图表不同 | 工作区修改、未跟踪文件、所选 ref、排除选项、代码/物理行列，以及 Git 是否回退目录扫描 |
| 找不到 builder 或 base builder | 默认内置页面不需要 builder；仅显式 `--builder` 或 `--verify` 时确认外部路径、配套文件及其运行依赖 |
| `Pillow is required to generate --share-card` | 为执行脚本的 Python 环境提供 Pillow；`--no-build` 不跳过 PNG |
| 切换仓库后输出仍在原位置 | 四个默认输出路径不随 `--repo` 改变，按需分别指定 |
| `--verify` 后仍有布局疑问 | 查看完整构建器输出并实际检查 HTML，不能只看脚本退出码 |

`package_html` 有一条验证降级路径：外部验证非零退出但输出包含 `"code":"horizontal_overflow"` 时，会改用普通构建器，后者成功即可继续。它没有证明该错误只来自滚动条，也没有排除同次验证中的其它失败；因此脚本成功不等于浏览器验证全部通过。

相关测试命令如下，不调用产品构建或发布链：

```powershell
py -m unittest discover -s Scripts/tests -p test_code_history_analysis.py -v
node --test Scripts/tests/test_code_history_dashboard.mjs
# 可选：用实际生成数据再次检查各视图与统计聚合
$env:CODE_HISTORY_ARTIFACT = (Resolve-Path .codex-artifacts/code-history-dashboard/artifact.json).Path
node --test Scripts/tests/test_code_history_dashboard.mjs
Remove-Item Env:CODE_HISTORY_ARTIFACT
```

Python 测试使用临时 Git 仓库检查未提交/未跟踪文件、工程归属、计数对齐、整周边界及安全嵌入。Node 测试默认使用独立合成数据，覆盖视图生成、筛选、下钻和聚合；设置上述环境变量后改用实际报告。Node 测试没有浏览器或布局引擎，不能代替真实浏览器的视觉、响应式、键盘和下载验收。只审阅说明时不必遍历整仓历史或生成图表。
