using FlowEngineLib.Algorithm;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System.Threading;

namespace FlowEngineLib.Base;

public class CVBaseServerNodeIn2Hub : CVBaseServerNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(CVBaseServerNodeIn2Hub));

	protected STNodeOption m_in2_start;

	protected CVStartCFC[] masterInput;

    private readonly object inputSync = new();
    private readonly AsyncLocal<CVStartCFC[]> requestInputs = new();

	protected string m_in2_text;

	protected CVBaseServerNodeIn2Hub(string title, string nodeType, string nodeName, string deviceCode, int inNum = 2)
		: base(title, nodeType, nodeName, deviceCode)
	{
		m_in_text = "IN_1";
		m_in2_text = "IN_2";
		masterInput = new CVStartCFC[inNum];
		base.Height += 15;
		m_custom_item.Y += 15;
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		m_in2_start = base.InputOptions.Add(m_in2_text, typeof(CVStartCFC), bSingle: true);
		m_in2_start.Connected += m_in_op_Connected;
		m_in2_start.DataTransfer += m_in_start_DataTransfer;
	}

	protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
	{
		DoInputDataTransfer(sender as STNodeOption, e);
	}

	protected override bool getPreStepParam(int idx, AlgorithmPreStepParam param)
	{
		CVStartCFC[] inputs = requestInputs.Value ?? masterInput;
		if (param != null && idx >= 0 && idx < inputs.Length && inputs[idx] != null)
		{
			CVStartCFC input = inputs[idx];
			bool hasMasterResult = input.Data.ContainsKey("MasterId")
				|| input.Data.ContainsKey("MasterResultType")
				|| input.Data.ContainsKey("MasterValue");
			if (hasMasterResult)
			{
				_getPreStepParam(input, param);
				return true;
			}
		}

		return base.getPreStepParam(idx, param);
	}

    private static void FinishFlow(CVStartCFC start)
    {
        if (start.TryDoFinishing()) start.FireFinished();
    }

    private void DoInputDataTransfer(STNodeOption sender, STNodeOptionEventArgs e)
    {
        if (e.Status != ConnectionStatus.Connected) return;
        if (!HasData(e))
        {
            lock (inputSync) ClearInputs();
            m_op_end.TransferData(null);
            return;
        }
        if (e.TargetOption.Data is not CVStartCFC action)
        {
            logger.WarnFormat("TargetData Type is not flow common type => {0}", e.TargetOption.DataType.AssemblyQualifiedName);
            return;
        }
        action.NormalizeStopStatus();
        if (action.IsPaused)
        {
            DoTransferToServer(action, e);
            return;
        }
        if (action.TryGetStopStatus(out _))
        {
            // A delayed terminal notification must not erase inputs of a newer run/round.
            lock (inputSync) ClearInputs(action);
            FinishFlow(action);
            m_op_end.TransferData(action);
            return;
        }

        CVStartCFC[] readyInputs;
        CVStartCFC execution;
        lock (inputSync)
        {
            sender.Data = new CVStartCFC(action);
            int received = 0;
            for (int index = 0; index < InputOptionsCount; index++)
            {
                STNodeOption input = InputOptions[index];
                if (input.DataType != typeof(CVStartCFC)) continue;
                if (input.Data is not CVStartCFC pending) continue;
                if (!action.IsSameExecutionIteration(pending))
                {
                    input.Data = null;
                    masterInput[index] = null;
                    continue;
                }
                masterInput[index] = pending;
                received++;
            }
            if (logger.IsDebugEnabled)
                logger.DebugFormat("[{0}][{1}/{2}] DoServerTransfer => {3} [{4}/{5}]", ToShortString(), received, InputOptionsCount,
                    action.ToShortString(), sender.Text, JsonConvert.SerializeObject(action.Data));
            if (received != InputOptionsCount) return;
            readyInputs = (CVStartCFC[])masterInput.Clone();
            execution = new CVStartCFC(action);
            // Consume the complete set before publishing: a synchronous response can start the next round here.
            ClearInputs();
        }

        CVStartCFC[] previousInputs = requestInputs.Value;
        try
        {
            requestInputs.Value = readyInputs;
            CVStartCFC stopped = System.Array.Find(readyInputs, input => { input.NormalizeStopStatus(); return !input.IsRunning; });
            if (stopped != null)
            {
                execution.SetStatusType(stopped.FlowStatus);
                FinishFlow(execution);
                m_op_end.TransferData(execution);
            }
            else DoTransferToServer(execution, e);
        }
        finally
        {
            requestInputs.Value = previousInputs;
        }
    }

    private void ClearInputs(CVStartCFC action = null)
    {
        for (int index = 0; index < InputOptionsCount; index++)
        {
            STNodeOption input = InputOptions[index];
            if (input.DataType != typeof(CVStartCFC)) continue;
            if (action == null || input.Data is CVStartCFC pending && (action.IsSameExecutionIteration(pending)
                || ReferenceEquals(action.RuntimeResources, pending.RuntimeResources)))
                input.Data = null;
            if (action == null || masterInput[index] != null && (action.IsSameExecutionIteration(masterInput[index])
                || ReferenceEquals(action.RuntimeResources, masterInput[index].RuntimeResources)))
                masterInput[index] = null;
        }
    }
}
