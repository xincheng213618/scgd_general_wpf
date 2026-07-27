using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using FlowEngineLib.Base;
using FlowEngineLib.MQTT;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;

namespace FlowEngineLib.Start;

[STNode("/00 全局")]
public class MQTTStartNode : BaseStartNode
{
	private static readonly ILog logger = LogManager.GetLogger(typeof(MQTTStartNode));

	private static readonly TimeSpan PublishReadyTimeout = TimeSpan.FromSeconds(3);

	private string _Server = "127.0.0.1";

	private int _Port = 1883;

	private string _StartTopic;

	private string _StatusTopicName;

	private MQTTHelper _MQTTHelper;

	private readonly SemaphoreSlim mqttLifecycleLock = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim subscriptionRestoreLock = new SemaphoreSlim(1, 1);

	private readonly SemaphoreSlim publishLock = new SemaphoreSlim(1, 1);

	private readonly object mqttSessionLock = new object();

	private CancellationTokenSource mqttSessionCts = CreateCanceledTokenSource();

	private long mqttSessionGeneration;

	private long startTopicVersion;

	private int restoredTopicSubscriptionVersion = -1;

	private long restoredStartTopicVersion = -1;

	private bool connectionWanted;

	private volatile bool isDisposed;

	public override bool RequiresConnectionReady => true;

	public override bool IsExecutionReady
	{
		get
		{
			MQTTHelper mqttHelper = _MQTTHelper;
			lock (mqttSessionLock)
			{
				return connectionWanted
					&& !isDisposed
					&& Ready
					&& mqttHelper != null
					&& mqttHelper.IsClientConnect()
					&& restoredTopicSubscriptionVersion == TopicSubscriptionVersion
					&& restoredStartTopicVersion == startTopicVersion;
			}
		}
	}

	[STNodeProperty("Server", "Server")]
	public string Server
	{
		get
		{
			return _Server;
		}
		set
		{
			_Server = value;
			OnPropertyChanged();
		}
	}

	[STNodeProperty("Port", "Port")]
	public int Port
	{
		get
		{
			return _Port;
		}
		set
		{
			_Port = value;
			OnPropertyChanged();
		}
	}

	public MQTTStartNode()
		: base("Start_MQTT")
	{
	}

	protected override void OnCreate()
	{
		base.OnCreate();
		_StartTopic = "FLOW/CMD";
		_StatusTopicName = "FLOW/STATUS";
		base.TitleColor = Color.FromArgb(200, Color.Goldenrod);
	}

	protected override void OnNodeNameChanged(string oldValue, string newValue)
	{
		Ready = false;
		restoredTopicSubscriptionVersion = -1;
		Interlocked.Increment(ref startTopicVersion);
		MQTTHelper mqttHelper = _MQTTHelper;
		if (mqttHelper != null && mqttHelper.IsClientConnect()
			&& TryCaptureMqttSession(out long generation, out long topicVersion, out CancellationToken sessionToken))
		{
			_ = ChangeStartTopicAsync(mqttHelper, oldValue, generation, topicVersion, sessionToken);
		}
	}

	protected override void DoStartConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		BeginMqttSession();
		_ = EnsureReadyAfterConnectionAsync();
	}

	protected override void DoStartDisConnected(STNodeOption sender, STNodeOptionEventArgs e)
	{
		if (sender.ConnectionCount == 0)
		{
			EndMqttSession();
		}
	}

	private static CancellationTokenSource CreateCanceledTokenSource()
	{
		var cancellationTokenSource = new CancellationTokenSource();
		cancellationTokenSource.Cancel();
		return cancellationTokenSource;
	}

	private void BeginMqttSession()
	{
		CancellationTokenSource previousSession = null;
		lock (mqttSessionLock)
		{
			if (isDisposed || connectionWanted)
			{
				return;
			}
			previousSession = mqttSessionCts;
			mqttSessionCts = new CancellationTokenSource();
			connectionWanted = true;
			mqttSessionGeneration++;
		}
		previousSession.Cancel();
		previousSession.Dispose();
	}

	private void EndMqttSession()
	{
		CancellationTokenSource previousSession;
		lock (mqttSessionLock)
		{
			connectionWanted = false;
			mqttSessionGeneration++;
			previousSession = mqttSessionCts;
			mqttSessionCts = CreateCanceledTokenSource();
		}
		previousSession.Cancel();
		previousSession.Dispose();
		ReleaseMQTTClient();
	}

	private bool TryCaptureMqttSession(out long generation, out long topicVersion, out CancellationToken sessionToken)
	{
		lock (mqttSessionLock)
		{
			if (isDisposed || !connectionWanted)
			{
				generation = 0;
				topicVersion = 0;
				sessionToken = default;
				return false;
			}
			generation = mqttSessionGeneration;
			topicVersion = startTopicVersion;
			sessionToken = mqttSessionCts.Token;
			return true;
		}
	}

	private bool IsMqttSessionCurrent(long generation, long topicVersion)
	{
		lock (mqttSessionLock)
		{
			return !isDisposed
				&& connectionWanted
				&& mqttSessionGeneration == generation
				&& startTopicVersion == topicVersion;
		}
	}

	private void ThrowIfMqttSessionChanged(long generation, long topicVersion, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (!IsMqttSessionCurrent(generation, topicVersion))
		{
			throw new OperationCanceledException(cancellationToken);
		}
	}

	private async Task EnsureReadyAfterConnectionAsync()
	{
		try
		{
			await EnsureReadyAsync().ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.Warn("Failed to prepare MQTT start node.", ex);
		}
	}

	private void ReleaseMQTTClient()
	{
		Ready = false;
		restoredTopicSubscriptionVersion = -1;
		restoredStartTopicVersion = -1;
		MQTTHelper mqttHelper = Interlocked.Exchange(ref _MQTTHelper, null);
		if (mqttHelper != null)
		{
			_ = mqttHelper.DisconnectAsync_Client();
		}
	}

	public override async Task<bool> EnsureReadyAsync(CancellationToken cancellationToken = default)
	{
		if (!TryCaptureMqttSession(out long generation, out long topicVersion, out CancellationToken sessionToken))
		{
			return false;
		}
		using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, sessionToken);
		CancellationToken linkedToken = linkedTokenSource.Token;
		MQTTHelper current = _MQTTHelper;
		if (IsExecutionReady)
		{
			return true;
		}

		try
		{
			await mqttLifecycleLock.WaitAsync(linkedToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			return false;
		}
		try
		{
			ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
			current = _MQTTHelper;
			if (current != null && !current.IsClientConnect())
			{
				await current.TryReconnectAsync_Client(linkedToken).ConfigureAwait(false);
				ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
			}
			if (current == null || !current.IsClientConnect())
			{
				if (current != null)
				{
					Interlocked.CompareExchange(ref _MQTTHelper, null, current);
					await current.DisconnectAsync_Client().ConfigureAwait(false);
				}

				ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
				current = new MQTTHelper();
				Interlocked.Exchange(ref _MQTTHelper, current);
				ResultData_MQTT result = await current.CreateMQTTClientAndStart(
					_Server,
					_Port,
					string.Empty,
					string.Empty,
					onMsgSub,
					linkedToken).ConfigureAwait(false);
				ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
				if (result.ResultCode <= 0 || !current.IsClientConnect())
				{
					await current.TryReconnectAsync_Client(linkedToken).ConfigureAwait(false);
					ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
				}
				if (!current.IsClientConnect())
				{
					Ready = false;
					if (ReferenceEquals(Interlocked.CompareExchange(ref _MQTTHelper, null, current), current))
					{
						await current.DisconnectAsync_Client().ConfigureAwait(false);
						ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
					}
					logger.WarnFormat("MQTT start node connection failed => {0}:{1}", _Server, _Port);
					return false;
				}
			}

			return await RestoreSubscriptionsAsync(current, generation, topicVersion, linkedToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			Ready = false;
			await ReleaseStaleSessionHelperAsync(current, generation, topicVersion).ConfigureAwait(false);
			return false;
		}
		finally
		{
			mqttLifecycleLock.Release();
		}
	}

	private async Task ReleaseStaleSessionHelperAsync(MQTTHelper mqttHelper, long generation, long topicVersion)
	{
		if (mqttHelper != null
			&& !IsMqttSessionCurrent(generation, topicVersion)
			&& ReferenceEquals(Interlocked.CompareExchange(ref _MQTTHelper, null, mqttHelper), mqttHelper))
		{
			await mqttHelper.DisconnectAsync_Client().ConfigureAwait(false);
		}
	}

	private async Task ChangeStartTopicAsync(
		MQTTHelper mqttHelper,
		string oldNodeName,
		long generation,
		long topicVersion,
		CancellationToken sessionToken)
	{
		try
		{
			await mqttHelper.UnsubscribeAsync_Client(GetStartTopic(oldNodeName)).WaitAsync(sessionToken).ConfigureAwait(false);
			ThrowIfMqttSessionChanged(generation, topicVersion, sessionToken);
			await RestoreSubscriptionsAsync(mqttHelper, generation, topicVersion, sessionToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			Ready = false;
		}
		catch (Exception ex)
		{
			Ready = false;
			logger.Warn("Failed to update MQTT start topic.", ex);
		}
	}

	private async Task<bool> RestoreSubscriptionsAsync(
		MQTTHelper mqttHelper,
		long generation,
		long topicVersion,
		CancellationToken cancellationToken)
	{
		await subscriptionRestoreLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			ThrowIfMqttSessionChanged(generation, topicVersion, cancellationToken);
			if (!ReferenceEquals(_MQTTHelper, mqttHelper)
				|| !mqttHelper.IsClientConnect())
			{
				Ready = false;
				return false;
			}

			Ready = false;
			while (true)
			{
				(int version, string[] topics) = GetTopicSubscriptionsSnapshot();
				bool startTopicSubscribed = await mqttHelper.TrySubscribeAsync_Client(GetStartTopic(), cancellationToken).ConfigureAwait(false);
				ThrowIfMqttSessionChanged(generation, topicVersion, cancellationToken);
				if (!startTopicSubscribed)
				{
					return false;
				}
				foreach (string topic in topics)
				{
					bool responseTopicSubscribed = await mqttHelper.TrySubscribeAsync_Client(topic, cancellationToken).ConfigureAwait(false);
					ThrowIfMqttSessionChanged(generation, topicVersion, cancellationToken);
					if (!responseTopicSubscribed)
					{
						return false;
					}
				}

				if (version == TopicSubscriptionVersion
					&& IsMqttSessionCurrent(generation, topicVersion))
				{
					restoredTopicSubscriptionVersion = version;
					restoredStartTopicVersion = topicVersion;
					Ready = ReferenceEquals(_MQTTHelper, mqttHelper)
						&& mqttHelper.IsClientConnect();
					if (Ready)
					{
						logger.DebugFormat("MQTT subscriptions restored => start={0}, responses={1}", GetStartTopic(), topics.Length);
					}
					return Ready;
				}
			}
		}
		catch (OperationCanceledException)
		{
			Ready = false;
			throw;
		}
		catch (Exception ex)
		{
			Ready = false;
			logger.Warn("Failed to restore MQTT subscriptions.", ex);
			return false;
		}
		finally
		{
			subscriptionRestoreLock.Release();
		}
	}

	private string GetStartTopic(string nodeName)
	{
		return _StartTopic + "/" + nodeName;
	}

	private string GetStartTopic()
	{
		return GetStartTopic(m_nodeName);
	}

	private string GetStatusTopic()
	{
		return _StatusTopicName + "/" + m_nodeName;
	}

	private void onMsgSub(ResultData_MQTT resultData_MQTT)
	{
		if (isDisposed)
		{
			return;
		}
		string value = (string)resultData_MQTT.ResultObject2;
		string text = (string)resultData_MQTT.ResultObject1;
		if (resultData_MQTT.EventType == EventTypeEnum.MsgRecv)
		{
			if (!HasCurrentMqttClient(_MQTTHelper))
			{
				return;
			}
			CVMQTTRequest cVMQTTRequest = null;
			if (!string.IsNullOrEmpty(value))
			{
				cVMQTTRequest = JsonConvert.DeserializeObject<CVMQTTRequest>(value);
			}
			if (string.IsNullOrEmpty(text) || cVMQTTRequest == null)
			{
				return;
			}
			List<CVBaseServerNode> serverSubscribers = GetServerSubscribersSnapshot(text);
			if (serverSubscribers != null)
			{
				CVBaseDataFlowResp statusEvent = JsonConvert.DeserializeObject<CVBaseDataFlowResp>(value);
				using List<CVBaseServerNode>.Enumerator enumerator = serverSubscribers.GetEnumerator();
				while (enumerator.MoveNext() && !enumerator.Current.DoServerStatusRecv(statusEvent))
				{
				}
				return;
			}
			List<CVServiceProxy> proxySubscribers = GetServiceProxySubscribersSnapshot(text);
			if (proxySubscribers != null)
			{
				CVBaseDataFlowResp statusEvent = JsonConvert.DeserializeObject<CVBaseDataFlowResp>(value);
				foreach (CVServiceProxy proxy in proxySubscribers)
				{
					proxy.DoServerStatusDataTransfer(statusEvent);
				}
				return;
			}
			if (text.Equals(GetStartTopic()))
			{
				CVStartCFC action = GetAction(cVMQTTRequest);
				if (action != null)
				{
					DoDispatch(action);
				}
			}
		}
		else if (resultData_MQTT.EventType == EventTypeEnum.ClientConnected)
		{
			MQTTHelper mqttHelper = _MQTTHelper;
			if (resultData_MQTT.ResultCode > 0 && mqttHelper != null && mqttHelper.IsClientConnect())
			{
				QueueSubscriptionRestore();
				logger.Debug("MQTT connected; restoring subscriptions.");
			}
		}
		else if (resultData_MQTT.EventType == EventTypeEnum.ClientDisconnected)
		{
			base.Ready = false;
			logger.Debug("MQTT DisConnected");
		}
	}

	private CVStartCFC GetAction(CVMQTTRequest evt)
	{
		CVStartCFC result = null;
		string value = evt.EventName.ToUpper();
		if ("START".Equals(value))
		{
			result = new CVStartCFC(evt.SerialNumber);
		}
		else if ("STOP".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Stop, evt.SerialNumber);
		}
		else if ("PAUSE".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Pause, evt.SerialNumber);
		}
		else if ("FAIL".Equals(value))
		{
			result = new CVStartCFC(ActionTypeEnum.Fail, evt.SerialNumber);
		}
		return result;
	}

	public override void DoPublishStatus(string msg)
	{
		_ = PublishWhenReadyAsync(GetStatusTopic(), msg);
	}

	public override void DoPublish(MQActionEvent act)
	{
		if (act != null)
		{
			_ = PublishWhenReadyAsync(act.Topic, act.Message);
		}
	}

	public override void DoSubscribe(string topic, CVBaseServerNode serverNode)
	{
		base.DoSubscribe(topic, serverNode);
		QueueSubscriptionRestore();
	}

	public override void DoSubscribe(string topic, CVServiceProxy serverNodeProxy)
	{
		base.DoSubscribe(topic, serverNodeProxy);
		QueueSubscriptionRestore();
	}

	private void QueueSubscriptionRestore()
	{
		Ready = false;
		MQTTHelper mqttHelper = _MQTTHelper;
		if (mqttHelper != null
			&& mqttHelper.IsClientConnect()
			&& TryCaptureMqttSession(out long generation, out long topicVersion, out CancellationToken sessionToken))
		{
			_ = RestoreSubscriptionsForSessionAsync(mqttHelper, generation, topicVersion, sessionToken);
		}
	}

	private async Task RestoreSubscriptionsForSessionAsync(
		MQTTHelper mqttHelper,
		long generation,
		long topicVersion,
		CancellationToken sessionToken)
	{
		try
		{
			await RestoreSubscriptionsAsync(mqttHelper, generation, topicVersion, sessionToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			Ready = false;
		}
		catch (Exception ex)
		{
			Ready = false;
			logger.Warn("Failed to restore MQTT subscriptions.", ex);
		}
	}

	private async Task PublishWhenReadyAsync(string topic, string message)
	{
		if (!TryCaptureMqttSession(out long generation, out long topicVersion, out CancellationToken sessionToken))
		{
			logger.DebugFormat("MQTT publish skipped because the start-node session is inactive => topic={0}", topic);
			return;
		}

		bool publishLockTaken = false;
		try
		{
			using CancellationTokenSource timeoutSource = new CancellationTokenSource(PublishReadyTimeout);
			using CancellationTokenSource linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, sessionToken);
			CancellationToken linkedToken = linkedTokenSource.Token;
			await publishLock.WaitAsync(linkedToken).ConfigureAwait(false);
			publishLockTaken = true;
			ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
			if (!await EnsureReadyAsync(linkedToken).ConfigureAwait(false))
			{
				logger.ErrorFormat("MQTT publish skipped because the start node is not ready => topic={0}", topic);
				return;
			}
			ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
			MQTTHelper mqttHelper = _MQTTHelper;
			if (!HasCurrentMqttClient(mqttHelper))
			{
				logger.ErrorFormat("MQTT publish skipped because the active client is unavailable or changed => topic={0}", topic);
				return;
			}
			bool published = await mqttHelper.TryPublishAsync_Client(topic, message, retained: false, linkedToken).ConfigureAwait(false);
			ThrowIfMqttSessionChanged(generation, topicVersion, linkedToken);
			if (!published)
			{
				Ready = false;
				logger.ErrorFormat("MQTT publish failed => topic={0}", topic);
				return;
			}
		}
		catch (OperationCanceledException)
		{
			Ready = false;
			if (IsMqttSessionCurrent(generation, topicVersion))
			{
				logger.ErrorFormat("MQTT publish skipped after waiting {0}ms for readiness => topic={1}", PublishReadyTimeout.TotalMilliseconds, topic);
			}
			else
			{
				logger.DebugFormat("MQTT publish canceled because the start-node session ended => topic={0}", topic);
			}
		}
		catch (Exception ex)
		{
			Ready = false;
			logger.Error($"MQTT publish failed => topic={topic}", ex);
		}
		finally
		{
			if (publishLockTaken)
			{
				publishLock.Release();
			}
		}
	}

	internal bool HasCurrentMqttClient(MQTTHelper mqttHelper)
	{
		return mqttHelper != null
			&& ReferenceEquals(mqttHelper, _MQTTHelper)
			&& mqttHelper.IsClientConnect();
	}

	public override void Dispose()
	{
		isDisposed = true;
		EndMqttSession();
		base.Dispose();
	}
}
