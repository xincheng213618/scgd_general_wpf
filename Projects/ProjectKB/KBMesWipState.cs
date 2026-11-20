namespace ProjectKB;

internal sealed class KBMesWipState
{
    private (string SerialNumber, string Stage)? _run;
    private Check? _inputCheck;
    private Check? _runCheck;

    public void InputChanged(string serialNumber)
    {
        if (_inputCheck?.Identity.SerialNumber != serialNumber)
            _inputCheck = null;
    }

    public Check BeginCheck(string serialNumber, string stage)
    {
        var check = new Check((serialNumber, stage));
        _inputCheck = check;
        if (_run == check.Identity)
            _runCheck = check;
        return check;
    }

    public bool IsCurrentInputCheck(Check check, string stage)
        => ReferenceEquals(_inputCheck, check) && check.Identity.Stage == stage;

    public void BeginRun(string serialNumber, string stage)
    {
        _run = (serialNumber, stage);
        _runCheck = _inputCheck?.Identity == _run ? _inputCheck : null;
    }

    public bool ConsumeRunApproval(string serialNumber, string stage)
    {
        Check? check = _runCheck;
        _runCheck = null;
        if (ReferenceEquals(_inputCheck, check))
            _inputCheck = null;
        return check is { IsApproved: true } && check.Identity == (serialNumber, stage);
    }

    public void EndRun()
    {
        _run = null;
        _runCheck = null;
    }

    internal sealed class Check((string SerialNumber, string Stage) identity)
    {
        public (string SerialNumber, string Stage) Identity { get; } = identity;
        public bool IsApproved { get; set; }
    }
}
