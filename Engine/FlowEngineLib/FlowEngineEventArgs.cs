using System;
using System.Collections.Generic;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;

namespace FlowEngineLib;

public class FlowEngineEventArgs : EventArgs
{
	public string StartNodeName { get; set; }

	public string SerialNumber { get; set; }

	public string Message { get; set; }

	public StatusTypeEnum Status { get; set; }

	public long TotalTime { get; set; }

	public string ErrorNodeName { get; set; }

	public string ErrorNodeId { get; set; }

	public IReadOnlyList<FlowHandledFailure> HandledFailures { get; set; }

	public FlowEngineEventArgs(
		string startNodeName,
		string serialNumber,
		StatusTypeEnum status,
		long totalTime,
		string message,
		string errorNodeName = "",
		string errorNodeId = "",
		IReadOnlyList<FlowHandledFailure> handledFailures = null)
	{
		StartNodeName = startNodeName;
		SerialNumber = serialNumber;
		Status = status;
		TotalTime = totalTime;
		Message = message;
		ErrorNodeName = errorNodeName;
		ErrorNodeId = errorNodeId;
		HandledFailures = handledFailures ?? Array.Empty<FlowHandledFailure>();
	}
}
