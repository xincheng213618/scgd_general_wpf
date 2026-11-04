namespace ColorVision.Copilot.Tests.Evaluation;

internal enum CopilotIntentTool
{
    LocalSearch,
    WorkspaceWrite,
    Execute,
    Shell,
    GitDiff,
    GitStatus,
    DatabaseRead,
    DatabaseWrite,
    FlowInspect,
    FlowWrite,
    FlowRunHistory,
    DeviceServices,
    RecentLog,
    WebSearch,
    FetchUrl,
    WindowsProcess,
    WindowsService,
    TcpPort,
}

[Flags]
internal enum CopilotIntentEvidence
{
    None = 0,
    Edit = 1,
    Create = 2,
    Rollback = 4,
    Execute = 8,
    Web = 16,
    BatchImage = 32,
}

internal enum CopilotIntentContext
{
    Workspace,
    Flow,
    NoWorkspace,
}

internal sealed record CopilotIntentSample(
    string Id,
    string Prompt,
    CopilotIntentTool[] Needs,
    CopilotIntentTool[] Forbids,
    CopilotIntentEvidence Evidence,
    CopilotIntentContext Context)
{
    public string Category => Id[..Id.IndexOf('-')];
}

internal static class CopilotIntentSamples
{
    // Synthetic prompts labeled by what the user is asking for, not by the current markers.
    // Needs: the request cannot be fulfilled without this tool. Forbids: the user ruled it out.
    // Evidence: what must actually happen before the answer may count as complete.
    public static IReadOnlyList<CopilotIntentSample> All { get; } =
    [
        // Database reads, phrased with and without the word "database".
        S("db-01", "数据库里有多少张表", [DatabaseRead]),
        S("db-02", "查一下数据库里 OK 和 NG 各有多少条", [DatabaseRead]),
        S("db-03", "结果库里 OK 和 NG 各有多少", [DatabaseRead]),
        S("db-04", "这张表有多少条结果", [DatabaseRead]),
        S("db-05", "昨天的检测结果存在哪张表里，帮我查一下有几条", [DatabaseRead]),
        S("db-06", "帮我统计一下今天的不良率", [DatabaseRead]),
        S("db-07", "批次 B2024 的测量数据查出来看看", [DatabaseRead]),
        S("db-08", "用 SQL 查一下最近十条算法结果", [DatabaseRead]),
        S("db-09", "MySQL 里最新的一条批次记录是什么", [DatabaseRead]),
        S("db-10", "结果表的字段有哪些", [DatabaseRead]),
        S("db-11", "最近一周每天测了多少片", [DatabaseRead]),
        S("db-12", "how many rows are in the database result table", [DatabaseRead]),
        S("db-13", "只查数据库，别改任何数据，看下 NG 有多少", [DatabaseRead], forbids: [DatabaseWrite]),
        S("db-14", "把数据库里昨天的测试记录删掉", [DatabaseWrite]),
        S("db-15", "把结果表里批次为 TEST 的记录清掉", [DatabaseWrite]),
        S("db-16", "把数据库里这条记录的状态更新成 OK", [DatabaseWrite]),
        S("db-17", "给结果表加一个备注字段", [DatabaseWrite]),
        S("db-18", "delete from the result table where batch is TEST", [DatabaseWrite]),

        // Workspace edits, from explicit phrasing to colloquial instructions.
        S("edit-01", "请修改这个文件，把超时改成 30 秒", [WorkspaceWrite], evidence: Edit),
        S("edit-02", "帮我修改代码，修掉这个空引用", [WorkspaceWrite], evidence: Edit),
        S("edit-03", "修复这个 bug", [WorkspaceWrite], evidence: Edit),
        S("edit-04", "帮我把这个类重构一下", [WorkspaceWrite], evidence: Edit),
        S("edit-05", "实现一下导出 CSV 的功能", [WorkspaceWrite], evidence: Edit),
        S("edit-06", "把这个函数里的空指针问题修掉", [WorkspaceWrite], evidence: Edit),
        S("edit-07", "这段逻辑有 bug，你处理下", [WorkspaceWrite], evidence: Edit),
        S("edit-08", "把配置文件里的超时时间调成 30 秒", [WorkspaceWrite], evidence: Edit),
        S("edit-09", "给 Foo 类加一个 Reset 方法", [WorkspaceWrite], evidence: Edit),
        S("edit-10", "把 README 里的版本号更新成 2.0", [WorkspaceWrite], evidence: Edit),
        S("edit-11", "这个方法名字不好，换成 LoadAsync，调用的地方也一起改了", [WorkspaceWrite], evidence: Edit),
        S("edit-12", "把日志级别从 Debug 调到 Info", [WorkspaceWrite], evidence: Edit),
        S("edit-13", "去掉这个没用的参数", [WorkspaceWrite], evidence: Edit),
        S("edit-14", "按刚才的方案改吧", [WorkspaceWrite], evidence: Edit),
        S("edit-15", "把这个文件删掉", [WorkspaceWrite], evidence: Edit),
        S("edit-16", "fix this null reference", [WorkspaceWrite], evidence: Edit),
        S("edit-17", "please refactor the Foo class", [WorkspaceWrite], evidence: Edit),
        S("edit-18", "rename this method to LoadAsync everywhere", [WorkspaceWrite], evidence: Edit),
        S("edit-19", "把这个 bug 修好，然后跑一下测试", [WorkspaceWrite, Execute], evidence: Edit | CopilotIntentEvidence.Execute),
        S("edit-20", "改完之后编译一下确认能过", [WorkspaceWrite, Execute], evidence: Edit | CopilotIntentEvidence.Execute),

        // Rewriting text in the chat is not a workspace change.
        S("text-01", "帮我改一下这句话的语气", []),
        S("text-02", "请修改一下回复的格式，用表格", []),
        S("text-03", "把上面的回答改短一点", []),
        S("text-04", "帮我修改这段邮件，语气礼貌一些", []),
        S("text-05", "把你刚才说的翻译成英文", []),
        S("text-06", "润色一下这段话：设备运行正常", []),
        S("text-07", "帮我改一下这个标题，更吸引人一点", []),
        S("text-08", "重新组织一下你的回答", []),
        S("text-09", "请修改回答，只保留结论", []),
        S("text-10", "please edit your answer to be shorter", []),

        // Spoken read-only constraints: investigating stays possible, writing does not.
        S("ro-01", "只读审计一下这个项目的代码", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-02", "不要修改任何文件，检查一下这个项目的异常处理", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-03", "别改代码，只分析原因", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-04", "这次只看不改", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-05", "先不要动文件，看看代码哪里有问题", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-06", "看看这个函数有什么问题，先别动代码", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-07", "只分析这个项目的启动流程", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-08", "帮我看下代码，不用改", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-09", "排查一下这个报错，不要动代码", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-10", "给个修复思路就行，别动手", forbids: [WorkspaceWrite]),
        S("ro-11", "怎么修复这个问题，只说思路", forbids: [WorkspaceWrite]),
        S("ro-12", "不改代码的前提下，定位一下内存泄漏在哪", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-13", "do not modify any file, review this project", [LocalSearch], forbids: [WorkspaceWrite]),
        S("ro-14", "just look, don't change anything", [LocalSearch], forbids: [WorkspaceWrite]),

        // Code questions that need local evidence but no edit.
        S("read-01", "这个项目里登录逻辑在哪里实现", [LocalSearch]),
        S("read-02", "查找 Foo 类的所有引用", [LocalSearch]),
        S("read-03", "这个函数是谁调用的", [LocalSearch]),
        S("read-04", "看一下这个项目的目录结构", [LocalSearch]),
        S("read-05", "超时时间是在哪配的", [LocalSearch]),
        S("read-06", "帮我找找哪里用到了 MQTT", [LocalSearch]),
        S("read-07", "项目里有没有写相机重连的逻辑", [LocalSearch]),
        S("read-08", "where is the login logic implemented in this repo", [LocalSearch]),

        // Running commands, builds and tests.
        S("run-01", "运行 dotnet test", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-02", "跑一下单元测试看看过没过", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-03", "build 一下看能不能编译", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-04", "帮我编译一下这个项目", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-05", "执行一下 git status 命令", [Shell], evidence: CopilotIntentEvidence.Execute),
        S("run-06", "跑一下这个 Python 脚本", [Shell], evidence: CopilotIntentEvidence.Execute),
        S("run-07", "在 PowerShell 里执行 Get-Date", [Shell], evidence: CopilotIntentEvidence.Execute),
        S("run-08", "验证一下这个项目现在能不能编译通过", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-09", "把开发服务器在后台跑起来", [Shell], evidence: CopilotIntentEvidence.Execute),
        S("run-10", "run the unit tests", [Execute], evidence: CopilotIntentEvidence.Execute),
        S("run-11", "怎么运行这个项目的单元测试"),
        S("run-12", "dotnet test 是干什么的"),

        // Creating files and rolling back the agent's own changes.
        S("new-01", "新建一个 Foo.cs 文件", [WorkspaceWrite], evidence: Create),
        S("new-02", "帮我创建一个配置文件 app.json", [WorkspaceWrite], evidence: Create),
        S("new-03", "在这个目录下加一个 README", [WorkspaceWrite], evidence: Create),
        S("new-04", "写一个脚本存到 tools 目录里", [WorkspaceWrite], evidence: Create),
        S("new-05", "create a new file named Foo.cs", [WorkspaceWrite], evidence: Create),
        S("new-06", "怎么新建一个 WPF 窗口"),
        S("back-01", "回滚刚才的修改", [WorkspaceWrite], evidence: Rollback),
        S("back-02", "撤销你刚才的改动", [WorkspaceWrite], evidence: Rollback),
        S("back-03", "刚才改错了，恢复原样", [WorkspaceWrite], evidence: Rollback),
        S("back-04", "roll back the patch you just applied", [WorkspaceWrite], evidence: Rollback),

        // Flow graph work with a flow open.
        S("flow-01", "当前流程有几个节点", [FlowInspect], context: Flow),
        S("flow-02", "看一下这个流程的结构", [FlowInspect], context: Flow),
        S("flow-03", "这个流程有什么问题", [FlowInspect], context: Flow),
        S("flow-04", "为什么这个流程没跑通", [FlowInspect], context: Flow),
        S("flow-05", "检查一下这个流程的连线对不对", [FlowInspect], context: Flow),
        S("flow-06", "POI 节点的参数现在是多少", [FlowInspect], context: Flow),
        S("flow-07", "流程里加一个 POI 节点", [FlowWrite], context: Flow),
        S("flow-08", "把这个流程的曝光节点删掉", [FlowWrite], context: Flow),
        S("flow-09", "把相机节点的曝光时间改成 100", [FlowWrite], context: Flow),
        S("flow-10", "把这两个节点连起来", [FlowWrite], context: Flow),
        S("flow-11", "什么是流程节点", context: Flow),
        S("flow-12", "看看这个流程，不要改", [FlowInspect], forbids: [FlowWrite], context: Flow),

        // Run history, incidents, devices and logs.
        S("ops-01", "上一次流程跑了多久", [FlowRunHistory]),
        S("ops-02", "今天流程执行了多少次，失败几次", [FlowRunHistory]),
        S("ops-03", "最近哪些流程运行失败了", [FlowRunHistory]),
        S("ops-04", "刚才那次流程为什么失败", [FlowRunHistory]),
        S("ops-05", "看一下设备服务的状态", [DeviceServices]),
        S("ops-06", "相机连不上了", [DeviceServices]),
        S("ops-07", "光谱仪一直显示离线", [DeviceServices]),
        S("ops-08", "现在哪些设备在线", [DeviceServices]),
        S("ops-09", "相机心跳正常吗", [DeviceServices]),
        S("ops-10", "设备诊断一下，为什么拍不了图", [DeviceServices]),
        S("ops-11", "看看最近的日志有什么报错", [RecentLog]),
        S("ops-12", "刚才软件报错了，帮我看看日志", [RecentLog]),
        S("ops-13", "软件刚才闪退了，帮我查查原因", [RecentLog]),
        S("ops-14", "check the recent application log", [RecentLog]),

        // Public web access.
        S("web-01", "搜索一下 OpenCV 5 的新特性", [WebSearch], evidence: Web),
        S("web-02", "联网查一下 .NET 10 的发布时间", [WebSearch], evidence: Web),
        S("web-03", "今天的新闻有什么", [WebSearch], evidence: Web),
        S("web-04", "上网查一下这个错误码", [WebSearch], evidence: Web),
        S("web-05", "现在最新的 Python 版本是多少", [WebSearch], evidence: Web),
        S("web-06", "读一下 https://example.com/spec 这个页面", [FetchUrl], evidence: Web),
        S("web-07", "总结这篇文章 https://example.com/post", [FetchUrl], evidence: Web),
        S("web-08", "search the web for OpenCV 5 release notes", [WebSearch], evidence: Web),
        S("web-09", "不要联网，解释一下什么是 MQTT", forbids: [WebSearch, FetchUrl]),
        S("web-10", "别上网，凭你知道的说说 SFR 是什么", forbids: [WebSearch, FetchUrl]),

        // Git state.
        S("git-01", "git status 是什么情况", [GitStatus]),
        S("git-02", "当前在哪个分支", [GitStatus]),
        S("git-03", "有哪些未跟踪文件", [GitStatus]),
        S("git-04", "看一下当前改动", [GitDiff]),
        S("git-05", "我改了什么", [GitDiff]),
        S("git-06", "这次提交前都动了哪些文件", [GitDiff]),
        S("git-07", "show me the git diff", [GitDiff]),
        S("git-08", "git rebase 是什么意思"),

        // Local machine inspection.
        S("sys-01", "ColorVision 这个进程还在吗", [WindowsProcess]),
        S("sys-02", "现在哪个进程占用 CPU 最高", [WindowsProcess]),
        S("sys-03", "MySQL 服务启动了吗", [WindowsService]),
        S("sys-04", "RegistrationCenterService 这个 Windows 服务在运行吗", [WindowsService]),
        S("sys-05", "1883 端口有人在监听吗", [TcpPort]),
        S("sys-06", "看看 3306 端口通不通", [TcpPort]),

        // Batch image work is still enforced from text, so questions must stay out of it.
        S("batch-01", "批量处理这些图像：应用直方图均衡并导出", evidence: CopilotIntentEvidence.BatchImage),
        S("batch-02", "请把这些图片反相", evidence: CopilotIntentEvidence.BatchImage),
        S("batch-03", "Run Canny on these images", evidence: CopilotIntentEvidence.BatchImage),
        S("batch-04", "什么是白平衡"),
        S("batch-05", "这些图片需要做白平衡吗"),
        S("batch-06", "批量处理图片一般有哪些算法"),
        S("batch-07", "这批图片的白平衡效果怎么样"),
        S("batch-08", "怎么把这些图片做反相"),

        // Concepts and small talk: no work has to be proven.
        S("talk-01", "你好"),
        S("talk-02", "什么是 POI"),
        S("talk-03", "SFR 和 MTF 有什么区别"),
        S("talk-04", "解释一下色度学里的 xy 坐标"),
        S("talk-05", "thread pool 怎么用"),
        S("talk-06", "C# 的 record 和 class 有什么区别"),
        S("talk-07", "为什么 WPF 要用 Dispatcher"),
        S("talk-08", "如何修复空引用异常，讲讲一般思路"),
        S("talk-09", "数据库索引的原理是什么"),
        S("talk-10", "SQL 的 join 有几种"),
        S("talk-11", "谢谢，辛苦了"),
        S("talk-12", "what is a mutex"),
        S("talk-13", "实现一个单例模式有哪些写法"),
        S("talk-14", "重构和重写有什么区别"),

        // Without a workspace there is nothing to edit, so no edit can be demanded.
        S("nows-01", "帮我修改代码，修掉这个空引用", context: NoWorkspace),
        S("nows-02", "修复这个 bug", context: NoWorkspace),
        S("nows-03", "写一个快速排序给我看看", context: NoWorkspace),
        S("nows-04", "数据库里 OK 和 NG 各有多少", [DatabaseRead], context: NoWorkspace),
        S("nows-05", "相机连不上了", [DeviceServices], context: NoWorkspace),
    ];

    private const CopilotIntentTool LocalSearch = CopilotIntentTool.LocalSearch;
    private const CopilotIntentTool WorkspaceWrite = CopilotIntentTool.WorkspaceWrite;
    private const CopilotIntentTool Execute = CopilotIntentTool.Execute;
    private const CopilotIntentTool Shell = CopilotIntentTool.Shell;
    private const CopilotIntentTool GitDiff = CopilotIntentTool.GitDiff;
    private const CopilotIntentTool GitStatus = CopilotIntentTool.GitStatus;
    private const CopilotIntentTool DatabaseRead = CopilotIntentTool.DatabaseRead;
    private const CopilotIntentTool DatabaseWrite = CopilotIntentTool.DatabaseWrite;
    private const CopilotIntentTool FlowInspect = CopilotIntentTool.FlowInspect;
    private const CopilotIntentTool FlowWrite = CopilotIntentTool.FlowWrite;
    private const CopilotIntentTool FlowRunHistory = CopilotIntentTool.FlowRunHistory;
    private const CopilotIntentTool DeviceServices = CopilotIntentTool.DeviceServices;
    private const CopilotIntentTool RecentLog = CopilotIntentTool.RecentLog;
    private const CopilotIntentTool WebSearch = CopilotIntentTool.WebSearch;
    private const CopilotIntentTool FetchUrl = CopilotIntentTool.FetchUrl;
    private const CopilotIntentTool WindowsProcess = CopilotIntentTool.WindowsProcess;
    private const CopilotIntentTool WindowsService = CopilotIntentTool.WindowsService;
    private const CopilotIntentTool TcpPort = CopilotIntentTool.TcpPort;

    private const CopilotIntentEvidence Edit = CopilotIntentEvidence.Edit;
    private const CopilotIntentEvidence Create = CopilotIntentEvidence.Create;
    private const CopilotIntentEvidence Rollback = CopilotIntentEvidence.Rollback;
    private const CopilotIntentEvidence Web = CopilotIntentEvidence.Web;

    private const CopilotIntentContext Flow = CopilotIntentContext.Flow;
    private const CopilotIntentContext NoWorkspace = CopilotIntentContext.NoWorkspace;

    private static CopilotIntentSample S(
        string id,
        string prompt,
        CopilotIntentTool[]? needs = null,
        CopilotIntentTool[]? forbids = null,
        CopilotIntentEvidence evidence = CopilotIntentEvidence.None,
        CopilotIntentContext context = CopilotIntentContext.Workspace) =>
        new(id, prompt, needs ?? [], forbids ?? [], evidence, context);
}
