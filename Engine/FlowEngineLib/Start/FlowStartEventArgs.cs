using System;
using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;

namespace FlowEngineLib.Start;

public class FlowStartEventArgs : EventArgs
{
	public string SerialNumber { get; set; }

	public string Message { get; set; }

	public StatusTypeEnum Status { get; set; }

	public long TotalTime { get; set; }

	public string ErrorNodeName { get; set; }

	public string ErrorNodeId { get; set; }

	public IReadOnlyList<FlowHandledFailure> HandledFailures { get; set; }

	public FlowStartEventArgs(
		string serialNumber,
		StatusTypeEnum status,
		long totalTime,
		string message,
		string errorNodeName = "",
		string errorNodeId = "",
		IReadOnlyList<FlowHandledFailure> handledFailures = null)
	{
		SerialNumber = serialNumber;
		Message = message;
		Status = status;
		TotalTime = totalTime;
		ErrorNodeName = errorNodeName;
		ErrorNodeId = errorNodeId;
		HandledFailures = handledFailures ?? Array.Empty<FlowHandledFailure>();
	}
}
