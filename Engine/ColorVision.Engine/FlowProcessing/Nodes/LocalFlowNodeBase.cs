using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.PropertyEditor;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    public abstract class LocalFlowNodeBase : CVCommonNode
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalFlowNodeBase));

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
        private STNodeOption flowOutputOption = null!;
        protected string OperatorCode { get; }

        [Display(Order = -200)]
        [PropertyEditorType(typeof(FlowDeviceNameEditor))]
        [STNodeProperty("设备代码", "设备代码", false, false)]
        public new string DeviceCode
        {
            get => base.DeviceCode;
            set
            {
                base.DeviceCode = value;
                OnPropertyChanged();
            }
        }

        protected LocalFlowNodeBase(string title, string nodeType, string operatorName, params string[] inputNames)
            : base(title, nodeType, $"LOCAL.{nodeType}", $"LOCAL.{nodeType}")
        {
            OperatorCode = operatorName;
            this.inputNames = inputNames.Length == 0 ? new[] { "IN" } : inputNames.ToArray();
            if (this.inputNames.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("本地节点输入端口名称不能为空。", nameof(inputNames));
            if (this.inputNames.Distinct(StringComparer.Ordinal).Count() != this.inputNames.Length) throw new ArgumentException("本地节点输入端口名称不能重复。", nameof(inputNames));
            AutoSize = false;
            Width = StandardNodeWidth;
            Height = 85;
            if (this.inputNames.Length > 1)
            {
                int offset = 15 * (this.inputNames.Length - 1);
                Height += offset;
            }
        }

        protected override void OnCreate()
        {
            base.OnCreate();
            flowInputOptions = new STNodeOption[inputNames.Length];
            for (int index = 0; index < inputNames.Length; index++)
            {
                STNodeOption input = InputOptions.Add(inputNames[index], typeof(CVStartCFC), bSingle: true);
                input.DataTransfer += m_in_start_DataTransfer;
                flowInputOptions[index] = input;
            }
            flowOutputOption = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
        }

        protected void SelectFirstAvailableDevice<TDevice>() where TDevice : DeviceService
        {
            DeviceCode = GetFirstAvailableDeviceCode<TDevice>();
        }

        protected static string GetFirstAvailableDeviceCode<TDevice>() where TDevice : DeviceService
        {
            return ServiceManager.Current?.DeviceServices.OfType<TDevice>().FirstOrDefault()?.Code ?? string.Empty;
        }

        protected string ResolveAvailableDeviceCode<TDevice>() where TDevice : DeviceService
        {
            TDevice[] devices = ServiceManager.Current?.DeviceServices.OfType<TDevice>().ToArray() ?? Array.Empty<TDevice>();
            if (devices.Length == 0) return DeviceCode;
            return devices.Any(device => string.Equals(device.Code, DeviceCode, StringComparison.Ordinal))
                ? DeviceCode
                : devices[0].Code;
        }

        private void m_in_start_DataTransfer(object sender, STNodeOptionEventArgs e)
        {
            if (inputNames.Length > 1)
            {
                HandleMultiInput(sender as STNodeOption, e);
                return;
            }
            if (e.Status != ConnectionStatus.Connected || !HasData(e))
            {
                flowOutputOption.TransferData(e.TargetOption.Data);
                return;
            }
            if (e.TargetOption.Data is not CVStartCFC start)
            {
                flowOutputOption.TransferData(e.TargetOption.Data);
                return;
            }

            start.NormalizeStopStatus();
            if (!start.IsRunning)
            {
                flowOutputOption.TransferData(start);
                return;
            }

            BeginExecution(start, new[] { CaptureInput(start) });
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
                flowOutputOption.TransferData(null);
                return;
            }
            if (e.TargetOption.Data is not CVStartCFC start) return;

            start.NormalizeStopStatus();
            if (!start.IsRunning)
            {
                ClearPendingInputs(start.SerialNumber);
                flowOutputOption.TransferData(start);
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
                inputs[inputIndex] = CaptureInput(start);
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

        private static LocalFlowInputSnapshot CaptureInput(CVStartCFC start)
        {
            return LocalFlowInputSnapshot.Create(start);
        }

        private void BeginExecution(CVStartCFC start, LocalFlowInputSnapshot[] inputs)
        {
            CVTransAction transaction = new(start);
            activeInputSets.AddOrUpdate(start.SerialNumber, inputs, (_, _) => inputs);
            PublishNodeRun(new FlowEngineNodeRunEventArgs
            {
                SerialNumber = start.SerialNumber,
                SendTopic = LocalTopic,
                SendMsgId = start.SerialNumber,
                SendEventName = OperatorCode,
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
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, DeviceCode, EventName = OperatorCode, action.SerialNumber });
        }

        private void ExecuteCore(CVTransAction transaction)
        {
            try
            {
                LocalNodeExecutionResult result = ExecuteLocal(transaction.trans_action);
                CVServerResponse response = new(transaction.trans_action.SerialNumber, ActionStatusEnum.Finish, result.Message, OperatorCode, result.Data);
                transaction.trans_action.AddResult(GetLocalNodeName(), response, transaction.startTime);
                TransferEnd(transaction, response, 0);
            }
            catch (Exception ex)
            {
                CVStartCFC action = transaction.trans_action;
                action.Failed(ex.Message, GetLocalNodeName(), transaction.startTime, NodeID);
                CVServerResponse response = new(action.SerialNumber, ActionStatusEnum.Failed, ex.Message, OperatorCode, null);
                TransferEnd(transaction, response, -1);
            }
            finally
            {
                activeInputSets.TryRemove(transaction.trans_action.SerialNumber, out _);
            }
        }

        private void TransferEnd(CVTransAction transaction, CVServerResponse response, int statusCode)
        {
            PublishNodeEnd(new FlowEngineNodeEndEventArgs
            {
                SerialNumber = transaction.trans_action.SerialNumber,
                RecvTopic = LocalTopic,
                RecvMsgId = response.Id,
                RecvEventName = response.EventName,
                RecvStatusCode = statusCode,
                RecvStatusMessage = response.Message,
                RecvPayload = response.Data == null ? null : JsonConvert.SerializeObject(response.Data)
            });
            flowOutputOption.TransferData(transaction.trans_action);
        }

        private void PublishNodeRun(FlowEngineNodeRunEventArgs args)
        {
            foreach (FlowEngineNodeRunEvent handler in nodeRunEvent?.GetInvocationList().Cast<FlowEngineNodeRunEvent>() ?? Enumerable.Empty<FlowEngineNodeRunEvent>())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    log.Error($"[{ToShortString()}] local node-run subscriber failed", ex);
                }
            }
        }

        private void PublishNodeEnd(FlowEngineNodeEndEventArgs args)
        {
            foreach (FlowEngineNodeEndEvent handler in nodeEndEvent?.GetInvocationList().Cast<FlowEngineNodeEndEvent>() ?? Enumerable.Empty<FlowEngineNodeEndEvent>())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    log.Error($"[{ToShortString()}] local node-end subscriber failed", ex);
                }
            }
        }

        private string GetLocalNodeName() => $"{base.Title}.{NodeName}";
    }
}
