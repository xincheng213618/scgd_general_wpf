#pragma warning disable CA1822,CA1859,CS8602
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using FlowEngineLib.Base;
using log4net;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ST.Library.UI.NodeEditor;

namespace ColorVision.Engine.FlowProcessing.Integration
{
    /// <summary>
    /// Captures the active Flow graph and exposes the loaded node catalog.
    /// </summary>
    public sealed class FlowCopilotContextService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowCopilotContextService));
        private readonly FlowEngineManager _manager;
        private string _nodeCatalogSignature = string.Empty;
        private IReadOnlyList<CopilotFlowNodeTypeContextSnapshot> _nodeCatalog = Array.Empty<CopilotFlowNodeTypeContextSnapshot>();

        private ViewFlow View => _manager.View;
        private FlowControl FlowControl => _manager.FlowControl;
        private FlowParam? SelectedFlowParam => _manager.SelectedFlowParam;
        private int TemplateFlowParamsIndex => _manager.TemplateFlowParamsIndex;
        private ObservableCollection<TemplateModel<FlowParam>> FlowParams => _manager.FlowParams;
        private MeasureBatchModel? Batch => _manager.Batch;
        private double BatchProgress => _manager.BatchProgress;
        private DisplayFlow DisplayFlow => _manager.DisplayFlow;

        public FlowCopilotContextService(FlowEngineManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public CopilotFlowContextSnapshot CaptureSnapshot()
        {
            var flowParam = SelectedFlowParam ?? (TemplateFlowParamsIndex >= 0 && TemplateFlowParamsIndex < FlowParams.Count ? FlowParams[TemplateFlowParamsIndex].Value : null);
            var nodes = BuildNodeSnapshots();
            var edges = BuildEdgeSnapshots();
            var batch = Batch;
            var recentRunMessage = TakeRecentFlowMessageTail(View?.logTextBox?.Text, 6000);
            var failureEvidence = ExtractRecentFlowFailureEvidence(recentRunMessage);
            var focusedNodes = nodes.Where(node => node.IsSelected || node.IsActive).ToArray();

            return new CopilotFlowContextSnapshot
            {
                SourceId = CopilotFlowAgentExtension.SourceId,
                Revision = ComputeFlowRevision(flowParam?.Id.ToString(), flowParam?.Name, View?.STNodeEditorMain?.Nodes.Cast<STNode>() ?? Enumerable.Empty<STNode>(), edges),
                FlowName = flowParam?.Name ?? string.Empty,
                TemplateName = flowParam?.Name ?? string.Empty,
                TemplateId = flowParam?.Id.ToString() ?? string.Empty,
                Status = FlowControl?.IsFlowRun == true ? "Running" : batch?.FlowStatus.ToString() ?? "Ready",
                IsRunning = FlowControl?.IsFlowRun == true,
                BatchSerialNumber = FirstNonEmpty(batch?.Code, batch?.Name),
                BatchStatus = batch?.FlowStatus.ToString() ?? string.Empty,
                BatchResult = batch?.Result ?? string.Empty,
                BatchProgress = $"{BatchProgress:0.##}%",
                LastNodeSummary = DisplayFlow?.LastNode?.ToShortString() ?? string.Empty,
                RecentRunMessage = recentRunMessage,
                RecentFailureSummary = string.Join(Environment.NewLine, failureEvidence),
                FocusedNodeSummary = string.Join(", ", focusedNodes.Select(node => FirstNonEmpty(node.Title, node.NodeName, node.NodeType, node.NodeId))),
                FailureEvidence = failureEvidence,
                Nodes = nodes,
                Edges = edges,
            };
        }

        public CopilotFlowNodeCatalogSnapshot CaptureNodeCatalog(string? query, int maxResults)
        {
            maxResults = Math.Clamp(maxResults, 1, 100);
            query = query?.Trim() ?? string.Empty;
            var catalog = GetCopilotFlowNodeCatalog();
            var matches = catalog
                .Where(nodeType => string.IsNullOrEmpty(query) || MatchesNodeType(nodeType, query))
                .OrderBy(nodeType => nodeType.CategoryPath, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(nodeType => nodeType.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(nodeType => nodeType.RuntimeType, StringComparer.Ordinal)
                .ToArray();

            return new CopilotFlowNodeCatalogSnapshot
            {
                Query = query,
                TotalMatches = matches.Length,
                IsTruncated = matches.Length > maxResults,
                NodeTypes = matches.Take(maxResults).ToArray(),
            };
        }

        private IReadOnlyList<CopilotFlowNodeTypeContextSnapshot> GetCopilotFlowNodeCatalog()
        {
            var types = (View?.STNodeEditorMain?.GetTypes() ?? Array.Empty<Type>())
                .Where(IsVisibleFlowNodeType)
                .ToArray();
            var signature = string.Join("\n", types.Select(GetNodeTypeKey).OrderBy(value => value, StringComparer.Ordinal));
            if (string.Equals(signature, _nodeCatalogSignature, StringComparison.Ordinal))
                return _nodeCatalog;

            var catalog = new List<CopilotFlowNodeTypeContextSnapshot>();
            foreach (var type in types)
            {
                try
                {
                    var attribute = type.GetCustomAttribute<STNodeAttribute>()!;
                    var node = Activator.CreateInstance(type) as STNode;
                    catalog.Add(new CopilotFlowNodeTypeContextSnapshot
                    {
                        TypeKey = GetNodeTypeKey(type),
                        RuntimeType = type.FullName ?? type.Name,
                        CategoryPath = attribute.Path ?? string.Empty,
                        Title = node?.Title ?? type.Name,
                        Description = attribute.DisplayDescription ?? string.Empty,
                        NodeType = node is CVCommonNode commonNode ? commonNode.NodeType ?? string.Empty : string.Empty,
                        DefaultDeviceCode = node is CVCommonNode commonNode1 ? commonNode1.DeviceCode ?? string.Empty : string.Empty,
                        Properties = BuildNodePropertySchemas(type),
                    });
                }
                catch (Exception ex)
                {
                    log.Debug($"Skip Copilot node catalog type {type.FullName}: {ex.Message}");
                }
            }

            _nodeCatalogSignature = signature;
            _nodeCatalog = catalog;
            return _nodeCatalog;
        }

        private static IReadOnlyList<CopilotFlowNodePropertySchemaSnapshot> BuildNodePropertySchemas(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Property: property, Attribute: property.GetCustomAttribute<STNodePropertyAttribute>()))
                .Where(item => item.Attribute != null && !item.Attribute.IsHide && item.Property.GetIndexParameters().Length == 0)
                .Select(item => new CopilotFlowNodePropertySchemaSnapshot
                {
                    PropertyName = item.Property.Name,
                    DisplayName = string.IsNullOrWhiteSpace(item.Attribute!.Name) ? item.Property.Name : item.Attribute.Name,
                    Description = item.Attribute.Description ?? string.Empty,
                    DataType = item.Property.PropertyType.FullName ?? item.Property.PropertyType.Name,
                    IsWritable = item.Property.SetMethod?.IsPublic == true && !item.Attribute.IsReadOnly,
                })
                .OrderBy(property => property.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static bool MatchesNodeType(CopilotFlowNodeTypeContextSnapshot nodeType, string query)
        {
            return new[]
            {
                nodeType.Title,
                nodeType.Description,
                nodeType.CategoryPath,
                nodeType.NodeType,
                nodeType.DefaultDeviceCode,
                nodeType.RuntimeType,
                nodeType.TypeKey,
            }.Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        internal static bool IsVisibleFlowNodeType(Type type)
        {
            return type != null
                && !type.IsAbstract
                && typeof(STNode).IsAssignableFrom(type)
                && !type.IsDefined(typeof(ObsoleteAttribute), inherit: false)
                && type.GetCustomAttribute<STNodeAttribute>() != null;
        }

        private IReadOnlyList<CopilotFlowNodeContextSnapshot> BuildNodeSnapshots()
        {
            var result = new List<CopilotFlowNodeContextSnapshot>();
            var nodeEditor = View?.STNodeEditorMain;
            if (nodeEditor?.Nodes == null)
                return result;

            foreach (STNode node in nodeEditor.Nodes)
                result.Add(BuildNodeSnapshot(node, ReferenceEquals(nodeEditor.ActiveNode, node)));

            return result;
        }

        internal static CopilotFlowNodeContextSnapshot BuildNodeSnapshot(STNode node, bool isActive)
        {
            var runtimeType = node.GetType();
            var instanceId = node.Guid.ToString();
            return new CopilotFlowNodeContextSnapshot
            {
                InstanceId = instanceId,
                TypeKey = GetNodeTypeKey(runtimeType),
                RuntimeType = runtimeType.FullName ?? runtimeType.Name,
                CategoryPath = runtimeType.GetCustomAttribute<STNodeAttribute>()?.Path ?? string.Empty,
                Title = node.Title ?? string.Empty,
                NodeName = node is CVCommonNode commonNode ? commonNode.NodeName ?? string.Empty : string.Empty,
                NodeType = node is CVCommonNode commonNode1 ? commonNode1.NodeType ?? string.Empty : runtimeType.Name,
                DeviceCode = node is CVCommonNode commonNode2 ? commonNode2.DeviceCode ?? string.Empty : string.Empty,
                NodeId = node is CVCommonNode commonNode3 ? commonNode3.NodeID ?? instanceId : instanceId,
                Position = $"Left={node.Left}, Top={node.Top}, Width={node.Width}, Height={node.Height}",
                Left = node.Left,
                Top = node.Top,
                Width = node.Width,
                Height = node.Height,
                Mark = node.Mark ?? string.Empty,
                IsActive = isActive,
                IsSelected = node.IsSelected,
                Inputs = DescribeOptions(node.GetAllInputOptions()),
                Outputs = DescribeOptions(node.GetAllOutputOptions()),
                InputPorts = BuildPortSnapshots(node.GetAllInputOptions(), "in"),
                OutputPorts = BuildPortSnapshots(node.GetAllOutputOptions(), "out"),
                Parameters = BuildNodeParameterSummary(node),
            };
        }

        private IReadOnlyList<CopilotFlowEdgeContextSnapshot> BuildEdgeSnapshots()
        {
            var editor = View?.STNodeEditorMain;
            if (editor == null)
                return Array.Empty<CopilotFlowEdgeContextSnapshot>();

            return editor.GetConnectionInfo()
                .Where(connection => connection.Output?.Owner != null && connection.Input?.Owner != null)
                .Select(connection =>
                {
                    var outputOptions = connection.Output.Owner.GetAllOutputOptions();
                    var inputOptions = connection.Input.Owner.GetAllInputOptions();
                    return new CopilotFlowEdgeContextSnapshot
                    {
                        SourceNodeId = connection.Output.Owner.Guid.ToString(),
                        SourcePortId = $"out:{Array.IndexOf(outputOptions, connection.Output)}",
                        SourcePortName = connection.Output.Text ?? string.Empty,
                        TargetNodeId = connection.Input.Owner.Guid.ToString(),
                        TargetPortId = $"in:{Array.IndexOf(inputOptions, connection.Input)}",
                        TargetPortName = connection.Input.Text ?? string.Empty,
                        DataType = connection.Output.DataType?.FullName ?? connection.Output.DataType?.Name ?? "System.Object",
                    };
                })
                .OrderBy(edge => edge.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.SourcePortId, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetPortId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<CopilotFlowPortContextSnapshot> BuildPortSnapshots(STNodeOption[]? options, string direction)
        {
            if (options == null || options.Length == 0)
                return Array.Empty<CopilotFlowPortContextSnapshot>();

            return options.Select((option, index) => new CopilotFlowPortContextSnapshot
            {
                PortId = $"{direction}:{index}",
                Name = option?.Text ?? string.Empty,
                DataType = option?.DataType?.FullName ?? option?.DataType?.Name ?? "System.Object",
                IsSingle = option?.IsSingle == true,
                ConnectionCount = option?.ConnectionCount ?? 0,
            }).ToArray();
        }

        internal static string GetNodeTypeKey(Type type)
        {
            return $"{type.Module.Name}|{type.Name}";
        }

        private static string ComputeFlowRevision(
            string? templateId,
            string? flowName,
            IEnumerable<STNode> nodes,
            IReadOnlyList<CopilotFlowEdgeContextSnapshot> edges)
        {
            var canonical = new StringBuilder();
            AppendCanonical(canonical, templateId);
            AppendCanonical(canonical, flowName);

            foreach (var node in nodes.OrderBy(node => node.Guid))
            {
                AppendCanonical(canonical, node.Guid.ToString());
                AppendCanonical(canonical, GetNodeTypeKey(node.GetType()));
                AppendCanonical(canonical, ComputeNodeStateHash(node));
            }

            foreach (var edge in edges)
            {
                AppendCanonical(canonical, edge.SourceNodeId);
                AppendCanonical(canonical, edge.SourcePortId);
                AppendCanonical(canonical, edge.TargetNodeId);
                AppendCanonical(canonical, edge.TargetPortId);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
        }

        private static string ComputeNodeStateHash(STNode node)
        {
            try
            {
                return Convert.ToHexString(SHA256.HashData(node.GetSaveData()));
            }
            catch
            {
                var fallback = $"{node.Guid}|{GetNodeTypeKey(node.GetType())}|{node.Left}|{node.Top}|{node.Width}|{node.Height}|{node.Mark}";
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fallback)));
            }
        }

        private static void AppendCanonical(StringBuilder builder, string? value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }

        private static IReadOnlyList<string> DescribeOptions(STNodeOption[]? options)
        {
            if (options == null || options.Length == 0)
                return Array.Empty<string>();

            return options
                .Where(option => option != null)
                .Select(option => $"{option.Text}({option.DataType?.Name ?? "object"}, connections {option.ConnectionCount})")
                .ToArray();
        }

        private static IReadOnlyList<string> ExtractRecentFlowFailureEvidence(string? recentRunMessage)
        {
            if (string.IsNullOrWhiteSpace(recentRunMessage))
                return Array.Empty<string>();

            var failureTerms = new[]
            {
                "fail", "failed", "failure", "error", "exception", "timeout",
                "失败", "错误", "异常", "超时",
            };

            var lines = recentRunMessage
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Reverse()
                .Where(line => failureTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Take(8)
                .Reverse()
                .ToArray();

            return lines;
        }

        private static string TakeRecentFlowMessageTail(string? message, int maxChars)
        {
            var text = message ?? string.Empty;
            if (text.Length <= maxChars)
                return text;
            return $"...<earlier flow messages omitted; kept the last {maxChars} characters.>{Environment.NewLine}" + text[^maxChars..];
        }

        private static IReadOnlyList<CopilotContextProperty> BuildNodeParameterSummary(STNode node)
        {
            var properties = new List<CopilotContextProperty>();
            foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                var nodeProperty = property.GetCustomAttribute<STNodePropertyAttribute>();
                if (nodeProperty == null || nodeProperty.IsHide)
                    continue;

                if (!IsSimpleType(property.PropertyType))
                    continue;

                try
                {
                    var value = property.GetValue(node);
                    properties.Add(new CopilotContextProperty
                    {
                        Name = string.IsNullOrWhiteSpace(nodeProperty.Name) ? property.Name : nodeProperty.Name,
                        Value = value?.ToString() ?? string.Empty,
                    });
                }
                catch
                {
                }
            }

            return properties;
        }

        private static bool IsSimpleType(Type type)
        {
            var source = Nullable.GetUnderlyingType(type) ?? type;
            return source.IsPrimitive
                || source.IsEnum
                || source == typeof(string)
                || source == typeof(decimal)
                || source == typeof(DateTime)
                || source == typeof(TimeSpan)
                || source == typeof(Guid);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }


    }
}

