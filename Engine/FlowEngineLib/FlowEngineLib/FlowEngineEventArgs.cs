using System;
using FlowEngineLib.Base;

namespace FlowEngineLib;

public class FlowEngineEventArgs : EventArgs
{
	public string StartNodeName { get; set; }

	public string SerialNumber { get; set; }

	public string Message { get; set; }

	public StatusTypeEnum Status { get; set; }

	public long TotalTime { get; set; }

	public FlowEngineEventArgs(string startNodeName, string serialNumber, StatusTypeEnum status, long totalTime, string message)
	{
		StartNodeName = startNodeName;
		SerialNumber = serialNumber;
		Status = status;
		TotalTime = totalTime;
		Message = message;
	}
}
