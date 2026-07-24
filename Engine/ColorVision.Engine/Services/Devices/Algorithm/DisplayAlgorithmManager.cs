using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine
{
    public sealed class DisplayAlgorithmMeta
    {
        public Type Type { get; init; } = null!;
        public int Order { get; init; }
        public string Name { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Group { get; init; } = string.Empty;
    }

    public sealed class DisplayAlgorithmManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayAlgorithmManager));
        private static readonly Lazy<DisplayAlgorithmManager> _instance = new(() => new DisplayAlgorithmManager());
        private readonly Dictionary<Type, DisplayAlgorithmMeta> _algorithmMetaByType;

        public static DisplayAlgorithmManager GetInstance() => _instance.Value;

        public IReadOnlyList<DisplayAlgorithmMeta> AlgorithmMetas { get; }

        private DisplayAlgorithmManager()
        {
            List<DisplayAlgorithmMeta> algorithmMetas = new();

            foreach (Assembly assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (!typeof(IDisplayAlgorithm).IsAssignableFrom(type) || type.IsAbstract)
                    {
                        continue;
                    }

                    DisplayAlgorithmAttribute? attribute = type.GetCustomAttribute<DisplayAlgorithmAttribute>();
                    if (attribute == null)
                    {
                        continue;
                    }

                    algorithmMetas.Add(new DisplayAlgorithmMeta
                    {
                        Type = type,
                        Order = attribute.Order,
                        Name = attribute.Name,
                        DisplayName = attribute.DisplayName,
                        Group = attribute.Group
                    });
                }
            }

            AlgorithmMetas = algorithmMetas
                .OrderBy(meta => meta.Order)
                .ThenBy(meta => meta.DisplayName, StringComparer.CurrentCulture)
                .ToArray();
            _algorithmMetaByType = AlgorithmMetas.ToDictionary(meta => meta.Type);
        }

        public IDisplayAlgorithm CreateAlgorithm(Type algorithmType, DeviceAlgorithm device, string? imageFilePath = null)
        {
            ArgumentNullException.ThrowIfNull(algorithmType);
            ArgumentNullException.ThrowIfNull(device);

            if (!_algorithmMetaByType.ContainsKey(algorithmType))
            {
                throw new ArgumentException($"Unknown display algorithm type: {algorithmType.FullName}", nameof(algorithmType));
            }

            if (Activator.CreateInstance(algorithmType, device) is not IDisplayAlgorithm algorithm)
            {
                throw new InvalidOperationException($"Could not create display algorithm {algorithmType.FullName}.");
            }

            algorithm.ImageFilePath = imageFilePath ?? string.Empty;
            return algorithm;
        }

        public UserControl CreateView(IDisplayAlgorithm algorithm)
        {
            ArgumentNullException.ThrowIfNull(algorithm);

            Type algorithmType = algorithm.GetType();
            if (!_algorithmMetaByType.ContainsKey(algorithmType))
            {
                throw new ArgumentException($"Unknown display algorithm type: {algorithmType.FullName}", nameof(algorithm));
            }

            return new DisplayAlgorithmControl(algorithm);
        }

        public void SetType(DisplayAlgorithmParam param)
        {
            OpenWindow(param);
        }

        public bool OpenWindow(DisplayAlgorithmParam param, Window? owner = null)
        {
            ArgumentNullException.ThrowIfNull(param);

            Application? application = Application.Current;
            if (application == null)
            {
                log.Warn("Display algorithm window cannot be opened because the WPF application is unavailable.");
                return false;
            }

            if (!application.Dispatcher.CheckAccess())
            {
                return application.Dispatcher.Invoke(() => OpenWindow(param, owner));
            }

            if (param.Type == null || !_algorithmMetaByType.TryGetValue(param.Type, out DisplayAlgorithmMeta? meta))
            {
                log.Warn($"Display algorithm type is not registered: {param.Type?.FullName ?? "<null>"}.");
                return false;
            }

            Window? resolvedOwner = owner?.IsLoaded == true ? owner : application.GetActiveWindow();
            List<DeviceAlgorithm> devices = ServiceManager.GetInstance().DeviceServices
                .OfType<DeviceAlgorithm>()
                .OrderBy(device => device.Config.Name, StringComparer.CurrentCulture)
                .ThenBy(device => device.Code, StringComparer.Ordinal)
                .ToList();
            if (devices.Count == 0)
            {
                MessageBox1.Show(
                    resolvedOwner,
                    Properties.Resources.NoAlgorithmServiceAvailable,
                    "ColorVision");
                return false;
            }

            DisplayAlgorithmWindow window = new(meta, devices, param.ImageFilePath);
            if (resolvedOwner?.IsLoaded == true)
            {
                window.Owner = resolvedOwner;
            }
            window.Show();
            return true;
        }
    }
}
