using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using FlowEngineLib.Start;
using log4net;
using System;

namespace FlowEngineLib.Algorithm;

public delegate Action<CVServerResponse> CalibrationResultBinder(
	CVStartCFC flow, BaseStartNode start, CVMQTTRequest request, string sendTopic, string replyTopic);

/// <summary>Associates host-owned result publication with an actual legacy remote calibration request.</summary>
public static class CalibrationFlowResultTracking
{
	private static readonly ILog log = LogManager.GetLogger(typeof(CalibrationFlowResultTracking));
	public static CalibrationResultBinder BindResult { private get; set; }

	public static void Track(CVStartCFC flow, MQActionEvent action, CVBaseEventCmd command, CalibrationResultBinder binder)
	{
		try
		{
			CVMQTTRequest request = command.cmd;
			Action<CVServerResponse> publish = (binder ?? BindResult)?.Invoke(
				flow, flow.GetStartNode(), request, action.Topic, command.ReplyTopic);
			RemoteFlowResultTracking.Track(command, publish);
		}
		catch (Exception error)
		{
			log.Warn("无法绑定远程流程校正的结果来源，本次流程仍按原协议执行。", error);
		}
	}
}
