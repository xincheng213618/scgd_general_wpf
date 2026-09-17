using ColorVision.Engine.FlowProcessing.Diagnostics;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ST.Library.UI;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing.Nodes
{
    public abstract class LocalFlowNodeBase : CVCommonNode
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LocalFlowNodeBase));
        private static readonly IReadOnlyDictionary<string, string> LegacyDefaultTitles = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["本地关注点布点(Re)"] = "关注点布点(Re)",
            ["本地关注点布点(参数)"] = "关注点布点(参数)",
            ["本地校正"] = "校正",
            ["本地校正+实时 POI"] = "校正+实时 POI",
            ["本地相机取图"] = "相机取图",
            ["本地十字定位"] = "十字定位",
            ["本地发光区定位(V2)"] = "发光区定位",
            ["本地FOV计算(V2)"] = "FOV计算",
            ["本地点阵畸变(V2)"] = "点阵畸变",
            ["本地 POI"] = "POI",
            ["Local POI Layout (Remap)"] = "关注点布点(Re)",
            ["Local POI Layout (Parameters)"] = "关注点布点(参数)",
            ["Local Calibration"] = "校正",
            ["Local Calibration + Real-time POI"] = "校正+实时 POI",
            ["Local Camera Capture"] = "相机取图",
            ["Local POI"] = "POI"
        };

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

        protected LocalFlowNodeBase(string title, string nodeType, string operatorName, params string[] inputNames)
            : base(title, nodeType, $"LOCAL.{nodeType}")
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

        public override void OnLoadNode(Dictionary<string, byte[]> dic)
        {
            base.OnLoadNode(dic);
            if (LegacyDefaultTitles.TryGetValue(Title, out string? currentTitle))
            {
                Title = Lang.Get(currentTitle);
            }
            else if (Title is "本地图片" or "Local Image")
            {
                Title = Properties.Resources.Engine_PG_LocalImage;
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

        protected static string CompactValueOrDash(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        protected static string FormatCompactRegion(Int32Rect region) =>
            region.IsEmpty || region.Width <= 0 || region.Height <= 0
                ? Lang.Get("全图")
                : $"{region.Width}×{region.Height}";

        protected virtual IReadOnlyList<string> GetCompactSummaryLines() => [GetCompactSummaryValue()];

        protected override void DrawCompactSummary(DrawingTools dt, string label, string value)
        {
            string[] lines = GetCompactSummaryLines()
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(2)
                .ToArray();
            if (lines.Length == 0) return;

            const int horizontalPadding = 14;
            System.Drawing.Rectangle rectangle = new(
                Left + horizontalPadding,
                Top + TitleHeight,
                Math.Max(0, Width - horizontalPadding * 2),
                CompactSummaryHeight * lines.Length);
            System.Drawing.Graphics graphics = dt.Graphics;
            GraphicsState state = graphics.Save();
            System.Drawing.StringAlignment alignment = m_sf.Alignment;
            System.Drawing.StringAlignment lineAlignment = m_sf.LineAlignment;
            System.Drawing.StringFormatFlags formatFlags = m_sf.FormatFlags;
            System.Drawing.StringTrimming trimming = m_sf.Trimming;
            try
            {
                graphics.SetClip(rectangle, CombineMode.Intersect);
                graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                dt.SolidBrush.Color = ForeColor;
                m_sf.Alignment = System.Drawing.StringAlignment.Center;
                m_sf.LineAlignment = System.Drawing.StringAlignment.Center;
                m_sf.FormatFlags |= System.Drawing.StringFormatFlags.NoWrap;
                m_sf.Trimming = System.Drawing.StringTrimming.EllipsisCharacter;
                for (int index = 0; index < lines.Length; index++)
                {
                    System.Drawing.Rectangle lineRectangle = new(
                        rectangle.X,
                        rectangle.Y + CompactSummaryHeight * index,
                        rectangle.Width,
                        CompactSummaryHeight);
                    graphics.DrawString(lines[index], Font, dt.SolidBrush, lineRectangle, m_sf);
                }
            }
            finally
            {
                m_sf.Alignment = alignment;
                m_sf.LineAlignment = lineAlignment;
                m_sf.FormatFlags = formatFlags;
                m_sf.Trimming = trimming;
                graphics.Restore(state);
            }
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

            LocalFlowInputSnapshot input = CaptureInput(start);
            BeginExecution(input.Action, new[] { input });
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
            return JsonConvert.SerializeObject(new { ServiceName = NodeName, EventName = OperatorCode, action.SerialNumber });
        }

        private void ExecuteCore(CVTransAction transaction)
        {
            var timing = new FlowNodeTiming();
            using var activation = timing.Activate();
            try
            {
                LocalNodeExecutionResult result = FlowNodeTiming.Run("ExecuteLocal", () => ExecuteLocal(transaction.trans_action));
                if (transaction.trans_action.RuntimeResources.IsDisposed) return;
                CVServerResponse response = new(transaction.trans_action.SerialNumber, ActionStatusEnum.Finish, result.Message, OperatorCode, result.Data);
                transaction.trans_action.AddResult(GetLocalNodeName(), response, transaction.startTime);
                TransferEnd(transaction, response, 0, timing);
            }
            catch (Exception ex)
            {
                CVStartCFC action = transaction.trans_action;
                if (action.RuntimeResources.IsDisposed) return;
                action.Failed(ex.Message, GetLocalNodeName(), transaction.startTime, NodeID);
                CVServerResponse response = new(action.SerialNumber, ActionStatusEnum.Failed, ex.Message, OperatorCode, null);
                TransferEnd(transaction, response, -1, timing);
            }
            finally
            {
                activeInputSets.TryRemove(transaction.trans_action.SerialNumber, out _);
            }
        }

        private void TransferEnd(CVTransAction transaction, CVServerResponse response, int statusCode, FlowNodeTiming timing)
        {
            PublishNodeEnd(new FlowEngineNodeEndEventArgs
            {
                SerialNumber = transaction.trans_action.SerialNumber,
                RecvTopic = LocalTopic,
                RecvMsgId = response.Id,
                RecvEventName = response.EventName,
                RecvStatusCode = statusCode,
                RecvStatusMessage = response.Message,
                RecvPayload = timing.SerializePayload(response.Data)
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
