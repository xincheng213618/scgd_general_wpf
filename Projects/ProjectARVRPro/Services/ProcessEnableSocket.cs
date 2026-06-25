using ColorVision.SocketProtocol;
using log4net;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using System.Net.Sockets;
using System.Windows;

namespace ProjectARVRPro.Services
{
    public class GetProcessEnableSocket : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GetProcessEnableSocket));
        public string EventName => "GetProcessEnable";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            try
            {
                var data = RunOnUiThread(BuildProcessEnableData);
                return CreateResponse(request, 0, "OK", data);
            }
            catch (Exception ex)
            {
                log.Error("GetProcessEnable异常", ex);
                return CreateResponse(request, -99, ex.Message, null);
            }
        }

        internal static ProcessEnableData BuildProcessEnableData()
        {
            var processManager = ProcessManager.GetInstance();
            int indexOffset = GetIndexOffset();
            var items = processManager.ProcessMetas.Select((meta, index) => new ProcessEnableItemData
            {
                Index = index + indexOffset,
                Name = meta.Name,
                FlowTemplate = meta.FlowTemplate,
                ProcessTypeName = meta.ProcessTypeName,
                IsEnabled = meta.IsEnabled
            }).ToList();

            return new ProcessEnableData
            {
                ActiveGroupName = processManager.ActiveGroup?.Name ?? string.Empty,
                Count = items.Count,
                Items = items
            };
        }

        internal static int GetIndexOffset()
        {
            return ViewResultManager.GetInstance().Config.UseLegacyARVROutput ? 1 : 0;
        }

        internal static SocketResponse CreateResponse(SocketRequest request, int code, string msg, object? data)
        {
            return new SocketResponse
            {
                Version = request.Version,
                MsgID = request.MsgID,
                EventName = request.EventName,
                SerialNumber = request.SerialNumber,
                Code = code,
                Msg = msg,
                Data = data
            };
        }

        internal static T RunOnUiThread<T>(Func<T> func)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return func();

            return dispatcher.Invoke(func);
        }
    }

    public class SetProcessEnableSocket : ISocketJsonHandler
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SetProcessEnableSocket));
        public string EventName => "SetProcessEnable";

        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            SocketControl.Current.Stream = stream;
            try
            {
                if (!TryParseItems(request.Params, out var requestItems, out string errorMessage))
                    return GetProcessEnableSocket.CreateResponse(request, -1, errorMessage, null);

                var data = GetProcessEnableSocket.RunOnUiThread(() => ApplyItems(requestItems));
                int code = data.NotFound.Count == 0 ? 0 : 1;
                string msg = data.NotFound.Count == 0 ? "OK" : "Partial applied";
                return GetProcessEnableSocket.CreateResponse(request, code, msg, data);
            }
            catch (Exception ex)
            {
                log.Error("SetProcessEnable异常", ex);
                return GetProcessEnableSocket.CreateResponse(request, -99, ex.Message, null);
            }
        }

        private static ProcessEnableSetResult ApplyItems(List<ProcessEnableSetItem> requestItems)
        {
            var processManager = ProcessManager.GetInstance();
            int indexOffset = GetProcessEnableSocket.GetIndexOffset();
            var applied = new List<ProcessEnableItemData>();
            var notFound = new List<int>();

            foreach (var item in requestItems)
            {
                int processIndex = item.Index - indexOffset;
                if (processIndex < 0 || processIndex >= processManager.ProcessMetas.Count)
                {
                    notFound.Add(item.Index);
                    continue;
                }

                ProcessMeta meta = processManager.ProcessMetas[processIndex];
                meta.IsEnabled = item.IsEnabled;
                applied.Add(new ProcessEnableItemData
                {
                    Index = item.Index,
                    Name = meta.Name,
                    FlowTemplate = meta.FlowTemplate,
                    ProcessTypeName = meta.ProcessTypeName,
                    IsEnabled = meta.IsEnabled
                });
            }

            processManager.SaveProcessGroups();
            return new ProcessEnableSetResult
            {
                ActiveGroupName = processManager.ActiveGroup?.Name ?? string.Empty,
                Applied = applied,
                NotFound = notFound
            };
        }

        private static bool TryParseItems(string? value, out List<ProcessEnableSetItem> items, out string errorMessage)
        {
            items = new List<ProcessEnableSetItem>();
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                errorMessage = "Params is empty";
                return false;
            }

            JToken root;
            try
            {
                root = JToken.Parse(value);
            }
            catch (Exception ex)
            {
                errorMessage = $"Params json parse failed: {ex.Message}";
                return false;
            }

            JToken? itemsToken = root.Type == JTokenType.Array ? root : root["Items"];
            if (itemsToken == null && root.Type == JTokenType.Object)
                itemsToken = new JArray(root);
            if (itemsToken is not JArray array)
            {
                errorMessage = "Params.Items is required";
                return false;
            }

            foreach (JToken token in array)
            {
                if (token.Type != JTokenType.Object)
                {
                    errorMessage = "Each item must be an object";
                    return false;
                }

                int? index = token.Value<int?>("Index");
                bool? isEnabled = token.Value<bool?>("IsEnabled") ?? token.Value<bool?>("Enabled");
                if (index == null)
                {
                    errorMessage = "Item.Index is required";
                    return false;
                }
                if (isEnabled == null)
                {
                    errorMessage = "Item.IsEnabled is required";
                    return false;
                }

                items.Add(new ProcessEnableSetItem { Index = index.Value, IsEnabled = isEnabled.Value });
            }

            if (items.Count == 0)
            {
                errorMessage = "Items is empty";
                return false;
            }

            return true;
        }
    }

    public class ProcessEnableData
    {
        public string ActiveGroupName { get; set; } = string.Empty;
        public int Count { get; set; }
        public List<ProcessEnableItemData> Items { get; set; } = new();
    }

    public class ProcessEnableSetResult
    {
        public string ActiveGroupName { get; set; } = string.Empty;
        public List<ProcessEnableItemData> Applied { get; set; } = new();
        public List<int> NotFound { get; set; } = new();
    }

    public class ProcessEnableItemData
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FlowTemplate { get; set; } = string.Empty;
        public string ProcessTypeName { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    internal sealed class ProcessEnableSetItem
    {
        public int Index { get; set; }
        public bool IsEnabled { get; set; }
    }
}
