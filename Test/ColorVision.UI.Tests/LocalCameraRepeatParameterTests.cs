using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Templates.CameraRunParam;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using Newtonsoft.Json.Linq;
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

    private sealed class CameraScope : IDisposable
    {
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;

        internal CameraScope()
        {
            var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
            device.Config = new ConfigCamera();
            device.SysResourceModel = new SysResourceModel { Code = "LOCAL.CAMERA.REPEAT.TEST" };
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [device];
            // Request preparation resolves this manager through GetInstance; avoid registering a test manager globally.
            typeof(ServiceManager).GetField("_copilotAgentExtensionRegistration", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, this);
            ManagerField.SetValue(null, manager);
        }

        public void Dispose() => ManagerField.SetValue(null, previousManager);
    }
}
