using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.FileServer;
using ColorVision.Engine.Services.Devices.FlowDevice;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms;
using ColorVision.Engine.Services.Types;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices
{
    public sealed class DeviceServiceCreateContext
    {
        public DeviceServiceCreateContext(string code, string name, string sendTopic, string subscribeTopic)
        {
            Code = code ?? string.Empty;
            Name = name ?? string.Empty;
            SendTopic = sendTopic ?? string.Empty;
            SubscribeTopic = subscribeTopic ?? string.Empty;
        }

        public string Code { get; }

        public string Name { get; }

        public string SendTopic { get; }

        public string SubscribeTopic { get; }
    }

    public interface IDeviceServiceFactory
    {
        ServiceTypes ServiceType { get; }

        string? TerminalIconResourceKey { get; }

        DeviceServiceConfig CreateConfig(DeviceServiceCreateContext context);

        DeviceService CreateService(SysResourceModel sysResourceModel);
    }

    public sealed class DeviceServiceFactory<TConfig> : IDeviceServiceFactory where TConfig : DeviceServiceConfig, new()
    {
        private readonly Func<SysResourceModel, DeviceService> createService;
        private readonly Action<TConfig, DeviceServiceCreateContext>? configureConfig;

        public DeviceServiceFactory(
            ServiceTypes serviceType,
            Func<SysResourceModel, DeviceService> createService,
            string? terminalIconResourceKey = null,
            Action<TConfig, DeviceServiceCreateContext>? configureConfig = null)
        {
            ServiceType = serviceType;
            this.createService = createService ?? throw new ArgumentNullException(nameof(createService));
            TerminalIconResourceKey = terminalIconResourceKey;
            this.configureConfig = configureConfig;
        }

        public ServiceTypes ServiceType { get; }

        public string? TerminalIconResourceKey { get; }

        public DeviceServiceConfig CreateConfig(DeviceServiceCreateContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            TConfig config = new()
            {
                Id = context.Code,
                Code = context.Code,
                Name = context.Name,
                SendTopic = context.SendTopic,
                SubscribeTopic = context.SubscribeTopic,
            };

            configureConfig?.Invoke(config, context);
            return config;
        }

        public DeviceService CreateService(SysResourceModel sysResourceModel)
        {
            ArgumentNullException.ThrowIfNull(sysResourceModel);
            return createService(sysResourceModel);
        }
    }

    public static class DeviceServiceFactoryRegistry
    {
        private static readonly object SyncRoot = new();
        private static readonly Dictionary<ServiceTypes, IDeviceServiceFactory> Factories = new();

        static DeviceServiceFactoryRegistry()
        {
            RegisterDefaults();
        }

        public static void Register(IDeviceServiceFactory factory, bool replace = false)
        {
            ArgumentNullException.ThrowIfNull(factory);

            lock (SyncRoot)
            {
                if (!replace && Factories.ContainsKey(factory.ServiceType))
                {
                    throw new InvalidOperationException($"Device service factory already registered: {factory.ServiceType}");
                }

                Factories[factory.ServiceType] = factory;
            }
        }

        public static bool TryGetFactory(ServiceTypes serviceType, out IDeviceServiceFactory? factory)
        {
            lock (SyncRoot)
            {
                return Factories.TryGetValue(serviceType, out factory);
            }
        }

        public static DeviceService? CreateService(SysResourceModel sysResourceModel)
        {
            ArgumentNullException.ThrowIfNull(sysResourceModel);

            ServiceTypes serviceType = (ServiceTypes)sysResourceModel.Type;
            return TryGetFactory(serviceType, out IDeviceServiceFactory? factory) && factory != null
                ? factory.CreateService(sysResourceModel)
                : null;
        }

        private static void RegisterDefaults()
        {
            Register(new DeviceServiceFactory<ConfigCamera>(
                ServiceTypes.Camera,
                sysResourceModel => new DeviceCamera(sysResourceModel)));

            Register(new DeviceServiceFactory<ConfigPG>(
                ServiceTypes.PG,
                sysResourceModel => new DevicePG(sysResourceModel)));

            Register(new DeviceServiceFactory<ConfigSpectrum>(
                ServiceTypes.Spectrum,
                sysResourceModel => new DeviceSpectrum(sysResourceModel),
                "DISpectrumIcon"));

            Register(new DeviceServiceFactory<ConfigSMU>(
                ServiceTypes.SMU,
                sysResourceModel => new DeviceSMU(sysResourceModel),
                "SMUDrawingImage"));

            Register(new DeviceServiceFactory<ConfigSensor>(
                ServiceTypes.Sensor,
                sysResourceModel => new DeviceSensor(sysResourceModel)));

            Register(new DeviceServiceFactory<ConfigFileServer>(
                ServiceTypes.FileServer,
                sysResourceModel => new DeviceFileServer(sysResourceModel),
                configureConfig: (config, _) =>
                {
                    int fromPort = Random.Shared.Next(6500, 6599);
                    config.Endpoint = "127.0.0.1";
                    config.PortRange = $"{fromPort}-{fromPort + 5}";
                    config.FileBasePath = "D:\\CVTest";
                }));

            Register(new DeviceServiceFactory<ConfigAlgorithm>(
                ServiceTypes.Algorithm,
                sysResourceModel => new DeviceAlgorithm(sysResourceModel),
                "DrawingImageAlgorithm",
                (config, _) => config.IsCCTWave = true));

            Register(new DeviceServiceFactory<ConfigCfwPort>(
                ServiceTypes.FilterWheel,
                sysResourceModel => new DeviceCfwPort(sysResourceModel),
                "CfwPortDrawingImage"));

            Register(new DeviceServiceFactory<ConfigCalibration>(
                ServiceTypes.Calibration,
                sysResourceModel => new DeviceCalibration(sysResourceModel),
                "DICalibrationIcon"));

            Register(new DeviceServiceFactory<ConfigMotor>(
                ServiceTypes.Motor,
                sysResourceModel => new DeviceMotor(sysResourceModel),
                "COMDrawingImage"));

            Register(new DeviceServiceFactory<ConfigThirdPartyAlgorithms>(
                ServiceTypes.ThirdPartyAlgorithms,
                sysResourceModel => new DeviceThirdPartyAlgorithms(sysResourceModel)));

            Register(new DeviceServiceFactory<ConfigFlowDevice>(
                ServiceTypes.Flow,
                sysResourceModel => new DeviceFlowDevice(sysResourceModel)));
        }
    }
}