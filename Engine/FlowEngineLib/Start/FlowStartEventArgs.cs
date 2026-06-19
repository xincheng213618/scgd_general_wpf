using System;
using FlowEngineLib.Base;

namespace FlowEngineLib.Start;

public class FlowStartEventArgs : EventArgs
{
	public string SerialNumber { get; set; }

	public string Message { get; set; }

	public StatusTypeEnum Status { get; set; }

	public long TotalTime { get; set; }

	public string ErrorNodeName { get; set; }

	public FlowStartEventArgs(string serialNumber, StatusTypeEnum status, long totalTime, string message, string errorNodeName = "")
	{
		SerialNumber = serialNumber;
		Message = message;
		Status = status;
		TotalTime = totalTime;
		ErrorNodeName = errorNodeName;
	}
}
