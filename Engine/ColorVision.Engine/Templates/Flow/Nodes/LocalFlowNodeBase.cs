using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow.Nodes
{
    public abstract class LocalFlowNodeBase : CVBaseServerNode
    {
        private sealed class LocalFlowInputSnapshot
        {
            public required CVStartCFC Action { get; init; }
            public int MasterId { get; init; } = -1;
            public int MasterResultType { get; init; } = -1;
            public string? MasterValue { get; init; }

            public static LocalFlowInputSnapshot Create(CVStartCFC action)
            {
                int masterId = ReadInt(action, "MasterId");
                int masterResultType = ReadInt(action, "MasterResultType");
                action.Data.TryGetValue("MasterValue", out object? masterValue);
                return new LocalFlowInputSnapshot
                {
                    Action = new CVStartCFC(action),
                    MasterId = masterId,
                    MasterResultType = masterResultType,
                    MasterValue = masterValue?.ToString()
                };
            }

            private static int ReadInt(CVStartCFC action, string key)
            {
                if (!action.Data.TryGetValue(key, out object? value) || value == null) return -1;
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    return -1;
                }
            }
        }

        protected sealed class LocalNodeExecutionResult
        {
            public string Message { get; init; } = "Finish";
            public object? Data { get; init; }
        }

        protected const string LocalTopic = "LOCAL";
        private readonly string[] inputNames;
        private readonly object inputSync = new();
        private readonly Dictionary<string, LocalFlowInputSnapshot?[]> pendingInputSets = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, LocalFlowInputSnapshot[]> activeInputSets = new(StringComparer.Ordinal);
        private STNodeOption[] flowInputOptions = Array.Empty<STNodeOption>();

        protected LocalFlowNodeBase(string title, string nodeType, string operatorName, int timeoutMs = 60000, params string[] inputNames)
            : base(title, nodeType, $"LOCAL.{nodeType}", $"LOCAL.{nodeType}")
        {
            operatorCode = operatorName;
            _MaxTime = timeoutMs;
            this.inputNames = inputNames.Length == 0 ? new[] { "IN" } : inputNames.ToArray();
            if (this.inputNames.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("本地节点输入端口名称不能为空。", nameof(inputNames));
            if (this.inputNames.Distinct(StringComparer.Ordinal).Count() != this.inputNames.Length) throw new ArgumentException("本地节点输入端口名称不能重复。", nameof(inputNames));
            m_in_text = this.inputNames[0];
            if (this.inputNames.Length > 1)
            {
                int offset = 15 * (this.inputNames.Length - 1);
                Height += offset;
                m_custom_item.Y += offset;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            flowInputOptions = new STNodeOption[inputNames.Length];
            flowInputOptions[0] = m_in_start;
            for (int index = 1; index < inputNames.Length; index++)
            {
                STNodeOption input = InputOptions.Add(inputNames[index], typeof(CVStartCFC), bSingle: true);
                input.Connected += m_in_op_Connected;
                input.DataTransfer += m_in_start_DataTransfer;
                flowInputOptions[index] = input;
            }
        }

        protected override void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (inputNames.Length > 1)
            {
                HandleMultiInput(sender as STNodeOption, e);
                return;
            }
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

            BeginExecution(start, new[] { CaptureInput(start, 0) });
        }

        protected bool TryGetInputMasterResult(CVStartCFC action, int inputIndex, out int masterId, out int masterResultType, out string? masterValue)
        {
            masterId = -1;
            masterResultType = -1;
            masterValue = null;
            if (!activeInputSets.TryGetValue(action.SerialNumber, out LocalFlowInputSnapshot[]? inputs)
                || inputIndex < 0
                || inputIndex >= inputs.Length)
            {
                return false;
            }

            LocalFlowInputSnapshot input = inputs[inputIndex];
            masterId = input.MasterId;
            masterResultType = input.MasterResultType;
            masterValue = input.MasterValue;
            return true;
        }

        private void HandleMultiInput(STNodeOption? sender, STNodeOptionEventArgs e)
        {
            if (sender == null || e.Status != ConnectionStatus.Connected) return;
            if (!HasData(e))
            {
                ClearPendingInputs();
                m_op_end.TransferData(null);
                return;
            }
            if (e.TargetOption.Data is not CVStartCFC start) return;

            start.NormalizeStopStatus();
            if (!start.IsRunning)
            {
                ClearPendingInputs(start.SerialNumber);
                m_op_end.TransferData(start);
                return;
            }

            int inputIndex = Array.IndexOf(flowInputOptions, sender);
            if (inputIndex < 0) throw new InvalidOperationException("无法识别本地节点输入端口。");
            LocalFlowInputSnapshot[]? readyInputs = null;
            lock (inputSync)
            {
                if (!pendingInputSets.TryGetValue(start.SerialNumber, out LocalFlowInputSnapshot?[]? inputs))
                {
                    inputs = new LocalFlowInputSnapshot?[inputNames.Length];
                    pendingInputSets.Add(start.SerialNumber, inputs);
                }
                inputs[inputIndex] = CaptureInput(start, inputIndex);
                if (inputs.All(input => input != null))
                {
                    readyInputs = inputs.Select(input => input!).ToArray();
                    pendingInputSets.Remove(start.SerialNumber);
                }
            }

            if (readyInputs != null)
            {
                BeginExecution(readyInputs[0].Action, readyInputs);
            }
        }

        private LocalFlowInputSnapshot CaptureInput(CVStartCFC start, int inputIndex)
        {
            LocalFlowInputSnapshot snapshot = LocalFlowInputSnapshot.Create(start);
            AlgorithmPreStepParam upstream = new();
            if (!getPreStepParam(inputIndex, upstream) || upstream.MasterId <= 0)
            {
                return snapshot;
            }
            return new LocalFlowInputSnapshot
            {
                Action = snapshot.Action,
                MasterId = upstream.MasterId,
                MasterResultType = upstream.MasterResultType,
                MasterValue = upstream.MasterValue
            };
        }

        private void BeginExecution(CVStartCFC start, LocalFlowInputSnapshot[] inputs)
        {
            CVTransAction transaction = new(start);
            m_trans_action.AddOrUpdate(start.SerialNumber, transaction, (_, _) => transaction);
            activeInputSets.AddOrUpdate(start.SerialNumber, inputs, (_, _) => inputs);
            nodeRunEvent?.Invoke(this, new FlowEngineNodeRunEventArgs
            {
                SendTopic = LocalTopic,
                SendMsgId = start.SerialNumber,
                SendEventName = operatorCode,
                SendPayload = BuildRunPayload(start)
            });

            _ = Task.Run(() => ExecuteCore(transaction));
        }

        private void ClearPendingInputs(string? serialNumber = null)
        {
            lock (inputSync)
            {
                if (serialNumber == null)
                {
                    pendingInputSets.Clear();
                }
                else
                {
                    pendingInputSets.Remove(serialNumber);
                }
            }
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
                activeInputSets.TryRemove(transaction.trans_action.SerialNumber, out _);
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
