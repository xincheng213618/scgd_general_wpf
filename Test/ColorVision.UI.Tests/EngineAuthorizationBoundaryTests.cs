using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using ColorVision.Engine.Services.PhyCameras.Calibration.Editing;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.UI.Authorizations;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

[Collection(AuthorizationStateTestGroup.CollectionName)]
public sealed class EngineAuthorizationBoundaryTests
{
    [Fact]
    public void ServiceCreationCommandWaitsForItsModelBeforeEvaluatingTheResourcePolicy()
    {
        WpfTestHost.Invoke(() =>
        {
            IAccessPolicy previous = AccessControl.Policy;
            var policy = new RecordingPolicy(request => new(request.ResourceId == "service-type:42", "resource_policy"));
            try
            {
                AccessControl.Policy = policy;
                var type = new TypeService();
                Assert.False(type.OpenCreateWindowCommand.CanExecute(null));
                Assert.Empty(policy.Requests);

                type.SysDictionaryModel = new SysDictionaryModel { Id = 42, Value = (int)ServiceTypes.Camera };
                Assert.True(type.OpenCreateWindowCommand.CanExecute(null));
                AccessRequest request = Assert.Single(policy.Requests);
                Assert.Equal("service.create", request.ActionId);
                Assert.Equal("service-type:42", request.ResourceId);
            }
            finally { AccessControl.Policy = previous; }
        });
    }

    [Fact]
    public void RevokedConfigurationPermissionCancelsBeforeCopyingTheDraftOrSaving()
    {
        WpfTestHost.Invoke(() =>
        {
            Authorization previous = Authorization.Instance;
            List<ResourceDictionary> dictionaries = [];
            PropertyEditorWindow? editor = null;
            try
            {
                Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    ResourceDictionary dictionary = new() { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                using var device = new OfflineDevice();
                editor = new PropertyEditorWindow(device.Config, PropertyEditorEditMode.Transactional);
                device.ProtectConfigurationEditor(editor);
                int submitted = 0;
                editor.Submitted += (_, _) => { submitted++; device.CommitConfiguration(); };
                ((DeviceServiceConfig)editor.EditConfig).Name = "Changed";
                Authorization.Instance.PermissionMode = PermissionMode.User;
                ((Button)editor.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("Original", device.Config.Name);
                Assert.Equal(0, device.Saves);
                Assert.Equal(0, submitted);
                Assert.Equal(Visibility.Visible, ((TextBlock)editor.FindName("SubmissionError")).Visibility);

                Authorization.Instance.PermissionMode = PermissionMode.Administrator;
                ((Button)editor.FindName("ConfirmButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("Changed", device.Config.Name);
                Assert.Equal(1, device.Saves);
                Assert.Equal(1, submitted);
            }
            finally
            {
                editor?.Close();
                foreach (ResourceDictionary dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
                Authorization.Instance = previous;
            }
        });
    }

    [Fact]
    public void ResourcePolicyDenialProtectsTheBusinessCommitBeforeAnyPreparation()
    {
        WpfTestHost.Invoke(() =>
        {
            IAccessPolicy previous = AccessControl.Policy;
            var policy = new RecordingPolicy();
            try
            {
                AccessControl.Policy = policy;
                using var device = new OfflineDevice();
                int prepared = 0;
                Assert.False(device.CanConfigure);
                Assert.Throws<AccessDeniedException>(() => device.CommitConfiguration(() => prepared++));
                Assert.Equal(0, prepared);
                Assert.Equal(0, device.Saves);
                Assert.All(policy.Requests, request =>
                {
                    Assert.Equal("device.configure", request.ActionId);
                    Assert.Equal("DEV.Authorization.Test", request.ResourceId);
                });
            }
            finally { AccessControl.Policy = previous; }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExperimentalModeSkipsApplicationLicenseExpiryChecks(bool enabled)
    {
        IConfigService previous = ConfigService.Instance;
        try
        {
            ConfigService.SetInstance(new MemoryConfiguration());
            ExperimentalFeaturesConfig.Instance.IsEnabled = enabled;
            bool skipValidation = ExperimentalFeaturesConfig.Instance.IsEnabled;
            DateTime now = new(2026, 10, 28, 12, 0, 0);
            foreach (DateTime? expiry in new DateTime?[] { null, now.AddSeconds(-1), now })
            {
                if (enabled)
                {
                    LocalCameraLicense.EnsureAvailable(expiry, skipValidation, now);
                    LocalSpectrumLicense.EnsureNotExpired(expiry, skipValidation, now);
                }
                else
                {
                    Assert.Throws<LocalCameraLicenseException>(() => LocalCameraLicense.EnsureAvailable(expiry, skipValidation, now));
                    Assert.Throws<LocalSpectrumLicenseException>(() => LocalSpectrumLicense.EnsureNotExpired(expiry, skipValidation, now));
                }
            }
            LocalCameraLicense.EnsureAvailable(now.AddSeconds(1), skipValidation, now);
            LocalSpectrumLicense.EnsureNotExpired(now.AddSeconds(1), skipValidation, now);
        }
        finally { ConfigService.SetInstance(previous); }
    }

    [Fact]
    public void RevocationAfterOpeningCalibrationFilesWritesNoReplacementBackupOrNewFile()
    {
        Authorization previous = Authorization.Instance;
        string directory = Path.Combine(Path.GetTempPath(), "ColorVisionAuthorizationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Authorization.Instance = new Authorization { PermissionMode = PermissionMode.Administrator };
            string json = Path.Combine(directory, "source.json"), binary = Path.Combine(directory, "source.dat");
            File.WriteAllText(json, "{\"a\":1}");
            byte[] original = [0, 0, 0, 0, 77];
            File.WriteAllBytes(binary, original);
            var jsonSnapshot = CalibrationJsonFileStore.Load(json);
            var binarySnapshot = CalibrationBinaryFileStore.Load(binary);

            Authorization.Instance.PermissionMode = PermissionMode.User;
            Assert.Throws<AccessDeniedException>(() => CalibrationJsonFileStore.Save(jsonSnapshot, "{\"a\":2}"));
            Assert.Throws<AccessDeniedException>(() => CalibrationJsonFileStore.SaveAs(Path.Combine(directory, "copy.json"), "{}", jsonSnapshot));
            Assert.Throws<AccessDeniedException>(() => CalibrationBinaryFileStore.Save(binarySnapshot, [0, 0, 0, 0, 99]));
            Assert.Throws<AccessDeniedException>(() => CalibrationBinaryFileStore.SaveAs(Path.Combine(directory, "copy.dat"), original, binarySnapshot));
            Assert.Equal("{\"a\":1}", File.ReadAllText(json));
            Assert.Equal(original, File.ReadAllBytes(binary));
            Assert.Equal(2, Directory.GetFiles(directory).Length);
        }
        finally { Authorization.Instance = previous; Directory.Delete(directory, true); }
    }

    private sealed class OfflineDevice : DeviceService<DeviceServiceConfig>
    {
        internal int Saves { get; private set; }
        internal OfflineDevice() : base(new SysResourceModel { Id = -2, Code = "DEV.Authorization.Test", Name = "Original", Value = "{}" }) { }
        public override void Save() => Saves++;
    }

    private sealed class RecordingPolicy(Func<AccessRequest, AccessDecision>? evaluate = null) : IAccessPolicy
    {
        internal List<AccessRequest> Requests { get; } = [];
        public AccessDecision Evaluate(AccessRequest request) { Requests.Add(request); return evaluate?.Invoke(request) ?? new(false, "resource_denied"); }
    }

    private sealed class MemoryConfiguration : IConfigService
    {
        private readonly Dictionary<Type, IConfig> values = [];
        public IConfig GetRequiredService(Type type) => values.TryGetValue(type, out var value) ? value : values[type] = (IConfig)Activator.CreateInstance(type)!;
        public T GetRequiredService<T>() where T : IConfig => (T)GetRequiredService(typeof(T));
        public void SaveConfigs() { }
        public void LoadConfigs() { }
        public void Save<T>() where T : IConfig { }
    }
}
