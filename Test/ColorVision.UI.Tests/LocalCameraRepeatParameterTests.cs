using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using ColorVision.Engine.Services.Results;
using ColorVision.FileIO;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using FlowEngineLib.Repeat;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class LocalCameraRepeatParameterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CameraDeclaresOnlyExposureAndAverageAndAutomaticExposureDisablesOnlyExposure(bool automatic)
    {
        StaTest.Run(() =>
        {
            var node = new LocalCameraNode { ExpTime = 350, AvgCount = 7, Gain = 2, IsAutoExp = automatic };
            var definitions = ((IRepeatParameterNode)node).GetRepeatParameters();
            Assert.Equal(new[] { nameof(LocalCameraNode.ExpTime), nameof(LocalCameraNode.AvgCount) }, definitions.Select(parameter => parameter.Key));
            var exposure = definitions[0];
            var average = definitions[1];
            Assert.Equal(350, exposure.DefaultValue);
            Assert.Equal("ms", exposure.Unit);
            Assert.Equal(7, average.DefaultValue);
            Assert.True(average.IsInteger);
            Assert.Equal(!automatic, exposure.TryValidate(125, out string error));
            if (automatic) Assert.Contains("自动曝光", error);
            Assert.True(average.TryValidate(2, out _));
        });
    }

    [Fact]
    public void AutomaticExposureAllowsAverageOnlyOverridesAndRejectsExposureOverridesBeforeRunning()
    {
        StaTest.Run(() =>
        {
            var node = new LocalCameraNode { IsAutoExp = true };
            node.Create();
            node.SetRepeatRegion(Guid.NewGuid().ToString(), 2, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]> { [nameof(LocalCameraNode.AvgCount)] = [1, 3] });
            Assert.True(FlowRepeatRegion.TryValidate([node], out string error), error);
            node.SetRepeatParameterValues(new Dictionary<string, double[]> { [nameof(LocalCameraNode.ExpTime)] = [100, 200] });
            Assert.False(FlowRepeatRegion.TryValidate([node], out error));
            Assert.Contains("自动曝光", error);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CameraRequestAndAllExposureChannelsUseThisRoundValuesWithoutChangingSavedParameters(bool repeated)
    {
        StaTest.Run(() =>
        {
            using var scope = new CameraScope();
            var node = new LocalCameraNode { ExpTime = 350, AvgCount = 7, Gain = 2, SaveFiles = false };
            node.Create();
            if (repeated) node.SetRepeatRegion(Guid.NewGuid().ToString(), 2, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(LocalCameraNode.ExpTime)] = [125, 250],
                [nameof(LocalCameraNode.AvgCount)] = [2, 3]
            });
            var action = new CVStartCFC("camera-repeat-parameters") { PersistResults = false };
            using var resources = action.RuntimeResources;
            JObject? payload = null;
            CameraRunParam? parameters = null;
            node.nodeRunEvent += (_, args) =>
            {
                try
                {
                    payload = JObject.Parse(args.SendPayload);
                    parameters = node.BuildCameraParameters(action);
                }
                finally
                {
                    // The shared runtime is stopped before native work can be scheduled.
                    resources.Dispose();
                }
            };
            node.GetAllInputOptions().Single().ReceiveData(action);
            Assert.NotNull(payload);
            Assert.True(payload.Value<bool>("AllowAcceleration"));
            Assert.NotNull(parameters);
            float expectedExposure = repeated ? 125 : 350;
            int expectedAverage = repeated ? 2 : 7;
            Assert.Equal(expectedExposure, parameters.ExpTime);
            Assert.Equal(expectedExposure, parameters.ExpTimeR);
            Assert.Equal(expectedExposure, parameters.ExpTimeG);
            Assert.Equal(expectedExposure, parameters.ExpTimeB);
            Assert.Equal(expectedAverage, parameters.AvgCount);
            Assert.Equal(2, parameters.Gain);
            Assert.Equal(expectedExposure, payload[nameof(LocalCameraNode.ExpTime)]!.Value<float>());
            Assert.Equal(expectedAverage, payload[nameof(LocalCameraNode.AvgCount)]!.Value<int>());
            Assert.Equal(2, payload[nameof(LocalCameraNode.Gain)]!.Value<float>());
            Assert.Equal(350, node.ExpTime);
            Assert.Equal(7, node.AvgCount);
            Assert.Equal(2, node.Gain);
            Assert.Equal(new double[] { 125, 250 }, node.RepeatParameterValues[nameof(LocalCameraNode.ExpTime)]);
            Assert.Equal(new double[] { 2, 3 }, node.RepeatParameterValues[nameof(LocalCameraNode.AvgCount)]);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StoppingDuringDedicatedCaptureDiscardsTheLateFrameBeforeSaving(bool saveFiles)
    {
        StaTest.Run(() =>
        {
            const string serialNumber = "stopped-local-camera";
            string tempParent = Path.GetFullPath(Path.GetTempPath());
            string output = Path.GetFullPath(Path.Combine(tempParent, $"cv-stopped-camera-{Guid.NewGuid():N}"));
            string outputPrefix = Path.TrimEndingDirectorySeparator(output) + Path.DirectorySeparatorChar;
            using var scope = new CameraScope();
            scope.Device.Config.FileServerCfg.DataBasePath = output;
            using var enteredCapture = new ManualResetEventSlim();
            using var releaseCapture = new ManualResetEventSlim();
            CancellationToken captureStop = default;
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
            {
                Width = 2, Height = 1, SourceBpp = 8, Channels = 1, Exposure = [100f]
            }, 2, 0);
            var node = new LocalCameraNode((request, autoConnect) =>
            {
                captureStop = request.Stop;
                enteredCapture.Set();
                releaseCapture.Wait();
                return new LocalCameraCaptureResult { Frame = frame };
            }) { SaveFiles = saveFiles };
            using var editor = new STNodeEditor();
            using var control = new FlowEngineControl(editor, false, new FlowNodeManager()) { PersistResults = false };
            var start = new RuntimeTestStartNode();
            var end = new CVEndNode();
            foreach (STNode graphNode in new STNode[] { start, node, end })
            {
                graphNode.Create();
                editor.Nodes.Add(graphNode);
            }
            Assert.Equal(ConnectionStatus.Connected, start.m_op_start.ConnectOption(node.GetAllInputOptions().Single()));
            Assert.Equal(ConnectionStatus.Connected, node.GetAllOutputOptions().Single().ConnectOption(end.m_in_start));
            var finished = new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
            control.Finished += (_, result) => finished.TrySetResult(result);
            int published = 0;
            using var subscription = ResultMessageBus.Default.Subscribe(message =>
            {
                if (message.SerialNumber == serialNumber) Interlocked.Increment(ref published);
            });

            bool previousCache = CVFileReadCache.IsEnabled;
            CVFileReadCache.IsEnabled = true;
            try
            {
                Assert.True(control.TryStartNode(start.NodeName, serialNumber));
                Assert.True(enteredCapture.Wait(TimeSpan.FromSeconds(10)));
                control.StopNode(start.NodeName, serialNumber);
                Assert.Equal(StatusTypeEnum.Canceled, finished.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult().Status);
                Assert.True(captureStop.IsCancellationRequested);
                releaseCapture.Set();
                Assert.True(SpinWait.SpinUntil(() =>
                {
                    try { using var lease = frame.Acquire(); return false; }
                    catch (ObjectDisposedException) { return true; }
                }, TimeSpan.FromSeconds(10)));

                Assert.False(Directory.Exists(output));
                Assert.DoesNotContain(CVFileReadCache.GetSnapshot().Entries, entry => entry.FilePath.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase));
                Assert.Empty(frame.CvRawFilePath);
                Assert.Equal(0, Volatile.Read(ref published));
            }
            finally
            {
                releaseCapture.Set();
                CVFileReadCache.Release();
                CVFileReadCache.IsEnabled = previousCache;
                Assert.StartsWith(Path.TrimEndingDirectorySeparator(tempParent) + Path.DirectorySeparatorChar, output, StringComparison.OrdinalIgnoreCase);
                if (Directory.Exists(output)) Directory.Delete(output, recursive: true);
            }
        });
    }

    private sealed class CameraScope : IDisposable
    {
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;
        internal DeviceCamera Device { get; }

        internal CameraScope()
        {
            Device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            Device.Config = new ConfigCamera();
            Device.SysResourceModel = new SysResourceModel { Code = "LOCAL.CAMERA.REPEAT.TEST" };
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [Device];
            // Request preparation resolves this manager through GetInstance; avoid registering a test manager globally.
            typeof(ServiceManager).GetField("_copilotAgentExtensionRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, this);
            ManagerField.SetValue(null, manager);
        }

        public void Dispose() => ManagerField.SetValue(null, previousManager);
    }
}
