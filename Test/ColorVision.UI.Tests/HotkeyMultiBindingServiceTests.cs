using ColorVision.UI.HotKey;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HotkeyMultiBindingServiceTests
{
    [Fact]
    public void FirstLoadWithoutConfigurationUsesAllDefaultsAndKeepsUnassignedActions()
    {
        WithFixture(fixture =>
        {
            fixture.LoadProviders();

            HotKeys assigned = fixture.Entry("provider-assigned");
            AssertBindings(assigned.GetBindings(), Key.N, Key.O);
            AssertBindings(assigned.GetDefaultBindings(), Key.N, Key.O);
            AssertBindings(fixture.Handle(assigned).Bindings, Key.N, Key.O);
            HotKeys unassigned = fixture.Entry("provider-unassigned");
            Assert.Empty(unassigned.GetBindings());
            Assert.Empty(unassigned.GetDefaultBindings());
            Assert.False(unassigned.IsRegistered);
            Assert.Empty(fixture.Config.Hotkeys);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void AddingEditingAndRemovingThePrimaryBindingPersistsEveryRemainingBinding()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A);
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.A, Key.B, Key.C)]).Success);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B, Key.C);
            AssertBindings(fixture.Handle(runtime).Bindings, Key.A, Key.B, Key.C);

            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.B, Key.C)]).Success);
            AssertBindings(runtime.GetBindings(), Key.B, Key.C);
            Assert.Equal(Key.B, runtime.Hotkey.Key);
            AssertBindings(runtime.AdditionalHotkeys, Key.C);

            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.B, Key.D)]).Success);
            AssertBindings(runtime.GetBindings(), Key.B, Key.D);
            AssertBindings(fixture.Persisted().Hotkeys.Single().GetBindings(), Key.B, Key.D);
            AssertBindings(fixture.Config.Hotkeys.Single().GetBindings(), Key.B, Key.D);

            // The saved JSON, not the existing runtime object, supplies the new service's bindings.
            using Fixture restarted = new();
            HotKeys restartedRuntime = restarted.Add("action", Key.F1);
            restarted.Config.Hotkeys = fixture.Persisted().Hotkeys;
            restarted.Service.ReloadSettings();
            Assert.True(restarted.Service.LastApplyResult!.Success);
            AssertBindings(restartedRuntime.GetBindings(), Key.B, Key.D);
            AssertBindings(restarted.Handle(restartedRuntime).Bindings, Key.B, Key.D);
        });
    }

    [Fact]
    public void ExplicitlyClearingTheLastBindingSurvivesReloadAndDiscoveryInsteadOfRestoringDefaults()
    {
        WithFixture(fixture =>
        {
            fixture.LoadProviders();
            HotKeys runtime = fixture.Entry("provider-assigned");
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.O)]).Success);
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime)]).Success);
            Assert.Empty(runtime.GetBindings());
            Assert.False(runtime.IsRegistered);
            Assert.Empty(fixture.Persisted().Hotkeys.Single().GetBindings());

            fixture.Service.ReloadSettings();
            Assert.True(fixture.Service.LastApplyResult!.Success);
            Assert.Empty(runtime.GetBindings());
            fixture.Config.Hotkeys = fixture.Persisted().Hotkeys;
            fixture.LoadProviders();

            HotKeys reloaded = fixture.Entry("provider-assigned");
            Assert.Empty(reloaded.GetBindings());
            Assert.False(reloaded.IsRegistered);
            AssertBindings(reloaded.GetDefaultBindings(), Key.N, Key.O);
            Assert.Empty(fixture.Entry("provider-unassigned").GetBindings());
        });
    }

    [Fact]
    public void ResetRestoresEveryDefaultBindingAndTheDefaultScope()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            runtime.DefaultKinds = HotKeyKinds.Global;
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.C)]).Success);
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime)]).Success);
            Assert.Empty(runtime.GetBindings());

            var defaults = fixture.Service.CreateDefaultEditableHotKeys();
            AssertBindings(defaults.Single().GetBindings(), Key.A, Key.B);
            Assert.True(fixture.Service.ApplyAndSaveSettings(defaults.Select(HotkeySetting.FromHotKeys)).Success);

            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            AssertBindings(fixture.Handle(runtime).Bindings, Key.A, Key.B);
            Assert.Equal(HotKeyKinds.Global, runtime.Kinds);
            Assert.Equal(HotKeyKinds.Global, fixture.Persisted().Hotkeys.Single().Kinds);
            AssertBindings(fixture.Persisted().Hotkeys.Single().GetBindings(), Key.A, Key.B);
            fixture.Service.SetDefault();
            Assert.True(fixture.Service.LastApplyResult!.Success);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
        });
    }

    [Fact]
    public void AnInitiallyUnassignedActionCanBeAssignedAndResetToUnassigned()
    {
        WithFixture(fixture =>
        {
            fixture.LoadProviders();
            HotKeys runtime = fixture.Entry("provider-unassigned");
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.A, Key.B)]).Success);
            Assert.True(runtime.IsRegistered);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);

            HotKeys defaults = fixture.Service.CreateDefaultEditableHotKeys().Single(item => item.Id == runtime.Id);
            Assert.True(fixture.Service.ApplyAndSaveSettings([HotkeySetting.FromHotKeys(defaults)]).Success);

            Assert.Empty(runtime.GetBindings());
            Assert.False(runtime.IsRegistered);
            Assert.Empty(fixture.Persisted().Hotkeys.Single().GetBindings());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateBindingsWithinOneActionAreRejectedWithoutMutatingOrSaving(bool duplicateAdditional)
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            FakeRegistration original = fixture.Handle(runtime);
            HotkeySetting candidate = duplicateAdditional ? Setting(runtime, Key.A, Key.C, Key.C) : Setting(runtime, Key.A, Key.A);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([candidate]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Message.Contains("重复", StringComparison.Ordinal));
            Assert.Same(original, runtime.Registration);
            Assert.True(original.IsRegistered);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            Assert.Equal(1, fixture.RegisterCalls);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(Key.None, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.None, ModifierKeys.Control)]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]
    public void InvalidAdditionalBindingsAreRejectedBeforeReplacingAnExistingGroup(Key key, ModifierKeys modifiers)
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            FakeRegistration original = fixture.Handle(runtime);
            HotkeySetting candidate = Setting(runtime, Key.A);
            candidate.AdditionalHotkeys.Add(new(key, modifiers));

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([candidate]);

            Assert.False(result.Success);
            Assert.Same(original, runtime.Registration);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdditionalBindingConflictsIncludeOtherActionsAdditionalBindings(bool globalConflict)
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A, Key.B);
            HotKeys second = fixture.Add("second", Key.C, Key.D);
            FakeRegistration firstHandle = fixture.Handle(first);
            FakeRegistration secondHandle = fixture.Handle(second);
            if (globalConflict) second.Kinds = HotKeyKinds.Global;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.A, Key.D)]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Id == first.Id && error.Message.Contains(second.Name, StringComparison.Ordinal));
            Assert.Same(firstHandle, first.Registration);
            Assert.Same(secondHandle, second.Registration);
            AssertBindings(first.GetBindings(), Key.A, Key.B);
            Assert.Equal(2, fixture.RegisterCalls);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public void AdditionalBindingConflictsRespectIndependentAncestorAndGlobalScopes(bool ancestorScope, bool globalScope, bool expectedSuccess)
    {
        WithFixture(fixture =>
        {
            ContentControl firstOwner = new();
            Button secondOwner = new();
            if (ancestorScope)
            {
                firstOwner.Content = new ContentControl { Content = secondOwner };
                Assert.False(firstOwner.IsAncestorOf(secondOwner));
                Assert.Same(firstOwner, LogicalTreeHelper.GetParent(LogicalTreeHelper.GetParent(secondOwner)));
            }
            HotKeys first = new("first", Combination(Key.A), () => { }) { Id = "first", AdditionalHotkeys = [Combination(Key.B)] };
            HotKeys second = new("second", Combination(Key.C), () => { }) { Id = "second", AdditionalHotkeys = [Combination(Key.D)] };
            Assert.True(fixture.Service.AddHotKeys(firstOwner, first));
            Assert.True(fixture.Service.AddHotKeys(secondOwner, second));
            HotkeySetting candidate = Setting(first, Key.A, Key.D);
            if (globalScope) candidate.Kinds = HotKeyKinds.Global;

            HotkeyApplyResult result = fixture.Service.ValidateSettings([candidate]);

            Assert.Equal(expectedSuccess, result.Success);
            AssertBindings(first.GetBindings(), Key.A, Key.B);
            AssertBindings(second.GetBindings(), Key.C, Key.D);
            Assert.Equal(2, fixture.RegisterCalls);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void AdditionalBindingConflictsAlsoRecognizeTemplateVisualAncestorsWithoutLogicalParents()
    {
        WithFixture(fixture =>
        {
            ContentControl owner = new()
            {
                Template = (ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                        <Button x:Name="ScopeChild" />
                    </ControlTemplate>
                    """)
            };
            Assert.True(owner.ApplyTemplate());
            Button child = Assert.IsType<Button>(owner.Template.FindName("ScopeChild", owner));
            Assert.True(owner.IsAncestorOf(child));
            Assert.Null(LogicalTreeHelper.GetParent(child));
            HotKeys first = new("first", Combination(Key.A), () => { }) { Id = "first", AdditionalHotkeys = [Combination(Key.B)] };
            HotKeys second = new("second", Combination(Key.C), () => { }) { Id = "second", AdditionalHotkeys = [Combination(Key.D)] };
            Assert.True(fixture.Service.AddHotKeys(owner, first));
            Assert.True(fixture.Service.AddHotKeys(child, second));

            HotkeyApplyResult result = fixture.Service.ValidateSettings([Setting(second, Key.C, Key.B)]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Id == second.Id && error.Message.Contains(first.Name, StringComparison.Ordinal));
            AssertBindings(first.GetBindings(), Key.A, Key.B);
            AssertBindings(second.GetBindings(), Key.C, Key.D);
            Assert.Equal(2, fixture.RegisterCalls);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void SwappingAdditionalBindingsInOneSubmissionSucceedsAndUnchangedGroupsKeepTheirHandle()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A, Key.B);
            HotKeys second = fixture.Add("second", Key.C, Key.D);
            HotKeys unchanged = fixture.Add("unchanged", Key.E, Key.F);
            FakeRegistration unchangedHandle = fixture.Handle(unchanged);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.A, Key.D), Setting(second, Key.C, Key.B), Setting(unchanged, Key.E, Key.F)]);

            Assert.True(result.Success, result.Message);
            AssertBindings(first.GetBindings(), Key.A, Key.D);
            AssertBindings(second.GetBindings(), Key.C, Key.B);
            Assert.Same(unchangedHandle, unchanged.Registration);
            Assert.Equal(5, fixture.RegisterCalls);
        });
    }

    [Fact]
    public void RegistrationFailureRestoresTheWholePreviousGroupWithoutSaving()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            FakeRegistration oldHandle = fixture.Handle(runtime);
            fixture.RegisterFailure = entry => entry.GetBindings().Any(binding => binding.Key == Key.C) ? "additional-unavailable" : null;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.A, Key.C, Key.D)]);

            Assert.False(result.Success);
            Assert.Empty(result.RestoreErrors);
            Assert.False(oldHandle.IsRegistered);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            AssertBindings(fixture.Handle(runtime).Bindings, Key.A, Key.B);
            AssertBindings(fixture.Config.Hotkeys.Single().GetBindings(), Key.A, Key.B);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void PersistenceFailureRestoresEveryBindingAndScopeInRuntimeAndConfiguration()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            fixture.PersistOverride = (_, _) => new(ConfigSavePublicationStatus.NotPersisted, "save-failed");
            HotkeySetting candidate = Setting(runtime, Key.C, Key.D, Key.E);
            candidate.Kinds = HotKeyKinds.Global;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([candidate]);

            Assert.False(result.Success);
            Assert.Empty(result.RestoreErrors);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            AssertBindings(fixture.Handle(runtime).Bindings, Key.A, Key.B);
            Assert.Equal(HotKeyKinds.Windows, runtime.Kinds);
            AssertBindings(fixture.Config.Hotkeys.Single().GetBindings(), Key.A, Key.B);
            Assert.Equal(HotKeyKinds.Windows, fixture.Config.Hotkeys.Single().Kinds);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicationFailureKeepsDiskAndTheCompleteRuntimeGroupAligned(bool compensationFails)
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            fixture.PersistOverride = (candidate, publish) =>
            {
                if (fixture.PersistCalls == 1)
                {
                    fixture.PersistedJson = JsonConvert.SerializeObject(candidate);
                    return new(ConfigSavePublicationStatus.PersistedButPublishFailed, "publish-failed");
                }
                if (compensationFails) return new(ConfigSavePublicationStatus.NotPersisted, "compensation-failed");
                fixture.PersistedJson = JsonConvert.SerializeObject(candidate);
                publish();
                return new(ConfigSavePublicationStatus.PersistedAndPublished);
            };

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.C, Key.D, Key.E)]);

            Assert.False(result.Success);
            Key[] expected = compensationFails ? [Key.C, Key.D, Key.E] : [Key.A, Key.B];
            AssertBindings(runtime.GetBindings(), expected);
            AssertBindings(fixture.Handle(runtime).Bindings, expected);
            AssertBindings(fixture.Persisted().Hotkeys.Single().GetBindings(), expected);
            AssertBindings(fixture.Config.Hotkeys.Single().GetBindings(), expected);
            Assert.Equal(compensationFails, result.RestoreErrors.Count > 0);
            Assert.Equal(2, fixture.PersistCalls);
        });
    }

    [Fact]
    public void FailedExplicitReplacementRestoresAllCurrentAndDefaultBindings()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            runtime.SetDefaultBindings([Combination(Key.F1), Combination(Key.F2), Combination(Key.F3)]);
            HotKeyCallBackHanlder? callback = runtime.HotKeyHandler;
            HotKeys replacement = new("replacement", Combination(Key.C), () => { }) { Id = runtime.Id };
            replacement.SetBindings([Combination(Key.C), Combination(Key.D)]);
            replacement.SetDefaultBindings([Combination(Key.E), Combination(Key.F)]);
            fixture.RegisterFailure = entry => entry.GetBindings().Any(binding => binding.Key == Key.D) ? "replacement-failed" : null;

            Assert.False(fixture.Service.AddHotKeys(fixture.Host, replacement));

            Assert.False(fixture.Service.LastApplyResult!.Success);
            Assert.Empty(fixture.Service.LastApplyResult.RestoreErrors);
            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            AssertBindings(runtime.GetDefaultBindings(), Key.F1, Key.F2, Key.F3);
            AssertBindings(fixture.Handle(runtime).Bindings, Key.A, Key.B);
            Assert.Same(callback, runtime.HotKeyHandler);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void CapturePausesAndRestoresTheWholeGlobalGroupWithoutChangingLocalBindings()
    {
        WithFixture(fixture =>
        {
            HotKeys global = fixture.Add("global", Key.A, Key.B, Key.C);
            HotkeySetting globalSetting = Setting(global, Key.A, Key.B, Key.C);
            globalSetting.Kinds = HotKeyKinds.Global;
            Assert.True(fixture.Service.ApplyAndSaveSettings([globalSetting]).Success);
            HotKeys local = fixture.Add("local", Key.D, Key.E);
            FakeRegistration globalHandle = fixture.Handle(global);
            FakeRegistration localHandle = fixture.Handle(local);
            int persistedBefore = fixture.PersistCalls;

            var capture = fixture.Service.BeginCapture();
            try
            {
                Assert.False(globalHandle.IsRegistered);
                Assert.False(global.IsRegistered);
                Assert.Same(localHandle, local.Registration);
                Assert.True(localHandle.IsRegistered);
                AssertBindings(global.GetBindings(), Key.A, Key.B, Key.C);
                Assert.True(fixture.Service.ValidateSettings([Setting(local, Key.D, Key.F)]).Success);
            }
            finally { capture.Dispose(); }

            Assert.NotNull(capture.RestoreResult);
            Assert.True(capture.RestoreResult.Success, capture.RestoreResult.Message);
            AssertBindings(fixture.Handle(global).Bindings, Key.A, Key.B, Key.C);
            Assert.Same(localHandle, local.Registration);
            Assert.Equal(persistedBefore, fixture.PersistCalls);
        });
    }

    [Fact]
    public void SavingALoadedActionPreservesAbsentPluginsMultipleAndUnassignedBindings()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("loaded", Key.A, Key.B);
            HotkeySetting absent = new() { Id = "absent-plugin", Kinds = HotKeyKinds.Global };
            absent.SetBindings([Combination(Key.C), Combination(Key.D)]);
            fixture.Config.Hotkeys.Add(absent);
            fixture.Config.Hotkeys.Add(new() { Id = "absent-unassigned" });

            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(runtime, Key.E, Key.F)]).Success);

            HotKeyConfig saved = fixture.Persisted();
            Assert.Equal(3, saved.Hotkeys.Count);
            AssertBindings(saved.Hotkeys.Single(item => item.Id == "absent-plugin").GetBindings(), Key.C, Key.D);
            Assert.Equal(HotKeyKinds.Global, saved.Hotkeys.Single(item => item.Id == "absent-plugin").Kinds);
            Assert.Empty(saved.Hotkeys.Single(item => item.Id == "absent-unassigned").GetBindings());
            AssertBindings(saved.Hotkeys.Single(item => item.Id == "loaded").GetBindings(), Key.E, Key.F);
        });
    }

    [Fact]
    public void EditableCopiesSavedOverridesAndDefaultsOwnEveryBindingObject()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("action", Key.A, Key.B);
            fixture.Config.Hotkeys.Clear();
            fixture.Config.Hotkeys.Add(Setting(runtime, Key.C, Key.D));
            HotKeys copy = fixture.Service.CreateEditableHotKeys(useSavedSettings: true).Single();

            AssertBindings(copy.GetBindings(), Key.C, Key.D);
            AssertBindings(copy.GetDefaultBindings(), Key.A, Key.B);
            Assert.Null(copy.Control);
            Assert.Null(copy.HotKeyHandler);
            copy.Hotkey.Key = Key.E;
            copy.AdditionalHotkeys[0].Key = Key.F;
            copy.DefaultAdditionalHotkeys[0].Key = Key.G;

            AssertBindings(runtime.GetBindings(), Key.A, Key.B);
            AssertBindings(runtime.GetDefaultBindings(), Key.A, Key.B);
            AssertBindings(fixture.Config.Hotkeys.Single().GetBindings(), Key.C, Key.D);
        });
    }

    private static Hotkey Combination(Key key) => new(key, ModifierKeys.Control);

    private static HotkeySetting Setting(HotKeys runtime, params Key[] keys)
    {
        HotkeySetting setting = new() { Id = runtime.Id, Kinds = runtime.Kinds };
        setting.SetBindings(keys.Select(Combination));
        return setting;
    }

    private static void AssertBindings(IEnumerable<Hotkey> bindings, params Key[] keys)
        => Assert.Equal(keys.Select(Combination), bindings);

    private static void WithFixture(Action<Fixture> action) => WpfTestHost.Invoke(() =>
    {
        using Fixture fixture = new();
        action(fixture);
        Assert.Equal(0, fixture.CallbackInvocations);
    });

    private sealed class Fixture : IDisposable
    {
        public Window Host { get; } = new();
        public HotKeyConfig Config { get; } = new();
        public HotkeyService Service { get; }
        public int RegisterCalls { get; private set; }
        public int PersistCalls { get; private set; }
        public int CallbackInvocations { get; private set; }
        public string? PersistedJson { get; set; }
        public Func<HotKeys, string?>? RegisterFailure { get; set; }
        public Func<HotKeyConfig, Action, HotkeyPersistenceAttempt>? PersistOverride { get; set; }

        public Fixture() => Service = new(Register, Persist, () => Config, () => [typeof(MultipleDefaultsProvider)]);

        public void LoadProviders() => Service.LoadFromAssemblies(Host);
        public HotKeys Entry(string id) => Service.HotKeys.Single(item => item.Id == id);
        public FakeRegistration Handle(HotKeys runtime) => Assert.IsType<FakeRegistration>(runtime.Registration);
        public HotKeyConfig Persisted() => JsonConvert.DeserializeObject<HotKeyConfig>(Assert.IsType<string>(PersistedJson))!;

        public HotKeys Add(string id, params Key[] keys)
        {
            HotKeys runtime = new() { Id = id, Name = id, HotKeyHandler = () => CallbackInvocations++ };
            runtime.SetBindings(keys.Select(Combination));
            runtime.SetDefaultBindings(keys.Select(Combination));
            Assert.Equal(keys.Length > 0, Service.AddHotKeys(Host, runtime));
            Config.Hotkeys.Add(HotkeySetting.FromHotKeys(runtime));
            return runtime;
        }

        private HotkeyRegistrationAttempt Register(Control owner, HotKeys runtime)
        {
            RegisterCalls++;
            string? error = RegisterFailure?.Invoke(runtime);
            return error == null ? new(new FakeRegistration(runtime.Id, runtime.GetBindings())) : new(null, error);
        }

        private HotkeyPersistenceAttempt Persist(HotKeyConfig candidate, Action publish)
        {
            PersistCalls++;
            if (PersistOverride != null) return PersistOverride(candidate, publish);
            PersistedJson = JsonConvert.SerializeObject(candidate);
            publish();
            return new(ConfigSavePublicationStatus.PersistedAndPublished);
        }

        public void Dispose()
        {
            Service.UnregisterAll();
            Host.Close();
        }
    }

    private sealed class FakeRegistration(string id, IReadOnlyList<Hotkey> bindings) : IHotkeyRegistration
    {
        public string Id { get; } = id;
        public IReadOnlyList<Hotkey> Bindings { get; } = bindings.Select(binding => new Hotkey(binding.Key, binding.Modifiers)).ToList();
        public Hotkey Hotkey => Bindings.FirstOrDefault() ?? new();
        public bool IsRegistered { get; private set; } = true;
        public void Dispose() => IsRegistered = false;
    }

    private sealed class MultipleDefaultsProvider : IHotkeyProvider
    {
        public MultipleDefaultsProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() =>
        [
            new("provider-assigned", "Assigned", Combination(Key.N), () => { }) { AdditionalDefaultHotkeys = [Combination(Key.O)] },
            new("provider-unassigned", "Unassigned", new(), () => { })
        ];
    }
}
