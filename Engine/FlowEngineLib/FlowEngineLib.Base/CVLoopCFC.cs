using FlowEngineLib.Start;
using Newtonsoft.Json;

namespace FlowEngineLib.Base;

public class CVLoopCFC
{
	[JsonIgnore]
	public BaseStartNode StartNode;

	public string SerialNumber;

	public string NodeName;

	public CVLoopCFC(CVStartCFC startCFC, string nodeName)
		: this(startCFC.GetStartNode(), startCFC.GetSerialNumber(), nodeName)
	{
	}

	public CVLoopCFC(BaseStartNode startNode, string serialNumber, string nodeName)
	{
		StartNode = startNode;
		SerialNumber = serialNumber;
		NodeName = nodeName;
	}

	internal void DoLoopNextAction()
	{
		StartNode.DoLoopNextAction(this);
	}
}
