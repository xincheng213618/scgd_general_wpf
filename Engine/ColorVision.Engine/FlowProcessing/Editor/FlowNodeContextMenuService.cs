using ColorVision.Engine.MQTT;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Editor
{
    internal sealed class FlowNodeContextMenuService : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowNodeContextMenuService));
        private static readonly string[] CoreNodeMenuAssemblyPrefixes =
        {
            "FlowEngineLib/",
            "ColorVision.Engine/",
        };

        private static STNodeTreeView? _nodeTreeView;
        private readonly STNodeEditor _nodeEditor;
        private readonly FlowExecutionNavigator _executionNavigator;
        private readonly ContextMenu _contextMenu;
        private System.Drawing.Point _contextCanvasPoint;

        private static STNodeTreeView NodeTreeView => _nodeTreeView ??= new STNodeTreeView();

        public FlowNodeContextMenuService(STNodeEditor nodeEditor, FlowExecutionNavigator executionNavigator)
        {
            _nodeEditor = nodeEditor;
            _executionNavigator = executionNavigator;
            _contextMenu = new ContextMenu();
            _nodeEditor.ContextMenu = _contextMenu;
            _nodeEditor.ContextMenuOpening += NodeEditor_ContextMenuOpening;
        }

        internal static string LocalizeNodeMenuPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string displayPath = path;
            foreach (string prefix in CoreNodeMenuAssemblyPrefixes)
            {
                if (!displayPath.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                displayPath = displayPath.Substring(prefix.Length);
                break;
            }

            return string.Join("/", displayPath.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(LocalizeNodeMenuText));
        }

        private static string LocalizeNodeMenuText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            string? localized = ST.Library.UI.Lang.Get(text);
            if (IsValidLocalizedMenuText(text, localized))
                return localized!;

            localized = ST.Library.UI.Properties.Resources.ResourceManager.GetString(text, CultureInfo.CurrentUICulture);
            return IsValidLocalizedMenuText(text, localized) ? localized! : text;
        }

        private static bool IsValidLocalizedMenuText(string key, string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && !string.Equals(value, $"[{key}]", StringComparison.Ordinal);
        }

        private void NodeEditor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var mousePosition = Mouse.GetPosition(_nodeEditor);
            var clientPoint = new System.Drawing.Point(
                (int)Math.Round(mousePosition.X),
                (int)Math.Round(mousePosition.Y));
            _contextCanvasPoint = _nodeEditor.ControlToCanvas(clientPoint);
            NodeFindInfo findInfo = _nodeEditor.FindNodeFromPoint(_contextCanvasPoint);
            _contextMenu.Items.Clear();

            if (findInfo.NodeOption != null)
            {
                e.Handled = true;
                return;
            }

            if (findInfo.Node != null)
            {
                AddNodeMenuItems(_contextMenu.Items, findInfo.Node);
            }
            else
            {
                AddNodeCreationMenuItems(_contextMenu.Items);
                AddImportModuleContextMenu(_contextMenu.Items);
            }

            if (_contextMenu.Items.Count == 0)
                e.Handled = true;
        }

        private void AddNodeMenuItems(ItemCollection items, STNode node)
        {
            var copyItem = new MenuItem { Header = Properties.Resources.Copy };
            copyItem.Click += (_, _) => CopyNode(node);
            items.Add(copyItem);

            var deleteItem = new MenuItem { Header = Properties.Resources.Delete };
            deleteItem.Click += (_, _) => _nodeEditor.Nodes.Remove(node);
            items.Add(deleteItem);

            if (node is CVCommonNode commonNode)
            {
                var historyItem = new MenuItem { Header = Properties.Resources.Flow_NodeExecutionDetails };
                historyItem.Click += (_, _) => _executionNavigator.OpenNodeExecutionDetails(commonNode);
                items.Add(historyItem);
            }

            if (node is LocalCalibrationNodeBase calibrationNode)
            {
                var releaseCalibrationCacheItem = new MenuItem { Header = "释放本地校正缓存" };
                releaseCalibrationCacheItem.Click += async (_, _) =>
                {
                    DeviceCamera? device = ServiceManager.GetInstance().DeviceServices
                        .OfType<DeviceCamera>()
                        .FirstOrDefault(camera => string.Equals(camera.Code, calibrationNode.DeviceCode, StringComparison.Ordinal));
                    if (device == null)
                    {
                        MessageBox.Show($"找不到本地相机设备：{calibrationNode.DeviceCode}", "ColorVision");
                        return;
                    }
                    await device.ReleaseLocalCalibrationCacheAsync();
                };
                items.Add(releaseCalibrationCacheItem);
            }

            items.Add(new Separator());

            var lockOptionItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockOption)),
                IsCheckable = true,
                IsChecked = node.LockOption
            };
            lockOptionItem.Click += (_, _) => _nodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockOption)),
                () => node.LockOption = !node.LockOption);
            items.Add(lockOptionItem);

            var lockLocationItem = new MenuItem
            {
                Header = LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                IsCheckable = true,
                IsChecked = node.LockLocation
            };
            lockLocationItem.Click += (_, _) => _nodeEditor.ExecuteEditTransaction(
                LocalizeNodeMenuText(nameof(STNode.LockLocation)),
                () => node.LockLocation = !node.LockLocation);
            items.Add(lockLocationItem);
        }

        private void CopyNode(STNode node)
        {
            byte[] data = _nodeEditor.GetNodesData(new[] { node });
            _nodeEditor.ImportSelectionData(data, new System.Drawing.Point(node.Left + 30, node.Top + 30));
        }

        private void AddNodeCreationMenuItems(ItemCollection items)
        {
            NodeTreeView.LoadAssembly();
            var groups = NodeTreeView.NodeTypes
                .Where(item => item.Key.IsSubclassOf(typeof(STNode))
                    && !item.Key.IsAbstract
                    && !item.Key.IsDefined(typeof(ObsoleteAttribute), inherit: false))
                .GroupBy(item => item.Value)
                .OrderBy(group => group.Key, Comparer<string>.Create(
                    (x, y) => Common.NativeMethods.Shlwapi.CompareLogical(x, y)));

            foreach (var group in groups)
            {
                var categoryItem = new MenuItem { Header = LocalizeNodeMenuPath(group.Key) };
                foreach (var entry in group.OrderBy(item => item.Key.Name, StringComparer.CurrentCulture))
                {
                    STNode? previewNode;
                    try
                    {
                        previewNode = Activator.CreateInstance(entry.Key) as STNode;
                    }
                    catch
                    {
                        continue;
                    }
                    if (previewNode == null)
                        continue;

                    Type nodeType = entry.Key;
                    var nodeItem = new MenuItem { Header = LocalizeNodeMenuText(previewNode.Title) };
                    nodeItem.Click += (_, _) => CreateNode(nodeType);
                    categoryItem.Items.Add(nodeItem);
                }
                if (categoryItem.Items.Count > 0)
                    items.Add(categoryItem);
            }
        }

        private void CreateNode(Type type)
        {
            if (Activator.CreateInstance(type) is not STNode node)
                return;

            node.Create();
            node.Left = _contextCanvasPoint.X;
            node.Top = _contextCanvasPoint.Y;

            if (node is CVBaseServerNode serverNode)
            {
                var matchedService = MqttRCService.GetInstance().ServiceTokens
                    .FirstOrDefault(service => service.Devices.Any(device => device.Key == serverNode.DeviceCode));
                if (matchedService != null)
                    serverNode.Token = matchedService.Token;
            }
            else if (node is MQTTStartNode startNode)
            {
                startNode.Server = MQTTControl.Config.Host;
                startNode.Port = MQTTControl.Config.Port;
            }

            _nodeEditor.Nodes.Add(node);
            _nodeEditor.SetActiveNode(node);
        }

        private void AddImportModuleContextMenu(ItemCollection items)
        {
            items.Add(new Separator());
            var importModuleItem = new MenuItem { Header = Properties.Resources.Flow_ImportTemplateAsModule };
            importModuleItem.SubmenuOpened += (_, _) =>
            {
                importModuleItem.Items.Clear();
                foreach (var template in TemplateFlow.Params)
                {
                    string name = template.Key;
                    FlowParam param = template.Value;
                    var templateItem = new MenuItem { Header = name };
                    templateItem.Click += (_, _) =>
                    {
                        if (string.IsNullOrEmpty(param.DataBase64))
                            return;

                        try
                        {
                            FlowEditorOperations.ImportCanvasAsModule(
                                _nodeEditor,
                                Convert.FromBase64String(param.DataBase64));
                        }
                        catch (Exception ex)
                        {
                            log.Error($"Import module '{name}' failed", ex);
                        }
                    };
                    importModuleItem.Items.Add(templateItem);
                }

                if (importModuleItem.Items.Count == 0)
                {
                    importModuleItem.Items.Add(new MenuItem
                    {
                        Header = Properties.Resources.Flow_NoTemplateAvailable,
                        IsEnabled = false
                    });
                }
            };
            items.Add(importModuleItem);
        }

        public void Dispose()
        {
            _nodeEditor.ContextMenuOpening -= NodeEditor_ContextMenuOpening;
            if (ReferenceEquals(_nodeEditor.ContextMenu, _contextMenu))
                _nodeEditor.ContextMenu = null;
        }
    }
}
