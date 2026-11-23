using FlowEngineLib.MQTT;
using FlowEngineLib.Start;
using log4net;
using MQTTnet;
using System;
using System.Threading;

namespace FlowEngineLib.Base;

public delegate Action<CVServerResponse> RemoteFlowResultBinder(
	CVStartCFC flow, CVMQTTRequest request, string sendTopic, string replyTopic, Func<MqttClientOptions, bool> matchesBroker);

/// <summary>Associates host-owned result publication with a pending legacy remote command.</summary>
public static class RemoteFlowResultTracking
{
	private static readonly ILog log = LogManager.GetLogger(typeof(RemoteFlowResultTracking));

	public static RemoteFlowResultBinder BindRegisteredResult { private get; set; }

	internal static void TrackRegistered(CVStartCFC flow, BaseStartNode start, MQActionEvent action, CVBaseEventCmd command,
		RemoteFlowResultBinder binder)
	{
		binder ??= BindRegisteredResult;
		if (binder == null || command.RemoteResult != null || string.IsNullOrEmpty(command.ReplyTopic)) return;
		try
		{
			Func<MqttClientOptions, bool> matchesConnection = null;
			Action<CVServerResponse> publish = binder(flow, command.cmd, action.Topic, command.ReplyTopic,
				options => Volatile.Read(ref matchesConnection)?.Invoke(options) == true);
			if (publish == null) return;

			Track(command, publish);
			if (start is MQTTStartNode)
				action.ObservePublishConnection = connection => Volatile.Write(ref matchesConnection, connection);
			else
				Volatile.Write(ref matchesConnection, _ => true);
		}
		catch (Exception error)
		{
			log.Warn("无法绑定远程流程结果的来源，本次流程仍按原协议执行。", error);
		}
	}

	internal static void Track(CVBaseEventCmd command, Action<CVServerResponse> publish) =>
		command.RemoteResult = new PendingRemoteFlowResult(command.cmd, command.ReplyTopic, publish);

	internal static void Complete(CVStartCFC flow, CVBaseEventCmd command, CVServerResponse response)
	{
		var pending = command.RemoteResult;
		command.RemoteResult = null;
		if (pending == null || flow.RuntimeResources.StopToken.IsCancellationRequested
			|| response.Status != ActionStatusEnum.Finish || !response.HasExplicitCode) return;

		try { pending.Publish?.Invoke(response); }
		catch (Exception error) { log.Warn("无法发布远程流程结果。", error); }
	}
}

internal sealed class PendingRemoteFlowResult
{
	private readonly string eventName;
	private readonly string deviceCode;
	private readonly string replyTopic;

	internal Action<CVServerResponse> Publish { get; }

	internal PendingRemoteFlowResult(CVMQTTRequest request, string replyTopic, Action<CVServerResponse> publish)
	{
		eventName = request.EventName;
		deviceCode = request.DeviceCode;
		this.replyTopic = replyTopic;
		Publish = publish;
	}

	internal bool CanAccept(CVBaseDataFlowResp response) =>
		string.Equals(eventName, response.EventName, StringComparison.Ordinal)
		&& (string.IsNullOrEmpty(response.DeviceCode) || string.Equals(deviceCode, response.DeviceCode, StringComparison.Ordinal))
		&& (response.ReceivedTopic == null || string.Equals(replyTopic, response.ReceivedTopic, StringComparison.Ordinal));
}
