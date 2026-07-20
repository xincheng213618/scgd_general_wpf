using FlowEngineLib;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    public abstract class LocalFlowNodeBase : CVBaseServerNode
    {
        protected sealed class LocalNodeExecutionResult
        {
            public string Message { get; init; } = "Finish";
            public object? Data { get; init; }
        }

        protected const string LocalTopic = "LOCAL";

        protected LocalFlowNodeBase(string title, string nodeType, string operatorName, int timeoutMs = 60000)
            : base(title, nodeType, $"LOCAL.{nodeType}", $"LOCAL.{nodeType}")
        {
            operatorCode = operatorName;
            _MaxTime = timeoutMs;
        }

        protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (e.Status != ConnectionStatus.Connected || !HasData(e))
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }
            if (e.TargetOption.Data is not CVStartCFC start)
            {
                m_op_end.TransferData(e.TargetOption.Data);
                return;
            }

            start.NormalizeStopStatus();
            if (!start.IsRunning)
            {
                m_op_end.TransferData(start);
                return;
            }

            CVTransAction transaction = new(start);
            m_trans_action.AddOrUpdate(start.SerialNumber, transaction, (_, _) => transaction);
            nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs
            {
                SendTopic = LocalTopic,
                SendMsgId = start.SerialNumber,
                SendEventName = operatorCode,
                SendPayload = BuildRunPayload(start)
            });

            _ = Task.Run(() => ExecuteCore(transaction));
        }

        protected abstract LocalNodeExecutionResult ExecuteLocal(CVStartCFC action);

        protected virtual string BuildRunPayload(CVStartCFC action)
        {
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, DeviceCode, EventName = operatorCode, action.SerialNumber });
        }

        private void ExecuteCore(CVTransAction transaction)
        {
            try
            {
                LocalNodeExecutionResult result = ExecuteLocal(transaction.trans_action);
                CVServerResponse response = new(transaction.trans_action.SerialNumber, ActionStatusEnum.Finish, result.Message, operatorCode, result.Data);
                svrRecvResp = response;
                transaction.trans_action.AddResult(GetLocalNodeName(), response, transaction.startTime);
                TransferEnd(transaction, response, 0);
            }
            catch (Exception ex)
            {
                CVStartCFC action = transaction.trans_action;
                action.Failed(ex.Message, GetLocalNodeName(), transaction.startTime);
                CVServerResponse response = new(action.SerialNumber, ActionStatusEnum.Failed, ex.Message, operatorCode, null);
                svrRecvResp = response;
                TransferEnd(transaction, response, -1);
            }
            finally
            {
                m_trans_action.TryRemove(transaction.trans_action.SerialNumber, out _);
            }
        }

        private void TransferEnd(CVTransAction transaction, CVServerResponse response, int statusCode)
        {
            nodeEndEvent?.Invoke(this, new FlowEngineNodeEndEventArgs
            {
                RecvTopic = LocalTopic,
                RecvMsgId = response.Id,
                RecvEventName = response.EventName,
                RecvStatusCode = statusCode,
                RecvStatusMessage = response.Message,
                RecvPayload = response.Data == null ? null : JsonConvert.SerializeObject(response.Data)
            });
            m_op_end.TransferData(transaction.trans_action);
        }

        private string GetLocalNodeName() => $"{base.Title}.{NodeName}";
    }
}
