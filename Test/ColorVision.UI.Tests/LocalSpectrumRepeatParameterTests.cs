using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using FlowEngineLib.Base;
using FlowEngineLib.Repeat;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class LocalSpectrumRepeatParameterTests
{
    [Theory]
    [InlineData(false, false, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, false)]
    public void AutomaticAndSynchronousModesExposeOnlyMeaningfulOverrides(bool automatic, bool synchronous, bool integralAllowed, bool averageAllowed)
    {
        StaTest.Run(() =>
        {
            using var scope = new SpectrumScope(synchronous);
            var node = new LocalSpectrumNode { IntegralTime = 250, NumberOfAverage = 5, AutoIntegration = automatic };
            var definitions = ((IRepeatParameterNode)node).GetRepeatParameters().ToDictionary(definition => definition.Key);
            Assert.Equal(2, definitions.Count);
            var integral = definitions[nameof(LocalSpectrumNode.IntegralTime)];
            var average = definitions[nameof(LocalSpectrumNode.NumberOfAverage)];
            Assert.Equal(250, integral.DefaultValue);
            Assert.Equal("ms", integral.Unit);
            Assert.Equal(5, average.DefaultValue);
            Assert.True(average.IsInteger);
            Assert.Equal(integralAllowed, integral.TryValidate(250, out string integralError));
            Assert.Equal(averageAllowed, average.TryValidate(5, out string averageError));
            if (!integralAllowed) Assert.Contains(synchronous ? "同步频率" : "自动积分", integralError);
            if (!averageAllowed) Assert.Contains("同步频率", averageError);
        });
    }

    [Theory]
    [InlineData(nameof(LocalSpectrumNode.IntegralTime), 0)]
    [InlineData(nameof(LocalSpectrumNode.IntegralTime), 0.0001)]
    [InlineData(nameof(LocalSpectrumNode.IntegralTime), 2147483.647)]
    [InlineData(nameof(LocalSpectrumNode.IntegralTime), double.NaN)]
    [InlineData(nameof(LocalSpectrumNode.IntegralTime), double.PositiveInfinity)]
    [InlineData(nameof(LocalSpectrumNode.NumberOfAverage), 0)]
    [InlineData(nameof(LocalSpectrumNode.NumberOfAverage), 1.5)]
    [InlineData(nameof(LocalSpectrumNode.NumberOfAverage), 2147483648)]
    public void NativeNumericBoundariesRejectInvalidRepeatValues(string key, double value)
    {
        StaTest.Run(() =>
        {
            using var scope = new SpectrumScope();
            var definition = new LocalSpectrumNode().GetRepeatParameters().Single(parameter => parameter.Key == key);
            Assert.False(definition.TryValidate(value, out string error));
            Assert.NotEmpty(error);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutionPayloadUsesThisRoundValuesWithoutChangingSavedNodeParameters(bool repeated)
    {
        StaTest.Run(() =>
        {
            using var scope = new SpectrumScope();
            var node = new LocalSpectrumNode { IntegralTime = 350, NumberOfAverage = 7 };
            node.Create();
            if (repeated) node.SetRepeatRegion(Guid.NewGuid().ToString(), 2, 1);
            node.SetRepeatParameterValues(new Dictionary<string, double[]>
            {
                [nameof(LocalSpectrumNode.IntegralTime)] = [125, 250],
                [nameof(LocalSpectrumNode.NumberOfAverage)] = [2, 3]
            });
            var action = new CVStartCFC("spectrum-repeat-parameters") { PersistResults = false };
            using var resources = action.RuntimeResources;
            JObject? payload = null;
            node.nodeRunEvent += (_, args) =>
            {
                payload = JObject.Parse(args.SendPayload);
                // Stop after request preparation, before any native work can be scheduled.
                resources.Dispose();
            };
            node.GetAllInputOptions().Single().ReceiveData(action);
            Assert.NotNull(payload);
            Assert.Equal(repeated ? 125 : 350, payload[nameof(LocalSpectrumNode.IntegralTime)]!.Value<float>());
            Assert.Equal(repeated ? 2 : 7, payload[nameof(LocalSpectrumNode.NumberOfAverage)]!.Value<int>());
            Assert.Equal(350, node.IntegralTime);
            Assert.Equal(7, node.NumberOfAverage);
            Assert.Equal(new double[] { 125, 250 }, node.RepeatParameterValues[nameof(LocalSpectrumNode.IntegralTime)]);
            Assert.Equal(new double[] { 2, 3 }, node.RepeatParameterValues[nameof(LocalSpectrumNode.NumberOfAverage)]);
        });
    }

    private sealed class SpectrumScope : IDisposable
    {
        private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly ServiceManager? previousManager = ServiceManager.Current;

        internal SpectrumScope(bool synchronous = false)
        {
            var device = (DeviceSpectrum)RuntimeHelpers.GetUninitializedObject(typeof(DeviceSpectrum));
            device.Config = new ConfigSpectrum();
            device.Config.GetDataConfig.IsSyncFrequencyEnabled = synchronous;
            device.SysResourceModel = new SysResourceModel { Code = "LOCAL.SPECTRUM.REPEAT.TEST" };
            var manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
            manager.DeviceServices = [device];
            ManagerField.SetValue(null, manager);
        }

        public void Dispose() => ManagerField.SetValue(null, previousManager);
    }
}
