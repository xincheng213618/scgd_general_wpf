using ColorVision.UI.HotKey;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class BuiltInShortcutUpgradeTests
{
    private static readonly Type[] BuiltInTypes =
    [
        typeof(LogImp.MenuLogWindow),
        typeof(ColorVision.Solution.MenuOpenSolution),
        typeof(ColorVision.Solution.Workspace.MenuResetLayout)
    ];

    public static IEnumerable<object[]> SavedOverrideCases =>
        from type in BuiltInTypes
        from cleared in new[] { false, true }
        from legacyNameOnly in new[] { false, true }
        select new object[] { type, cleared, legacyNameOnly };

    public static IEnumerable<object[]> ResetCases =>
        from type in BuiltInTypes
        from cleared in new[] { false, true }
        select new object[] { type, cleared };

    [Fact]
    public void NoSavedOverrideUsesTheNewDefaultsWithoutWritingConfiguration()
    {
        WpfTestHost.Invoke(() =>
        {
            using Fixture fixture = new();
            fixture.Load();

            Assert.Equal(3, fixture.Service.HotKeys.Count);
            foreach (Type type in BuiltInTypes)
            {
                HotKeys runtime = fixture.Entry(type);
                Assert.Single(runtime.GetBindings());
                Assert.Equal(ReadDeclaration(type).GetDefaultBindings(), runtime.GetBindings());
                Assert.Equal(runtime.GetDefaultBindings(), runtime.GetBindings());
                Assert.Equal(HotKeyKinds.Windows, runtime.Kinds);
                Assert.Equal(runtime.GetBindings(), fixture.Handle(runtime).Bindings);
            }
            Assert.Empty(fixture.Config.Hotkeys);
            Assert.Equal(3, fixture.RegisterCalls);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [MemberData(nameof(SavedOverrideCases))]
    public void CustomOrExplicitlyEmptyOverrideSurvivesNewDefaultsReloadAndRediscovery(Type type, bool cleared, bool legacyNameOnly)
    {
        WpfTestHost.Invoke(() =>
        {
            HotKeys declaration = ReadDeclaration(type);
            HotkeySetting saved = new()
            {
                Id = legacyNameOnly ? string.Empty : ActionId(type, declaration),
                LegacyName = legacyNameOnly ? declaration.Name : string.Empty,
                Kinds = HotKeyKinds.Global
            };
            saved.SetBindings(cleared ? [] : [new(Key.F11, ModifierKeys.Control | ModifierKeys.Shift), new(Key.F12, ModifierKeys.Alt)]);
            using Fixture fixture = new(new HotKeyConfig { Hotkeys = [saved] });
            string originalJson = JsonConvert.SerializeObject(fixture.Config);

            fixture.Load();
            AssertOverride(fixture, type, saved);
            Assert.Equal(originalJson, JsonConvert.SerializeObject(fixture.Config));
            fixture.Service.ReloadSettings();
            Assert.True(fixture.Service.LastApplyResult!.Success, fixture.Service.LastApplyResult.Message);
            AssertOverride(fixture, type, saved);
            fixture.Load();
            AssertOverride(fixture, type, saved);

            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [MemberData(nameof(ResetCases))]
    public void ResetOneUpgradedActionUsesItsNewDefaultAndPreservesOtherOverrides(Type type, bool cleared)
    {
        WpfTestHost.Invoke(() =>
        {
            var config = new HotKeyConfig();
            for (int index = 0; index < BuiltInTypes.Length; index++)
            {
                Type providerType = BuiltInTypes[index];
                HotKeys declaration = ReadDeclaration(providerType);
                HotkeySetting setting = new() { Id = ActionId(providerType, declaration), Kinds = HotKeyKinds.Global };
                setting.SetBindings(cleared && providerType == type ? [] : [new((Key)((int)Key.F6 + index), ModifierKeys.Control | ModifierKeys.Shift)]);
                config.Hotkeys.Add(setting);
            }
            using Fixture fixture = new(config);
            fixture.Load();
            HotKeys target = fixture.Entry(type);
            var otherEntries = fixture.Service.HotKeys.Where(item => item.Id != target.Id)
                .Select(item => (Runtime: item, Setting: HotkeySetting.FromHotKeys(item), Handle: item.Registration)).ToArray();
            var model = new HotkeySettingsViewModel(
                () => fixture.Service.CreateEditableHotKeys(),
                () => fixture.Service.CreateDefaultEditableHotKeys(),
                fixture.Service.ApplyAndSaveSettings,
                validate: fixture.Service.ValidateSettings);

            Assert.True(model.Reset(model.Rows.Single(row => row.Value.Id == target.Id)), model.Status);

            Assert.Equal(ReadDeclaration(type).GetDefaultBindings(), target.GetBindings());
            Assert.Equal(HotKeyKinds.Windows, target.Kinds);
            Assert.Equal(target.GetBindings(), fixture.Handle(target).Bindings);
            Assert.False(model.Rows.Single(row => row.Value.Id == target.Id).IsModified);
            Assert.Equal(1, fixture.PersistCalls);
            foreach (var other in otherEntries)
            {
                Assert.Equal(other.Setting.GetBindings(), other.Runtime.GetBindings());
                Assert.Equal(other.Setting.Kinds, other.Runtime.Kinds);
                Assert.Same(other.Handle, other.Runtime.Registration);
            }

            // Round-trip the fake saved bytes into a fresh service, not the existing runtime objects.
            using Fixture restarted = new(fixture.Persisted());
            restarted.Load();
            Assert.Equal(target.GetBindings(), restarted.Entry(type).GetBindings());
            Assert.Equal(target.Kinds, restarted.Entry(type).Kinds);
            foreach (var other in otherEntries)
            {
                HotKeys reloaded = restarted.Service.HotKeys.Single(item => item.Id == other.Runtime.Id);
                Assert.Equal(other.Setting.GetBindings(), reloaded.GetBindings());
                Assert.Equal(other.Setting.Kinds, reloaded.Kinds);
            }
            Assert.Equal(0, restarted.PersistCalls);
        });
    }

    private static void AssertOverride(Fixture fixture, Type type, HotkeySetting expected)
    {
        HotKeys runtime = fixture.Entry(type);
        Assert.Equal(expected.GetBindings(), runtime.GetBindings());
        Assert.Equal(expected.Kinds, runtime.Kinds);
        Assert.Equal(ReadDeclaration(type).GetDefaultBindings(), runtime.GetDefaultBindings());
        Assert.Equal(HotKeyKinds.Windows, runtime.DefaultKinds);
        Assert.Equal(expected.GetBindings().Count > 0, runtime.IsRegistered);
        if (runtime.IsRegistered) Assert.Equal(expected.GetBindings(), fixture.Handle(runtime).Bindings);
        else Assert.Null(runtime.Registration);
    }

    private static HotKeys ReadDeclaration(Type type) => Assert.IsAssignableFrom<IHotKey>(Activator.CreateInstance(type)).HotKeys;

    private static string ActionId(Type type, HotKeys declaration) => string.IsNullOrWhiteSpace(declaration.Id) ? type.FullName! : declaration.Id;

    private sealed class UpgradedDefaultsProvider : IHotkeyProvider
    {
        public UpgradedDefaultsProvider() { }

        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions()
        {
            foreach (Type type in BuiltInTypes)
            {
                // Only copy safe declarations; never retain or call a production business callback.
                HotKeys declaration = ReadDeclaration(type);
                yield return new HotkeyDefinition(ActionId(type, declaration), declaration.Name, declaration.DefaultHotkey,
                    () => throw new InvalidOperationException("Shortcut lifecycle must not invoke commands."), declaration.DefaultKinds)
                {
                    AdditionalDefaultHotkeys = declaration.DefaultAdditionalHotkeys,
                    Description = declaration.Description,
                    Category = declaration.Category
                };
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        public Window Host { get; } = new();
        public HotKeyConfig Config { get; }
        public HotkeyService Service { get; }
        public int RegisterCalls { get; private set; }
        public int PersistCalls { get; private set; }
        private string? _persistedJson;

        public Fixture(HotKeyConfig? config = null)
        {
            Config = config == null ? new() : JsonConvert.DeserializeObject<HotKeyConfig>(JsonConvert.SerializeObject(config))!;
            Service = new(Register, Persist, () => Config, () => [typeof(UpgradedDefaultsProvider)]);
        }

        public void Load() => Service.LoadFromAssemblies(Host);
        public HotKeys Entry(Type type) => Service.HotKeys.Single(item => item.Id == ActionId(type, ReadDeclaration(type)));
        public FakeRegistration Handle(HotKeys runtime) => Assert.IsType<FakeRegistration>(runtime.Registration);
        public HotKeyConfig Persisted() => JsonConvert.DeserializeObject<HotKeyConfig>(Assert.IsType<string>(_persistedJson))!;

        private HotkeyRegistrationAttempt Register(Control owner, HotKeys runtime)
        {
            RegisterCalls++;
            return new(new FakeRegistration(runtime.GetBindings()));
        }

        private HotkeyPersistenceAttempt Persist(HotKeyConfig candidate, Action publish)
        {
            PersistCalls++;
            _persistedJson = JsonConvert.SerializeObject(candidate);
            publish();
            return new(ConfigSavePublicationStatus.PersistedAndPublished);
        }

        public void Dispose()
        {
            Service.UnregisterAll();
            Host.Close();
        }
    }

    private sealed class FakeRegistration(IReadOnlyList<Hotkey> bindings) : IHotkeyRegistration
    {
        public IReadOnlyList<Hotkey> Bindings { get; } = bindings.Select(binding => new Hotkey(binding.Key, binding.Modifiers)).ToArray();
        public Hotkey Hotkey => Bindings.FirstOrDefault() ?? new();
        public bool IsRegistered { get; private set; } = true;
        public void Dispose() => IsRegistered = false;
    }
}
