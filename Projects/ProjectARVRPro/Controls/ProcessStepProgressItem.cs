using ColorVision.Common.MVVM;
using ColorVision.UI.Controls;
using ProjectARVRPro.Process;

namespace ProjectARVRPro.Controls;

public sealed class ProcessStepProgressItem : ViewModelBase
{
    public ProcessMeta Meta { get; }
    public int Number { get; }
    public bool IsFirst { get; }
    public bool IsLast { get; }
    public CircularProgressState State { get => _state; private set { _state = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; private set { _progress = value; OnPropertyChanged(); } }
    public string StatusText { get => _statusText; private set { _statusText = value; OnPropertyChanged(); } }
    public string Details { get => _details; private set { _details = value; OnPropertyChanged(); } }
    private CircularProgressState _state;
    private double _progress = double.NaN;
    private string _statusText = string.Empty;
    private string _details = string.Empty;

    internal ProcessStepProgressItem(ProcessMeta meta, int number, bool isFirst, bool isLast)
    {
        Meta = meta;
        Number = number;
        IsFirst = isFirst;
        IsLast = isLast;
        Reset();
    }

    internal void Reset()
    {
        State = CircularProgressState.Waiting;
        Progress = double.NaN;
        StatusText = DisplayText.Get("等待");
        UpdateDetails();
    }

    internal void Start()
    {
        State = CircularProgressState.Running;
        Progress = double.NaN;
        StatusText = DisplayText.Get("执行中");
        UpdateDetails();
    }

    internal void UpdateElapsed(long elapsedMilliseconds, long expectedMilliseconds)
    {
        if (State != CircularProgressState.Running) return;
        Progress = expectedMilliseconds > 0 ? Math.Clamp((double)Math.Max(0, elapsedMilliseconds) / expectedMilliseconds * 100, 0, 99) : double.NaN;
        StatusText = DisplayText.Get("执行中") + $" · {Math.Max(0, elapsedMilliseconds) / 1000d:N1} s";
        UpdateDetails();
    }

    internal void Finish(bool succeeded)
    {
        State = succeeded ? CircularProgressState.Completed : CircularProgressState.Failed;
        if (succeeded) Progress = 100;
        StatusText = DisplayText.Get(succeeded ? "已完成" : "失败");
        UpdateDetails();
    }

    private void UpdateDetails()
    {
        Details = $"{Meta.Name}\n{DisplayText.Get("流程模板")}: {Meta.FlowTemplate}\n{StatusText}";
        if (State == CircularProgressState.Running && double.IsFinite(Progress))
            Details += $"\n{Progress:F0}% · " + DisplayText.Get("进度根据历史耗时估算，以实际完成为准。");
        Details += "\n" + DisplayText.Get("双击打开流程模板，右键编辑配置。");
    }
}
