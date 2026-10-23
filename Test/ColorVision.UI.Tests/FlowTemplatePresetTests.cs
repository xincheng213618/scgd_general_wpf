using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.Themes;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Start;
using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplatePresetTests
{
    [Theory]
    [InlineData("flow-camera-capture", 3)]
    [InlineData("flow-camera-poi", 4)]
    [InlineData("flow-luminance-uniformity", 5)]
    [InlineData("flow-color-uniformity", 5)]
    [InlineData("flow-checkerboard-contrast", 5)]
    [InlineData("flow-camera-mtf", 4)]
    [InlineData("flow-image-mtf", 4)]
    [InlineData("flow-image-luminous-area", 4)]
    [InlineData("flow-sensor-command", 3)]
    public void EveryShippedFlowReopensWithConnectedNodesAndNoFieldDeviceBindings(string id, int expectedCount)
    {
        WpfTestHost.Invoke(() =>
        {
            var preset = Assert.IsType<FlowTemplatePreset>(FlowTemplatePresets.Find(id));
            var draft = FlowTemplatePresets.CreateDraft(preset);
            Assert.Equal(-1, draft.Id);
            Assert.Null(draft.FlowKey);
            Assert.Null(draft.ResourceId);
            using var editor = new STNodeEditor();
            editor.LoadCanvas(Convert.FromBase64String(draft.DataBase64));
            Assert.Equal(expectedCount, editor.Nodes.Count);
            Assert.Equal(expectedCount - 1, editor.GetConnectionInfo().Length);
            Assert.IsType<CVEndNode>(editor.Nodes[expectedCount - 1]);
            var start = Assert.IsAssignableFrom<BaseStartNode>(editor.Nodes[0]);
            if (preset.Start == "mqtt")
            {
                Assert.Empty(Assert.IsType<MQTTStartNode>(start).Server);
                Assert.True(start.RequiresConnectionReady);
            }
            else
            {
                Assert.IsType<ManualFlowStartNode>(start);
                Assert.False(start.RequiresConnectionReady);
                Assert.True(start.IsExecutionReady);
            }
            Assert.All(editor.Nodes.OfType<IFlowDeviceNode>(), node => Assert.Empty(node.DeviceCode));
            foreach (STNode node in editor.Nodes)
            {
                if (node is LocalCameraNode camera) { Assert.Empty(camera.CalibTempName); Assert.Equal(15.67f, camera.ExpTime); }
                if (node is TestMessageBoxNode image) Assert.Empty(image.ImageFileUrl);
                if (node is LocalPoiNode poi) Assert.Empty(poi.POITempName);
                if (node is LocalSensorNode sensor) Assert.Empty(sensor.TemplateName);
                if (node is LocalFindLuminousAreaNode luminous) Assert.Empty(luminous.SavePOITempName);
                if (node is LocalMtfNode mtf) Assert.Equal(StripeMtfPattern.FourPart, StripeMtfParameters.FromJson(mtf.ParameterJson).Pattern);
            }
            byte[] png = FlowTemplateCoverService.Render(Convert.FromBase64String(draft.DataBase64));
            Assert.Equal(new byte[] { 137, 80, 78, 71 }, png.Take(4));
            Assert.NotEmpty(preset.Setup);
        });
    }

    [Fact]
    public void SelectingAndCancellingNeverCreatesAFlowAndCreatingTwiceHasIndependentIdentities()
    {
        WpfTestHost.Invoke(() =>
        {
            string path = Path.Combine(Path.GetTempPath(), "ColorVision-flow-preset-tests", Guid.NewGuid() + ".db");
            var store = new LocalTemplateStore(path);
            var storage = new LocalFlowTemplateStorage(store);
            var old = TemplateFlow.Params.ToArray();
            TemplateFlow.Params.Clear();
            try
            {
                var template = new TemplateFlow(storage, () => false, () => throw new InvalidOperationException("Preset creation must not open MySQL."));
                Assert.Equal(9, template.GetCreatePresets().Count());
                Assert.True(template.ApplyCreatePreset("flow-camera-capture"));
                var preview = Assert.IsType<FlowParam>(template.CreateDefault());
                Assert.Equal(template.ImportTemp!.DataBase64, preview.DataBase64);
                Assert.False(File.Exists(path));
                template.ClearCreateTemplateSource();
                Assert.Null(template.ImportTemp);
                Assert.Empty(TemplateFlow.Params);
                Assert.False(File.Exists(path));
                Assert.True(template.ApplyCreatePreset("flow-camera-capture"));
                template.Create("流程一");
                Assert.True(template.ApplyCreatePreset("flow-camera-capture"));
                template.Create("流程二");
                var flows = storage.Load();
                Assert.Equal(2, flows.Count);
                Assert.NotEqual(flows[0].FlowKey, flows[1].FlowKey);
                Assert.NotEqual(flows[0].Id, flows[1].Id);
                Assert.All(flows, flow => Assert.False(string.IsNullOrEmpty(flow.DataBase64)));
                Assert.Empty(store.List("poi"));
                Assert.Empty(store.List("mod-details"));
            }
            finally
            {
                TemplateFlow.Params.Clear();
                foreach (var item in old) TemplateFlow.Params.Add(item);
                File.Delete(path);
            }
        });
    }

    [Fact]
    public void NewFlowWindowShowsBusinessSourcesGraphPreviewAndSetupChecklist()
    {
        WpfTestHost.Invoke(() =>
        {
            var old = Application.Current.Resources;
            try
            {
                var resources = new ResourceDictionary();
                foreach (var uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) });
                Application.Current.Resources = resources;
                var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".db");
                var template = new TemplateFlow(new LocalFlowTemplateStorage(new LocalTemplateStore(path)), () => false);
                var view = new TemplateCreateView();
                view.Initialize(template, new TemplateCreateOptions { InitialSourceKind = TemplateCreateSourceKind.Preset });
                var host = (Grid)view.FindName("CustomPreviewHost");
                var preview = Assert.IsType<FlowTemplateCreatePreview>(Assert.Single(host.Children.Cast<UIElement>()));
                Assert.NotNull(((Image)preview.FindName("CanvasImage")).Source);
                Assert.NotEmpty(((ItemsControl)preview.FindName("SetupItems")).Items);
                Assert.Equal(Visibility.Visible, ((Border)view.FindName("PresetGuide")).Visibility);
                Assert.False(File.Exists(path));
                view.Discard();
                Assert.Null(template.ImportTemp);
            }
            finally { Application.Current.Resources = old; }
        });
    }

    [Fact]
    public void ManualStartCompletesALocalGraphWithoutMqttReadiness() => StaTest.Run(() =>
    {
        using var canvas = new CVNodeContainer();
        var start = new ManualFlowStartNode { PersistResults = false };
        var end = new CVEndNode();
        foreach (STNode node in new STNode[] { start, end }) { node.Create(); canvas.Nodes.Add(node); }
        start.m_op_start.ConnectOption(end.m_in_start, false);
        using var control = new FlowEngineControl(canvas, false, new FlowNodeManager()) { PersistResults = false };
        var completed = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        control.Finished += (_, result) => completed.TrySetResult(result);
        Assert.True(control.TryStartNode(start.NodeName, "preset-manual-contract"));
        Assert.Equal(StatusTypeEnum.Completed, completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().Status);
    });
}
