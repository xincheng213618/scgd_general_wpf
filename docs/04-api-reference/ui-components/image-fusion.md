---
knowledge_id: "ui.image-fusion"
knowledge_type: "topic"
status: "current"
summary: "景深融合窗口与本地流程节点共用文件执行器；输入顺序、CPU/CUDA门禁、取消、结果保存及下游图像交接。"
aliases: ["景深融合", "执行融合", "融合结果", "GPU 异步 (CUDA Async)", "焦点堆栈", "focus stacking", "FusionWindow", "FusionMode", "FusionFolderMenuContribution", "ImageCompute", "M_Fusion", "CM_Fusion", "CM_Fusion_Async", "融合耗时", "FileFusion", "LocalFileFusionNode", "流程景深融合", "有序文件列表"]
code_paths: ["UI/ColorVision.Core/FileFusion.cs", "UI/ColorVision.Core/FileFusionOptions.cs", "Native/include/fusion_options.h", "Native/include/fusion_request.h", "Engine/ColorVision.Engine/FlowProcessing/Nodes/LocalFileFusionNode.cs", "Engine/ColorVision.Engine/Services/Images/FileFusion/FileFusionServices.cs", "UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml", "UI/ColorVision.ImageTools/Fusion/FusionWindow.xaml.cs", "UI/ColorVision.ImageTools/Fusion/FusionFolderMenuContribution.cs", "UI/ColorVision.ImageTools/ImageResourceFileTypes.cs", "UI/ColorVision.Core/ImageCompute.cs", "UI/ColorVision.Core/OpenCVMediaHelper.cs", "UI/ColorVision.Core/OpenCVCuda.cs", "Native/include/opencv_media_export.h", "Native/include/cuda_export.h", "Native/opencv_helper/opencv_media_export.cpp", "Native/opencv_helper/fusion.cpp", "Native/opencv_cuda/cuda_export.cpp", "Native/opencv_cuda/Fusion.h", "Native/opencv_cuda/cudamath.h"]
test_paths: ["Test/ColorVision.UI.Tests/FileFusionTests.cs", "Test/ColorVision.UI.Tests/LocalFileFusionNodeTests.cs", "Test/opencv_helper_test/test_find_luminous_area.cpp", "Test/opencv_helper_test/test_cuda_fusion.cpp"]
related: ["ui.image-tools", "ui.core", "ui.image-editor", "ui.documents", "engine.native-integration"]
---

# 景深融合：输入、执行与结果生命周期

<!-- algorithm-help:start -->

## 景深融合算法原理 {#algorithm-principles}

景深融合将一组不同焦点的本地图片融合为一张图像。独立窗口和本地流程节点共同调用 `ColorVision.Core.FileFusion`，读取磁盘文件，不使用编辑器未保存的修改或流程连续采集帧。算法不依赖远端服务；流程图像结果登记仍依赖已有流程批次和 MySQL。

不同焦点的图片在不同位置包含清晰细节。当前算法先为每张图计算局部清晰度，再在同一像素位置对各帧强度作加权融合。输入应保持同一视场和位置，并按焦点顺序、近似等间隔采集；**尺寸相同不代表已配准**，当前不会自动对齐、去重影或控制电机采集。

<figure class="algorithm-figure">
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 660 310" role="img" aria-labelledby="fusion-principle-title fusion-principle-desc">
  <title id="fusion-principle-title">焦点堆栈按局部清晰度融合</title>
  <desc id="fusion-principle-desc">三张同视场图像中，近处、中间和远处区域分别最清晰。每个位置建立跨帧清晰度权重，融合图综合三处细节。示意不表示执行了自动配准或取得真实深度。</desc>
  <defs><marker id="fusion-principle-arrow" viewBox="0 0 10 10" refX="8" refY="5" markerWidth="6" markerHeight="6" orient="auto"><path d="M0 0 L10 5 L0 10 Z" fill="#64748b" /></marker></defs>
  <g font-family="Segoe UI, Microsoft YaHei, sans-serif" font-size="16" fill="#1f2937">
    <text x="172" y="24" text-anchor="middle">按焦点顺序输入同视场图片</text>
    <rect x="20" y="48" width="225" height="56" fill="#f8fafc" stroke="#94a3b8" />
    <rect x="20" y="126" width="225" height="56" fill="#f8fafc" stroke="#94a3b8" />
    <rect x="20" y="204" width="225" height="56" fill="#f8fafc" stroke="#94a3b8" />
    <path d="M44 62 H80 M44 73 H80 M44 84 H80" stroke="#2563eb" stroke-width="3" />
    <path d="M108 145 H144 M108 156 H144 M108 167 H144" stroke="#2563eb" stroke-width="3" />
    <path d="M172 222 H208 M172 233 H208 M172 244 H208" stroke="#2563eb" stroke-width="3" />
    <text x="265" y="82">帧 1</text><text x="265" y="160">帧 2</text><text x="265" y="238">帧 3</text>
    <path d="M320 154 H419" stroke="#64748b" stroke-width="2" marker-end="url(#fusion-principle-arrow)" />
    <text x="371" y="117" text-anchor="middle">逐位置选焦</text><text x="371" y="197" text-anchor="middle">归一化权重</text>
    <rect x="442" y="114" width="196" height="82" fill="#eff6ff" stroke="#2563eb" />
    <path d="M458 136 H494 M458 147 H494 M458 158 H494 M520 136 H556 M520 147 H556 M520 158 H556 M582 136 H618 M582 147 H618 M582 158 H618" stroke="#2563eb" stroke-width="3" />
    <text x="540" y="93" text-anchor="middle">融合图像</text>
    <text x="330" y="294" text-anchor="middle">每个像素的权重可以不同  输出仍是一张普通 8-bit 图像</text>
  </g>
</svg>
<figcaption>示意强调同一位置跨帧比较清晰度；没有从权重直接输出深度图，也不生成 CIE 标定数据。</figcaption>
</figure>

### 局部清晰度与权重 {#fusion-focus-measure}

令第 $k$ 张输入的归一化灰度图为 $G_k=\operatorname{Gray}(I_k)/255$，$B_w$ 表示边界复制的 $w\times w$ 局部均值，则：

$$
F_k=B_w\left[(G_k-B_w(G_k))^2\right]
$$

这里先减去局部均值，再对平方偏差作局部平均；纹理起伏较大的位置通常得到较高的清晰度值。`FocusWindow` 同时控制这两次局部均值。高噪声也会抬高清晰度，因此清晰度不是独立的物理焦距或计量分辨率。

当图组张数 $P<2\times\mathrm{FocusStep}+1$ 时，使用普通清晰度加权。每个颜色通道独立套用同一组灰度权重：

$$
I_{\mathrm{out}}(x,y)=\frac{\sum_{k=1}^{P} w_k(x,y)I_k(x,y)}{\sum_{k=1}^{P}w_k(x,y)},\qquad w_k=F_k
$$

例如两帧某像素强度为 80、120，清晰度权重为 1、3，则输出为 $(80+3\times120)/4=110$。若所有清晰度之和不大于 $10^{-6}$，该位置改用输入平均强度，避免无纹理区域变成黑斑。

图数足够时，先在清晰度峰附近按 `FocusStep` 取样，建立当前实现的对称高斯峰拟合。将其与所有帧的实际清晰度比较，得到归一化平均绝对误差；按 `ConsistencyWindow` 平滑后，记为 $E$，再计算：

$$
S=-20\log_{10}(\max(E,10^{-6})),\qquad
\phi=\frac{1+\tanh(\alpha(S-T))}{2\alpha}
$$

$T$ 是 `Threshold`，单位 dB。$S$ 衡量清晰度曲线与该拟合模型的符合程度，**不是融合图与真值图比较所得的 PSNR**。$\phi$ 经过固定 3×3 中值滤波后，用归一化清晰度 $\widetilde F_k=F_k/\max_j F_j$ 形成权重：

$$
w_k=\frac12+\frac12\tanh\left[\phi(\widetilde F_k-1)\right]
$$

再代入加权融合公式。无法形成有效高斯峰时赋予低可信度并偏向多帧混合。调低阈值通常使选焦更集中，调高则增加混合；该行为还受纹理、噪声和 $\alpha$ 影响，不是线性锐化控制。

## 执行融合

1. 在 Solution 文件树中右键单个现有文件夹，选择 **景深融合**。
2. 使用 **添加文件** 或拖入文件补充列表，通过上移、下移、移除和清空调整焦点顺序。
3. 选择计算模式；需要调节时打开 **融合参数…**，通过通用属性编辑器修改参数，确认后生效，取消不更改。点击 **执行融合**。运行期间文件列表、计算模式、参数入口和执行按钮禁用。
4. 成功后显示“融合结果”标签；没有停靠面板时打开独立窗口。窗口不会自动保存文件，需要保存时使用图像编辑器的输出操作。

流程中添加 **自定义节点 → 景深融合**（`LocalFileFusionNode`）。它有一个 `IN` 和一个 `OUT`，输入连接负责触发执行，图片来源取自节点属性：

| 属性 | 含义 |
| --- | --- |
| 输入方式 | `Folder` 读取文件夹，`FileList` 使用有序文件列表 |
| 输入文件夹 | 只读取直接子文件，按文件名自然排序，不递归 |
| 有序文件列表 | 通过通用集合编辑器添加文件并调整顺序；STN 使用 JSON 保存，文件名中的逗号不会拆分路径 |
| 计算模式 | Auto、CPU、GPU、GPUAsync |
| 结果目录 | 留空使用当前用户 `LocalAppData/ColorVision/Results/Fusion`；不得等于任一输入文件的父目录 |

例如：开始 → 景深融合 → 十字定位 → 结束。每次执行保存一张带唯一名称的 PNG，以现有图像结果类型 `100` 登记，并通过 `SetCurrentFrame` 交给后续本地节点。`MasterId` 指向图像记录，`MasterValue` 和记录 `FileUrl` 指向保存后的图片。远端节点仍按自己的文件访问及结果引用契约工作，不因本地保存而自动获得远端文件可见性。

窗口和流程节点使用同一组参数，默认值保持清晰度窗口 5、可信度平滑窗口 3、拟合间隔 2、α=0.2、阈值 13。窗口参数编辑器按“常用/高级”分类，提供作用说明与恢复默认值；窗口摘要显示本次设置，耗时列表保存每次执行的参数，便于调参比较。流程节点将参数随 STN 保存，并将实际执行参数写入结果记录的 `Params.FusionOptions`。

| 参数 | 默认与允许范围 | 调节含义 |
| --- | --- | --- |
| 清晰度窗口 `FocusWindow`（常用） | 5；3–31 的奇数 | 控制两次局部均值的邻域。小窗口保留细小纹理，大窗口使选焦更平稳，但可能跨越深度边缘；可先比较 3/5/7/9 |
| 选焦可信度阈值 `Threshold`（常用） | 13 dB；0–60 | 调低偏向最清晰帧，调高增加多帧混合以抑制噪声，可能变软；可先比较 10/13/16。这不是曝光阈值 |
| 拟合取样间隔 `FocusStep`（高级） | 2 帧；1–1024 | 在清晰度峰附近选取相隔指定帧数的样本。不是电机步距，也不跳过输入图片；要求输入按焦点顺序、近似等间隔采集 |
| 可信度平滑窗口 `ConsistencyWindow`（高级） | 3；3–31 的奇数 | 平滑拟合误差；调大减少零散的选焦变化，但扩大区域过渡。与清晰度窗口独立 |
| 选择性系数 `Alpha`（高级） | 0.2；0.01–1 | 通常调小偏向最清晰帧，调大增加混合；同时影响阈值过渡与选择强度，不能理解为线性锐化强度 |

中值滤波固定为 3×3，不开放额外开关。图片数少于 `2 × FocusStep + 1` 时，两种实现均使用普通清晰度加权，此时阈值、α 和可信度平滑不参与；窗口会明确提示。无清晰度证据的平坦区域保留输入平均强度，避免输出黑斑。无效范围、NaN、无穷值在进入算法前被拒绝。异步调用在排队前复制文件列表和参数，后续编辑不影响在途任务。

算法保留原实现的对称端点拟合约定，通过中心化表达式避免负方差与指数溢出；不能形成有效高斯峰时赋予低可信度，转向多帧混合。CPU/CUDA 都采用单精度清晰度图、复制边界和最近整数输出，浮点累加顺序仍可能产生少量像素差异，不承诺逐字节相等。CUDA 每张原始 8-bit 图仅上传一次，采用可分离均值滤波并合并权重与最终融合，所有缓冲随调用释放。没有共享的全局 GPU 工作区。

这些默认值属于当前产品实现，不是 MATLAB 原版的逐项复刻；MATLAB 示例的拟合间隔为 5，且一个窗口参数同时控制清晰度与可信度滤波。调参建议是试验起点，不表示对所有场景最优；没有配准、去重影或真实焦距坐标输入。

## 文件列表与输入约束

文件夹入口统一使用 `FileFusion.GetFolderFiles`，过滤 `.bmp/.jpeg/.jpg/.png/.tif/.tiff`。显式文件列表保留顺序；窗口添加文件保留对话框顺序，拖入文件仍按字符串排序，可在执行前手动调整。每次执行捕获独立路径快照，不随运行期间参数编辑改变。

公共执行器要求至少两张不同路径的图片，拒绝重复完整路径、缺失文件、不可识别文件、尺寸或通道不一致。预检使用 WPF 解码器，仅接受 `Gray8`、`Bgr24`、`Rgb24`，对应 8-bit 单通道或三通道图像；Indexed、Alpha、高位深和浮点布局明确拒绝，不隐式降位深。文件后缀不能替代像素布局校验，也不包含 `.cvraw`、`.cvcie`。

预检后的只读文件句柄保持到 native 调用结束，禁止这段时间覆盖或替换源文件。native 通过 Unicode 文件路径读取字节，再以 `IMREAD_UNCHANGED` 解码，支持中文目录；不自动对齐、缩放、校色或跳过坏图。预检通过不代表 native 必定解码成功。

## 模式、返回值与资源所有权

| 模式 | 选择与调用 |
| --- | --- |
| Auto | 2–4 张固定选 CPU；至少五张且 `ImageCompute.UseCuda` 为真才选 GPU；否则选 CPU |
| CPU | `OpenCVMediaHelper.M_Fusion` → `opencv_helper.dll` |
| GPU | 至少五张且 CUDA 开关可用，调用 `OpenCVCuda.CM_Fusion` |
| GPUAsync | 同样的门禁，调用 `OpenCVCuda.CM_Fusion_Async`；native 内部并行加载，导出仍同步返回最终图像 |

Auto 只在执行前选路，不在 GPU 失败后重试 CPU。CUDA 驱动检查不证明 DLL、依赖和输入能够成功执行。结果记录保存本次实际模式，不在结束时重新读取全局开关推测模式。

公共执行器保留小图组优先 CPU 的选择与强制 GPU 五张门禁，避免小任务的 GPU 初始化开销。native CUDA 自身也安全处理单图、短图组和不足拟合跨度的输入。低层入口不包含托管文件锁与像素格式预检；五张及以上也不等于融合质量已验收。

两个 native DLL 保留原有导出名、调用约定和 HImage 所有权。旧 JSON 文件数组继续使用默认参数；新请求为 `{ "files": [...], "options": { "focusWindow": 5, "consistencyWindow": 3, "focusStep": 2, "alpha": 0.2, "threshold": 13 } }`，options 中未知字段和非法数值被拒绝，窗口与流程使用此格式，因此托管与 native 必须配套更新。两个 native DLL 均以返回 `0` 表示成功。公共执行器在所有返回和异常路径通过 `finally` 释放输出 `HImage`，成功时复制成冻结的托管位图。`FileFusionResult` 包含位图、输入快照、实际模式和耗时，不拥有待调用方释放的 native 缓冲，不引用窗口、工作区或 Engine DAO。

## 取消、并发与计时

公共执行器在进程内串行执行窗口和流程的融合任务，避免多个图组同时占用 GPU 和大量 native 内存。排队支持取消；已进入同步 native 的调用不能强制中断，取消后等待返回并释放输出，不再交付成功结果。

窗口关闭或点击 **取消** 会取消该次调用；迟到结果不会打开结果窗口或弹出错误框。流程执行把取消资源挂到本次 `RuntimeResources`，流程结束/停止释放资源后取消排队任务或丢弃在途结果。节点在保存、登记和交接之间检查停止状态；已经完成的文件或数据库写入不会回滚，也不保证与并发停止操作构成跨文件/数据库事务。

| 字段 | 实际覆盖 |
| --- | --- |
| `ValidationMs`，窗口“校验(ms)” | 打开并锁定输入、读取格式及尺寸预检 |
| `NativeMs`，窗口“读图及融合(ms)” | 整个 native 调用，包含 native 文件读取、融合和输出缓冲生成 |
| `ConvertMs` | native 输出复制为冻结托管位图 |
| `TotalMs` | 公共同步执行入口开始至结果完成，包含执行门禁排队；不含 UI 显示、流程保存和数据库登记 |

流程额外通过 `FlowNodeTiming` 记录 `ResolveBatch`、`Fusion`、`SaveImage`、`OpenImage`、`PersistResult`、`PublishResult`，不能将公共执行器耗时冒充流程整体耗时。

## 结果显示与保存边界

窗口结果仍使用临时 `LayoutDocument` 或独立窗口，不通过 `EditorDocumentService` 注册路径文档。关闭完成（`Closed`）后释放 ImageView；取消关闭不会提前清空图像。另存时需分清源像素和带叠加显示图像，详见本主题末尾的图像编辑器参考。

流程结果由 Engine 保存和登记，计算模块不接触 MySQL。输出 PNG 使用唯一名称和 `CreateNew`，不覆盖已有文件；默认目录与源文件分离，配置输出为输入子目录时因不递归枚举也不会卷入下一次图组。输入顺序、请求/实际模式和计算耗时保存到图像记录 `Params` 中。

只有图像记录成功登记、结果交接和发布完成后才作为节点成功结果继续执行。计算/写文件/数据库/发布任一失败均按节点失败处理；已经产生的 PNG 或记录保留用于诊断，不作为成功执行的证据。融合图是普通像素图，不生成 CIE 标定数据或承诺亮度、色度测量有效。

<!-- algorithm-help:end -->

## 验证入口与缺口

- `FileFusionTests`：文件夹自然顺序、显式顺序、输入锁、格式/尺寸/重复拒绝、Auto 小图组选路、强制 GPU 拒绝、取消后丢弃、native 失败不重试，以及两张合成灰度图的真实 CPU 调用。
- `LocalFileFusionNodeTests`：节点端口、包含逗号和中文路径的 STN 往返、文件夹/列表输入、真实 PNG 保存和帧交接、登记失败及停止后丢弃。数据库与消息发布使用替身，不访问真实数据库。
- `Test/opencv_helper_test/test_find_luminous_area.cpp` 的 `smokeFusionReturnsOwnedHImage` 检查 CPU 单图输出和释放；`test_cuda_fusion.cpp` 提供 CUDA DLL 验证和比较入口，覆盖旧数组/新参数请求、同步/异步一致性、非法参数和短图组/平坦图像。CUDA DLL 内的 `M_Fusion` 是 GPU 别名，该入口比较不等于 CPU/GPU 数值一致性。

测试入口不是本次已通过声明；窗口人工操作、真实数据库历史显示、CUDA 驱动/native 依赖、大图资源和真实焦点堆栈融合质量需分别验收。Native/CUDA 测试会创建隔离图像并加载真实 DLL，不能用窗口响应或托管替身测试代替它们。

图像另存与 source/rendered 的差别见[图像编辑器](./ColorVision.ImageEditor.md)。
