using ColorVision.UI.HotKey;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HotkeyServiceTests
{
    [Fact]
    public void FaultyProvidersAreIsolatedBeforeReplacingTheWorkingDefinitionSet()
    {
        WithFixture(fixture =>
        {
            HotKeys previous = fixture.Add("previous", Key.A);
            FakeRegistration previousHandle = fixture.Handle(previous);
            fixture.Config.Hotkeys.Add(new() { Id = "healthy-first", Hotkey = new(Key.F9, ModifierKeys.Control) });
            fixture.ProviderTypes = InspectDiscovery;

            fixture.Service.LoadFromAssemblies(fixture.Host);

            Assert.Equal(new[] { "healthy-first", "healthy-last" }, fixture.Service.HotKeys.Select(item => item.Id).ToArray());
            Assert.All(fixture.Service.HotKeys, item => Assert.True(item.IsRegistered));
            Assert.Equal(Key.F9, fixture.Service.HotKeys[0].Hotkey.Key);
            Assert.Equal(1, previousHandle.DisposeCalls);
            Assert.DoesNotContain(fixture.Service.HotKeys, item => item.Id == "partial-provider");
            Assert.Equal(0, fixture.PersistCalls);

            IEnumerable<Type> InspectDiscovery()
            {
                Type[] providers = [typeof(HealthyFirstProvider), typeof(ThrowingConstructorProvider), typeof(ThrowingMethodProvider),
                    typeof(ThrowingIteratorProvider), typeof(ThrowingLegacyPropertyProvider), typeof(NullSequenceProvider), typeof(HealthyLastProvider)];
                foreach (Type provider in providers)
                {
                    Assert.True(previous.IsRegistered);
                    Assert.Equal(0, previousHandle.DisposeCalls);
                    Assert.Same(previous, Assert.Single(fixture.Service.HotKeys));
                    yield return provider;
                }
            }
        });
    }

    [Fact]
    public void FailureOfTheProviderTypeSourceKeepsThePreviousDefinitionsAndRegistrations()
    {
        WithFixture(fixture =>
        {
            HotKeys previous = fixture.Add("previous", Key.A);
            FakeRegistration previousHandle = fixture.Handle(previous);
            int registerCalls = fixture.RegisterCalls.Count;
            fixture.ProviderTypes = BrokenTypeSource;

            Assert.Throws<InvalidOperationException>(() => fixture.Service.LoadFromAssemblies(fixture.Host));

            Assert.Same(previous, Assert.Single(fixture.Service.HotKeys));
            Assert.Same(previousHandle, previous.Registration);
            Assert.True(previous.IsRegistered);
            Assert.Equal(0, previousHandle.DisposeCalls);
            Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
            Assert.Equal(0, fixture.PersistCalls);
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(previous, Key.C)]).Success);

            static IEnumerable<Type> BrokenTypeSource()
            {
                yield return typeof(HealthyFirstProvider);
                throw new InvalidOperationException("provider-type-source-failed");
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitReplacementFailureRestoresTheOriginalDefinitionHostCallbackAndRegistration(bool useWindowOverload, bool throws)
    {
        WithFixture(fixture =>
        {
            Button originalHost = new();
            HotKeys original = fixture.Add("original", Key.A, control: originalHost);
            original.Name = "original-name";
            original.DisplayName = "original-display";
            original.Description = "original-description";
            original.Category = "original-category";
            original.Source = "original-source";
            original.DefaultHotkey = new(Key.F9, ModifierKeys.Alt);
            original.DefaultKinds = HotKeyKinds.Global;
            HotKeyCallBackHanlder? originalCallback = original.HotKeyHandler;
            FakeRegistration oldHandle = fixture.Handle(original);
            HotKeys other = fixture.Add("other", Key.B);
            FakeRegistration otherHandle = fixture.Handle(other);
            int candidateCalls = 0;
            HotKeys candidate = new("replacement-name", new(Key.C, ModifierKeys.Control), () => candidateCalls++)
            {
                Id = original.Id, DisplayName = "replacement-display", Description = "replacement-description",
                Category = "replacement-category", Source = "replacement-source", DefaultKinds = HotKeyKinds.Windows
            };
            fixture.RegisterFailure = (_, item) => item.Hotkey.Key == Key.C
                ? throws ? throw new InvalidOperationException("replacement-failed") : "replacement-failed" : null;

            bool registered = useWindowOverload ? fixture.Service.AddHotKeys(fixture.Host, candidate)
                : fixture.Service.AddHotKeys(new Button(), candidate);

            Assert.False(registered);
            Assert.NotNull(fixture.Service.LastApplyResult);
            Assert.False(fixture.Service.LastApplyResult.Success);
            Assert.Contains(fixture.Service.LastApplyResult.Errors, error => error.Message.Contains("replacement-failed", StringComparison.Ordinal));
            Assert.Empty(fixture.Service.LastApplyResult.RestoreErrors);
            Assert.Same(original, fixture.Service.HotKeys.Single(item => item.Id == original.Id));
            Assert.Equal("original-name", original.Name);
            Assert.Equal("original-display", original.DisplayName);
            Assert.Equal("original-description", original.Description);
            Assert.Equal("original-category", original.Category);
            Assert.Equal("original-source", original.Source);
            Assert.Equal(new Hotkey(Key.F9, ModifierKeys.Alt), original.DefaultHotkey);
            Assert.Equal(HotKeyKinds.Global, original.DefaultKinds);
            Assert.Equal(new Hotkey(Key.A, ModifierKeys.Control), original.Hotkey);
            Assert.Equal(HotKeyKinds.Windows, original.Kinds);
            Assert.Same(originalHost, original.Control);
            Assert.Same(originalCallback, original.HotKeyHandler);
            Assert.Same(originalCallback, fixture.Handle(original).Callback);
            Assert.Same(originalHost, fixture.Handle(original).Control);
            Assert.True(original.IsRegistered);
            Assert.Equal(1, oldHandle.DisposeCalls);
            Assert.Same(otherHandle, other.Registration);
            Assert.Equal(0, otherHandle.DisposeCalls);
            Assert.Equal(Key.A, fixture.Config.Hotkeys.Single(item => item.Id == original.Id).Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
            Assert.Equal(0, candidateCalls);
        });
    }

    [Fact]
    public void ExplicitReplacementReportsWhenRestoringTheOldRegistrationAlsoFails()
    {
        WithFixture(fixture =>
        {
            HotKeys original = fixture.Add("original", Key.A);
            HotKeyCallBackHanlder? callback = original.HotKeyHandler;
            fixture.RegisterFailure = (_, item) => item.Hotkey.Key == Key.C ? "new-registration-failed" : "old-registration-restore-failed";
            HotKeys candidate = new("replacement", new(Key.C, ModifierKeys.Control), () => { }) { Id = original.Id };

            Assert.False(fixture.Service.AddHotKeys(fixture.Host, candidate));

            Assert.NotNull(fixture.Service.LastApplyResult);
            Assert.Contains(fixture.Service.LastApplyResult.Errors, error => error.Message.Contains("new-registration-failed", StringComparison.Ordinal));
            Assert.Contains(fixture.Service.LastApplyResult.RestoreErrors, error => error.Message.Contains("old-registration-restore-failed", StringComparison.Ordinal));
            Assert.Equal(Key.A, original.Hotkey.Key);
            Assert.Equal("original", original.Name);
            Assert.Same(callback, original.HotKeyHandler);
            Assert.Same(fixture.Host, original.Control);
            Assert.False(original.IsRegistered);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void FailedUnregisterDoesNotPublishTheReplacementDefinitionOverTheStillActiveHandle()
    {
        WithFixture(fixture =>
        {
            HotKeys original = fixture.Add("original", Key.A);
            HotKeyCallBackHanlder? callback = original.HotKeyHandler;
            FakeRegistration handle = fixture.Handle(original);
            handle.DisposeFailure = new InvalidOperationException("unregister-failed");
            try
            {
                HotKeys candidate = new("replacement", new(Key.C, ModifierKeys.Control), () => { }) { Id = original.Id };

                Assert.False(fixture.Service.AddHotKeys(fixture.Host, candidate));

                Assert.NotNull(fixture.Service.LastApplyResult);
                Assert.Contains(fixture.Service.LastApplyResult.Errors, error => error.Message.Contains("unregister-failed", StringComparison.Ordinal));
                Assert.Empty(fixture.Service.LastApplyResult.RestoreErrors);
                Assert.Same(handle, original.Registration);
                Assert.True(original.IsRegistered);
                Assert.Equal(Key.A, original.Hotkey.Key);
                Assert.Equal("original", original.Name);
                Assert.Same(callback, original.HotKeyHandler);
                Assert.Single(fixture.RegisterCalls);
                Assert.Equal(0, fixture.PersistCalls);
            }
            finally { handle.DisposeFailure = null; }
        });
    }

    [Fact]
    public void SuccessfulExplicitReplacementChangesTheExistingEntryButDoesNotPersistIt()
    {
        WithFixture(fixture =>
        {
            HotKeys original = fixture.Add("original", Key.A);
            FakeRegistration oldHandle = fixture.Handle(original);
            Button newHost = new();
            HotKeys candidate = new("replacement", new(Key.C, ModifierKeys.Alt), () => { }) { Id = original.Id };

            Assert.True(fixture.Service.AddHotKeys(newHost, candidate));

            Assert.Same(original, Assert.Single(fixture.Service.HotKeys));
            Assert.Equal("replacement", original.Name);
            Assert.Equal(candidate.Hotkey, original.Hotkey);
            Assert.Same(candidate.HotKeyHandler, fixture.Handle(original).Callback);
            Assert.Same(newHost, original.Control);
            Assert.Equal(1, oldHandle.DisposeCalls);
            Assert.NotNull(fixture.Service.LastApplyResult);
            Assert.True(fixture.Service.LastApplyResult.Success);
            Assert.Equal(Key.A, Assert.Single(fixture.Config.Hotkeys).Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void ConflictingWindowCombinationDoesNotUnregisterOrPersistAnything()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys second = fixture.Add("second", Key.B);
            FakeRegistration firstHandle = fixture.Handle(first);
            FakeRegistration secondHandle = fixture.Handle(second);
            int registerCalls = fixture.RegisterCalls.Count;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.B)]);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
            Assert.Equal(0, firstHandle.DisposeCalls);
            Assert.Equal(0, secondHandle.DisposeCalls);
            Assert.Same(firstHandle, first.Registration);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(Key.None, ModifierKeys.Control)]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]
    [InlineData(Key.System, ModifierKeys.Alt)]
    [InlineData(Key.ImeProcessed, ModifierKeys.Control)]
    [InlineData(Key.A, ModifierKeys.None)]
    [InlineData(Key.A, ModifierKeys.Shift)]
    [InlineData(Key.Enter, ModifierKeys.None)]
    [InlineData(Key.Delete, ModifierKeys.None)]
    [InlineData((Key)99999, ModifierKeys.Control)]
    [InlineData(Key.C, (ModifierKeys)32)]
    public void InvalidCombinationsLeaveTheExistingRegistrationUntouched(Key key, ModifierKeys modifiers)
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            FakeRegistration original = fixture.Handle(hotkey);
            int calls = fixture.RegisterCalls.Count;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(hotkey, key, modifiers)]);

            Assert.False(result.Success);
            Assert.NotEmpty(result.Errors);
            Assert.Equal(calls, fixture.RegisterCalls.Count);
            Assert.Same(original, hotkey.Registration);
            Assert.Equal(0, original.DisposeCalls);
            Assert.Equal(Key.A, hotkey.Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-loaded-plugin")]
    public void MissingOrUnknownRuntimeIdsAreRejectedWithoutTouchingSavedPluginSettings(string id)
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            fixture.Config.Hotkeys.Add(new HotkeySetting { Id = "not-loaded-plugin", Hotkey = new(Key.F9, ModifierKeys.Control) });
            FakeRegistration original = fixture.Handle(hotkey);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([new HotkeySetting { Id = id, Hotkey = new(Key.C, ModifierKeys.Control) }]);

            Assert.False(result.Success);
            Assert.Same(original, hotkey.Registration);
            Assert.Equal(0, original.DisposeCalls);
            Assert.Equal(Key.F9, fixture.Config.Hotkeys.Single(setting => setting.Id == "not-loaded-plugin").Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void DuplicateIdsAndUnsupportedKindsAreRejectedBeforeUnregistering()
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            FakeRegistration original = fixture.Handle(hotkey);
            HotkeySetting duplicate = Setting(hotkey, Key.D);
            duplicate.Id = "FIRST";

            Assert.False(fixture.Service.ApplyAndSaveSettings([Setting(hotkey, Key.C), duplicate]).Success);
            HotkeySetting invalidKind = Setting(hotkey, Key.C);
            invalidKind.Kinds = (HotKeyKinds)99;
            Assert.False(fixture.Service.ApplyAndSaveSettings([invalidKind]).Success);

            Assert.Same(original, hotkey.Registration);
            Assert.Equal(0, original.DisposeCalls);
            Assert.Equal(1, fixture.RegisterCalls.Count);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void SingleReplacementPreservesOtherHandlesAndPublishesTheSavedCombination()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys other = fixture.Add("other", Key.B);
            FakeRegistration old = fixture.Handle(first);
            FakeRegistration untouched = fixture.Handle(other);
            HotkeySetting candidate = Setting(first, Key.C, ModifierKeys.Control | ModifierKeys.Shift);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([candidate]);

            Assert.True(result.Success, result.Message);
            Assert.Empty(result.Errors);
            Assert.Empty(result.RestoreErrors);
            Assert.Equal(1, old.DisposeCalls);
            Assert.NotSame(old, first.Registration);
            Assert.True(first.IsRegistered);
            Assert.Equal(candidate.Hotkey, first.Hotkey);
            Assert.Same(untouched, other.Registration);
            Assert.Equal(0, untouched.DisposeCalls);
            Assert.Equal(1, fixture.PersistCalls);
            Assert.Equal(candidate.Hotkey, fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey);
            Assert.Equal(candidate.Hotkey, fixture.PersistedConfig!.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey);
            candidate.Hotkey.Key = Key.Z;
            Assert.Equal(Key.C, first.Hotkey.Key);
            Assert.Equal(Key.C, fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
        });
    }

    [Fact]
    public void UnchangedSettingsKeepTheSameRegistrationHandle()
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            FakeRegistration original = fixture.Handle(hotkey);
            int registerCalls = fixture.RegisterCalls.Count;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(hotkey, Key.A)]);

            Assert.True(result.Success, result.Message);
            Assert.Same(original, hotkey.Registration);
            Assert.Equal(0, original.DisposeCalls);
            Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
        });
    }

    [Fact]
    public void ClearingOneCombinationDisablesItWithoutRegisteringNoneOrChangingOthers()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys other = fixture.Add("other", Key.B);
            FakeRegistration old = fixture.Handle(first);
            FakeRegistration untouched = fixture.Handle(other);
            int registerCalls = fixture.RegisterCalls.Count;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.None, ModifierKeys.None)]);

            Assert.True(result.Success, result.Message);
            Assert.True(first.Hotkey.IsEmpty);
            Assert.False(first.IsRegistered);
            Assert.Null(first.Registration);
            Assert.Equal(1, old.DisposeCalls);
            Assert.Same(untouched, other.Registration);
            Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
            Assert.True(fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.IsEmpty);
        });
    }

    [Fact]
    public void WindowCombinationsMayMatchInDistinctControlScopesAndKeepTheirOriginalHosts()
    {
        WithFixture(fixture =>
        {
            fixture.Add("main", Key.F1);
            Button firstControl = new();
            Button secondControl = new();
            HotKeys first = fixture.Add("first", Key.A, control: firstControl);
            HotKeys second = fixture.Add("second", Key.B, control: secondControl);
            FakeRegistration secondHandle = fixture.Handle(second);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.B)]);

            Assert.True(result.Success, result.Message);
            Assert.Same(firstControl, first.Control);
            Assert.Same(firstControl, fixture.Handle(first).Control);
            Assert.Same(secondControl, second.Control);
            Assert.Same(secondHandle, second.Registration);
            Assert.Equal(0, secondHandle.DisposeCalls);
        });
    }

    [Fact]
    public void ValidationAllowsTheSameWindowCombinationInIndependentControlScopesWithoutApplyingIt()
    {
        WithFixture(fixture =>
        {
            fixture.Add("main", Key.F1);
            Button firstControl = new();
            Button secondControl = new();
            StackPanel content = new();
            content.Children.Add(firstControl);
            content.Children.Add(secondControl);
            fixture.Host.Content = content;
            HotKeys first = fixture.Add("first", Key.A, control: firstControl);
            HotKeys second = fixture.Add("second", Key.B, control: secondControl);
            FakeRegistration firstHandle = fixture.Handle(first);
            FakeRegistration secondHandle = fixture.Handle(second);
            int registerCalls = fixture.RegisterCalls.Count;

            HotkeyApplyResult? previousApply = fixture.Service.LastApplyResult;

            HotkeyApplyResult allowed = fixture.Service.ValidateSettings([Setting(first, Key.B)]);
            HotkeyApplyResult globalConflict = fixture.Service.ValidateSettings([Setting(first, Key.B, kind: HotKeyKinds.Global)]);

            Assert.True(allowed.Success, allowed.Message);
            Assert.False(globalConflict.Success);
            Assert.NotEmpty(globalConflict.Errors);
            Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
            Assert.Same(firstHandle, first.Registration);
            Assert.Same(secondHandle, second.Registration);
            Assert.Equal(0, firstHandle.DisposeCalls);
            Assert.Equal(0, secondHandle.DisposeCalls);
            Assert.Same(firstControl, first.Control);
            Assert.Same(secondControl, second.Control);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.Equal(HotKeyKinds.Windows, first.Kinds);
            Assert.Equal(Key.A, fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
            Assert.Same(previousApply, fixture.Service.LastApplyResult);
        });
    }

    [Fact]
    public void AGlobalCombinationConflictsWithAWindowCombinationWithoutReleasingEither()
    {
        WithFixture(fixture =>
        {
            HotKeys local = fixture.Add("local", Key.A);
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global);
            FakeRegistration globalHandle = fixture.Handle(global);
            FakeRegistration localHandle = fixture.Handle(local);
            int calls = fixture.RegisterCalls.Count;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(global, Key.A, kind: HotKeyKinds.Global)]);

            Assert.False(result.Success);
            Assert.Same(globalHandle, global.Registration);
            Assert.Same(localHandle, local.Registration);
            Assert.Equal(0, globalHandle.DisposeCalls);
            Assert.Equal(0, localHandle.DisposeCalls);
            Assert.Equal(calls, fixture.RegisterCalls.Count);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedReplacementRestoresTheOldCombinationAndDoesNotPersist(bool throws)
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            FakeRegistration old = fixture.Handle(hotkey);
            fixture.RegisterFailure = (_, candidate) => candidate.Hotkey.Key == Key.C
                ? throws ? throw new InvalidOperationException("fake-register-error") : "fake-register-error"
                : null;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(hotkey, Key.C)]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Message.Contains("fake-register-error", StringComparison.Ordinal));
            Assert.Empty(result.RestoreErrors);
            Assert.Equal(Key.A, hotkey.Hotkey.Key);
            Assert.True(hotkey.IsRegistered);
            Assert.NotSame(old, hotkey.Registration);
            Assert.Equal(1, old.DisposeCalls);
            Assert.Equal(Key.A, fixture.Handle(hotkey).Hotkey.Key);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void FailedOldRegistrationRecoveryIsReportedInsteadOfClaimingTheOldKeyWorks()
    {
        WithFixture(fixture =>
        {
            HotKeys hotkey = fixture.Add("first", Key.A);
            fixture.RegisterFailure = (_, candidate) => candidate.Hotkey.Key == Key.C ? "new-registration-failed" : "old-registration-failed";

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(hotkey, Key.C)]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Message.Contains("new-registration-failed", StringComparison.Ordinal));
            Assert.Contains(result.RestoreErrors, error => error.Message.Contains("old-registration-failed", StringComparison.Ordinal));
            Assert.Equal(Key.A, hotkey.Hotkey.Key);
            Assert.False(hotkey.IsRegistered);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void BatchCanSwapTwoCombinationsUsingTheFinalCandidateSetForConflictDetection()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys second = fixture.Add("second", Key.B);

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.B), Setting(second, Key.A)]);

            Assert.True(result.Success, result.Message);
            Assert.Equal(Key.B, first.Hotkey.Key);
            Assert.Equal(Key.A, second.Hotkey.Key);
            Assert.True(first.IsRegistered);
            Assert.True(second.IsRegistered);
            Assert.Equal(1, fixture.PersistCalls);
        });
    }

    [Fact]
    public void LaterBatchFailureDisposesTheNewEarlierHandleAndRestoresEveryChangedItem()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys second = fixture.Add("second", Key.B);
            fixture.RegisterFailure = (_, candidate) => candidate.Id == "second" && candidate.Hotkey.Key == Key.D ? "second-new-key-failed" : null;

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.C), Setting(second, Key.D)]);

            Assert.False(result.Success);
            Assert.Empty(result.RestoreErrors);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.Equal(Key.B, second.Hotkey.Key);
            Assert.True(first.IsRegistered);
            Assert.True(second.IsRegistered);
            FakeRegistration transient = Assert.Single(fixture.CreatedHandles.Where(handle => handle.Id == first.Id && handle.Hotkey.Key == Key.C));
            Assert.Equal(1, transient.DisposeCalls);
            Assert.False(transient.IsRegistered);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NotPersistedFailureCompensatesRuntimeWithoutTouchingOtherRegistrations(bool throws)
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            HotKeys other = fixture.Add("other", Key.B);
            FakeRegistration untouched = fixture.Handle(other);
            fixture.PersistOverride = (_, _) => throws
                ? throw new IOException("fake-save-error")
                : new(ConfigSavePublicationStatus.NotPersisted, "fake-save-error");

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.C)]);

            Assert.False(result.Success);
            Assert.Contains(result.Errors, error => error.Message.Contains("fake-save-error", StringComparison.Ordinal));
            Assert.Empty(result.RestoreErrors);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.True(first.IsRegistered);
            Assert.Equal(Key.A, fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Same(untouched, other.Registration);
            Assert.Equal(0, untouched.DisposeCalls);
            Assert.Equal(1, fixture.PersistCalls);
        });
    }

    [Fact]
    public void PersistedButPublishFailedCompensatesDiskBeforeRestoringRuntime()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            fixture.PersistOverride = (candidate, publish) =>
            {
                fixture.PersistedConfig = CloneConfig(candidate);
                if (fixture.PersistCalls == 1)
                    return new(ConfigSavePublicationStatus.PersistedButPublishFailed, "initial-publish-failed");
                publish();
                return new(ConfigSavePublicationStatus.PersistedAndPublished);
            };

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.C)]);

            Assert.False(result.Success);
            Assert.Empty(result.RestoreErrors);
            Assert.Equal(2, fixture.PersistCalls);
            Assert.Equal(Key.C, fixture.PersistenceCandidates[0].Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(Key.A, fixture.PersistenceCandidates[1].Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(Key.A, fixture.PersistedConfig!.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.True(first.IsRegistered);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailedDiskCompensationKeepsTheNewRuntimeCombinationThatWasActuallyPersisted(bool throws)
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            fixture.PersistOverride = (candidate, _) =>
            {
                if (fixture.PersistCalls == 1)
                {
                    fixture.PersistedConfig = CloneConfig(candidate);
                    return new(ConfigSavePublicationStatus.PersistedButPublishFailed, "initial-publish-failed");
                }
                return throws ? throw new IOException("disk-compensation-failed")
                    : new(ConfigSavePublicationStatus.NotPersisted, "disk-compensation-failed");
            };

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.C)]);

            Assert.False(result.Success);
            Assert.NotEmpty(result.RestoreErrors);
            Assert.Contains(result.RestoreErrors, error => error.Message.Contains("disk-compensation-failed", StringComparison.Ordinal));
            Assert.Equal(2, fixture.PersistCalls);
            Assert.Equal(Key.C, fixture.PersistedConfig!.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(Key.C, first.Hotkey.Key);
            Assert.True(first.IsRegistered);
            Assert.Equal(Key.C, fixture.Config.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
        });
    }

    [Fact]
    public void DiskRestoredButPublicationStillFailedReportsTheRemainingRecoveryFailure()
    {
        WithFixture(fixture =>
        {
            HotKeys first = fixture.Add("first", Key.A);
            fixture.PersistOverride = (candidate, _) =>
            {
                fixture.PersistedConfig = CloneConfig(candidate);
                return new(ConfigSavePublicationStatus.PersistedButPublishFailed,
                    fixture.PersistCalls == 1 ? "initial-publish-failed" : "old-config-publish-failed");
            };

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(first, Key.C)]);

            Assert.False(result.Success);
            Assert.Contains(result.RestoreErrors, error => error.Message.Contains("old-config-publish-failed", StringComparison.Ordinal));
            Assert.Equal(2, fixture.PersistCalls);
            Assert.Equal(Key.A, fixture.PersistedConfig!.Hotkeys.Single(setting => setting.Id == first.Id).Hotkey.Key);
            Assert.Equal(Key.A, first.Hotkey.Key);
            Assert.True(first.IsRegistered);
        });
    }

    [Fact]
    public void SavedEntriesForUnloadedPluginsAndUnmatchedLegacyNamesArePreserved()
    {
        WithFixture(fixture =>
        {
            HotKeys loaded = fixture.Add("loaded", Key.A);
            fixture.Config.Hotkeys.Add(new() { Id = "future-plugin", Hotkey = new(Key.F9, ModifierKeys.Alt), Kinds = HotKeyKinds.Global });
            fixture.Config.Hotkeys.Add(new() { Id = "future-name-collision", LegacyName = loaded.Name, Hotkey = new(Key.F8, ModifierKeys.Control) });
            fixture.Config.Hotkeys.Add(new() { LegacyName = "unloaded-legacy-action", Hotkey = new(Key.F10, ModifierKeys.Control) });

            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(loaded, Key.C)]);

            Assert.True(result.Success, result.Message);
            HotkeySetting future = Assert.Single(fixture.Config.Hotkeys.Where(setting => setting.Id == "future-plugin"));
            Assert.Equal(new Hotkey(Key.F9, ModifierKeys.Alt), future.Hotkey);
            Assert.Equal(HotKeyKinds.Global, future.Kinds);
            HotkeySetting sameLegacyName = Assert.Single(fixture.Config.Hotkeys.Where(setting => setting.Id == "future-name-collision"));
            Assert.Equal(loaded.Name, sameLegacyName.LegacyName);
            Assert.Equal(new Hotkey(Key.F8, ModifierKeys.Control), sameLegacyName.Hotkey);
            HotkeySetting legacy = Assert.Single(fixture.Config.Hotkeys.Where(setting => setting.LegacyName == "unloaded-legacy-action"));
            Assert.Equal(new Hotkey(Key.F10, ModifierKeys.Control), legacy.Hotkey);
            Assert.Contains(fixture.PersistedConfig!.Hotkeys, setting => setting.Id == "future-plugin");
            Assert.Contains(fixture.PersistedConfig.Hotkeys, setting => setting.Id == "future-name-collision" && setting.Hotkey.Key == Key.F8);
            Assert.Contains(fixture.PersistedConfig.Hotkeys, setting => setting.LegacyName == "unloaded-legacy-action");
        });
    }

    [Fact]
    public void PersistedUnloadedLegacyEntryRetainsItsIdentityAcrossTheActualJsonContract()
    {
        WithFixture(fixture =>
        {
            HotKeys loaded = fixture.Add("loaded", Key.A);
            fixture.Config.Hotkeys.Add(new() { LegacyName = "unloaded-legacy-action", Hotkey = new(Key.F10, ModifierKeys.Control) });
            HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(loaded, Key.C)]);
            Assert.True(result.Success, result.Message);

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(fixture.PersistedConfig);
            HotKeyConfig roundTrip = Newtonsoft.Json.JsonConvert.DeserializeObject<HotKeyConfig>(json)!;
            Assert.True(Hotkey.None.IsEmpty, "Deserialization must not populate the shared None sentinel.");
            Assert.NotSame(roundTrip.Hotkeys[0].Hotkey, roundTrip.Hotkeys[1].Hotkey);

            HotkeySetting legacy = Assert.Single(roundTrip.Hotkeys.Where(setting => string.IsNullOrEmpty(setting.Id)));
            Assert.Equal("unloaded-legacy-action", legacy.LegacyName);
            Assert.Equal(new Hotkey(Key.F10, ModifierKeys.Control), legacy.Hotkey);
            var document = Newtonsoft.Json.Linq.JObject.Parse(json);
            var stable = Assert.Single(document["Hotkeys"]!.Children<Newtonsoft.Json.Linq.JObject>().Where(item => (string?)item["Id"] == loaded.Id));
            Assert.Null(stable["Name"]);
            Assert.Null(stable["IsGlobal"]);
        });
    }

    [Fact]
    public void EditableCopiesCannotMutateRuntimeOrDefaultCombinations()
    {
        WithFixture(fixture =>
        {
            HotKeys runtime = fixture.Add("first", Key.A);
            HotKeys draft = Assert.Single(fixture.Service.CreateEditableHotKeys());
            draft.Hotkey.Key = Key.C;
            draft.DefaultHotkey.Key = Key.D;
            draft.Kinds = HotKeyKinds.Global;

            Assert.Equal(Key.A, runtime.Hotkey.Key);
            Assert.Equal(Key.A, runtime.DefaultHotkey.Key);
            Assert.Equal(HotKeyKinds.Windows, runtime.Kinds);
            Assert.Null(draft.Registration);
            Assert.Null(draft.Control);
            Assert.Null(draft.HotKeyHandler);
        });
    }

    [Fact]
    public void CaptureSuspendsDispatchButKeepsWindowHandlesAndRestoresOnlyReleasedGlobalHandles()
    {
        WithFixture(fixture =>
        {
            HotKeys local = fixture.Add("local", Key.A);
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global);
            FakeRegistration localHandle = fixture.Handle(local);
            FakeRegistration globalHandle = fixture.Handle(global);
            Assert.False(HotkeyDispatchGate.IsSuspended);

            var lease = fixture.Service.BeginCapture();
            try
            {
                Assert.True(HotkeyDispatchGate.IsSuspended);
                Assert.Same(localHandle, local.Registration);
                Assert.Equal(0, localHandle.DisposeCalls);
                Assert.Equal(1, globalHandle.DisposeCalls);
                Assert.False(global.IsRegistered);
                Assert.Equal(0, fixture.PersistCalls);
            }
            finally { lease.Dispose(); }

            Assert.NotNull(lease.RestoreResult);
            Assert.True(lease.RestoreResult.Success, lease.RestoreResult.Message);
            Assert.False(HotkeyDispatchGate.IsSuspended);
            Assert.Same(localHandle, local.Registration);
            Assert.True(global.IsRegistered);
            Assert.NotSame(globalHandle, global.Registration);
            Assert.Equal(Key.B, fixture.Handle(global).Hotkey.Key);
            Assert.Equal(0, fixture.CallbackInvocations);
        });
    }

    [Fact]
    public void NestedCaptureRestoresOnlyOnTheLastDisposeAndRepeatedDisposeIsHarmless()
    {
        WithFixture(fixture =>
        {
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global);
            FakeRegistration original = fixture.Handle(global);
            var outer = fixture.Service.BeginCapture();
            var inner = fixture.Service.BeginCapture();
            int suspendedCalls = fixture.RegisterCalls.Count;
            try
            {
                Assert.Equal(1, original.DisposeCalls);
                outer.Dispose();
                Assert.True(HotkeyDispatchGate.IsSuspended);
                Assert.False(global.IsRegistered);
                Assert.Equal(suspendedCalls, fixture.RegisterCalls.Count);
                Assert.NotNull(outer.RestoreResult);
                Assert.True(outer.RestoreResult.Success);
                inner.Dispose();
                Assert.False(HotkeyDispatchGate.IsSuspended);
                Assert.True(global.IsRegistered);
                Assert.Equal(suspendedCalls + 1, fixture.RegisterCalls.Count);
                inner.Dispose();
                outer.Dispose();
                Assert.Equal(suspendedCalls + 1, fixture.RegisterCalls.Count);
            }
            finally { inner.Dispose(); outer.Dispose(); }
        });
    }

    [Fact]
    public void CaptureRejectsApplyingChangesUntilCaptureEnds()
    {
        WithFixture(fixture =>
        {
            HotKeys local = fixture.Add("local", Key.A);
            FakeRegistration original = fixture.Handle(local);
            using (fixture.Service.BeginCapture())
            {
                HotkeyApplyResult result = fixture.Service.ApplyAndSaveSettings([Setting(local, Key.C)]);
                Assert.False(result.Success);
                Assert.NotEmpty(result.Errors);
                Assert.Equal(Key.A, local.Hotkey.Key);
                Assert.Same(original, local.Registration);
                Assert.Equal(0, fixture.PersistCalls);
            }
            Assert.False(HotkeyDispatchGate.IsSuspended);
            Assert.True(fixture.Service.ApplyAndSaveSettings([Setting(local, Key.C)]).Success);
        });
    }

    [Fact]
    public void ValidationDuringCaptureReportsCandidatesWithoutChangingHandlesConfigurationOrCaptureState()
    {
        WithFixture(fixture =>
        {
            HotKeys local = fixture.Add("local", Key.A);
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global);
            FakeRegistration localHandle = fixture.Handle(local);
            FakeRegistration globalHandle = fixture.Handle(global);
            HotkeyApplyResult previousApply = fixture.Service.ApplyAndSaveSettings([Setting(local, Key.B)]);
            Assert.False(previousApply.Success);
            string originalConfiguration = Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Config);

            var capture = fixture.Service.BeginCapture();
            int registerCalls = fixture.RegisterCalls.Count;
            try
            {
                Assert.True(HotkeyDispatchGate.IsSuspended);
                HotkeyApplyResult valid = fixture.Service.ValidateSettings([Setting(local, Key.C)]);
                HotkeyApplyResult conflict = fixture.Service.ValidateSettings([Setting(local, Key.B)]);
                HotkeyApplyResult invalid = fixture.Service.ValidateSettings([Setting(local, Key.None, ModifierKeys.Control)]);

                Assert.True(valid.Success, valid.Message);
                Assert.False(conflict.Success);
                Assert.False(invalid.Success);
                Assert.NotEmpty(conflict.Errors);
                Assert.NotEmpty(invalid.Errors);
                Assert.True(HotkeyDispatchGate.IsSuspended);
                Assert.Null(capture.RestoreResult);
                Assert.Null(fixture.Service.LastCaptureRestoreResult);
                Assert.Same(previousApply, fixture.Service.LastApplyResult);
                Assert.Equal(registerCalls, fixture.RegisterCalls.Count);
                Assert.Same(localHandle, local.Registration);
                Assert.Equal(0, localHandle.DisposeCalls);
                Assert.Null(global.Registration);
                Assert.False(global.IsRegistered);
                Assert.Equal(1, globalHandle.DisposeCalls);
                Assert.Equal(Key.A, local.Hotkey.Key);
                Assert.Equal(Key.B, global.Hotkey.Key);
                Assert.Equal(originalConfiguration, Newtonsoft.Json.JsonConvert.SerializeObject(fixture.Config));
                Assert.Equal(0, fixture.PersistCalls);
                Assert.Empty(fixture.PersistenceCandidates);
            }
            finally { capture.Dispose(); }

            Assert.NotNull(capture.RestoreResult);
            Assert.True(capture.RestoreResult.Success, capture.RestoreResult.Message);
            Assert.False(HotkeyDispatchGate.IsSuspended);
            Assert.True(global.IsRegistered);
            Assert.Equal(registerCalls + 1, fixture.RegisterCalls.Count);
            Assert.Same(previousApply, fixture.Service.LastApplyResult);
            Assert.Equal(0, fixture.PersistCalls);
        });
    }

    [Fact]
    public void CaptureDoesNotRetryGlobalKeysThatWereAlreadyUnregistered()
    {
        WithFixture(fixture =>
        {
            fixture.RegisterFailure = (_, _) => "initially-unavailable";
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global, expectRegistered: false);
            fixture.RegisterFailure = null;
            int calls = fixture.RegisterCalls.Count;

            var lease = fixture.Service.BeginCapture();
            lease.Dispose();

            Assert.NotNull(lease.RestoreResult);
            Assert.True(lease.RestoreResult.Success, lease.RestoreResult.Message);
            Assert.Equal(calls, fixture.RegisterCalls.Count);
            Assert.False(global.IsRegistered);
            Assert.False(HotkeyDispatchGate.IsSuspended);
        });
    }

    [Fact]
    public void CaptureRestorationFailureIsVisibleAndDoesNotLeaveDispatchPermanentlySuspended()
    {
        WithFixture(fixture =>
        {
            HotKeys global = fixture.Add("global", Key.B, kind: HotKeyKinds.Global);
            var lease = fixture.Service.BeginCapture();
            fixture.RegisterFailure = (_, _) => "capture-restore-failed";
            lease.Dispose();

            Assert.NotNull(lease.RestoreResult);
            Assert.False(lease.RestoreResult.Success);
            Assert.Contains(lease.RestoreResult.Errors.Concat(lease.RestoreResult.RestoreErrors),
                error => error.Message.Contains("capture-restore-failed", StringComparison.Ordinal));
            Assert.False(global.IsRegistered);
            Assert.False(HotkeyDispatchGate.IsSuspended);
            int calls = fixture.RegisterCalls.Count;
            lease.Dispose();
            Assert.Equal(calls, fixture.RegisterCalls.Count);
        });
    }

    private static HotkeySetting Setting(HotKeys source, Key key, ModifierKeys modifiers = ModifierKeys.Control, HotKeyKinds? kind = null)
        => new() { Id = source.Id, Hotkey = new(key, modifiers), Kinds = kind ?? source.Kinds };

    private static HotKeyConfig CloneConfig(HotKeyConfig config) => new()
    {
        Hotkeys = new ObservableCollection<HotkeySetting>(config.Hotkeys.Select(setting => new HotkeySetting
        {
            Id = setting.Id,
            LegacyName = setting.LegacyName,
            Kinds = setting.Kinds,
            Hotkey = new(setting.Hotkey.Key, setting.Hotkey.Modifiers)
        }))
    };

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
        public List<(Control Host, string Id, Hotkey Hotkey, HotKeyKinds Kind)> RegisterCalls { get; } = [];
        public List<FakeRegistration> CreatedHandles { get; } = [];
        public List<HotKeyConfig> PersistenceCandidates { get; } = [];
        public Func<Control, HotKeys, string?>? RegisterFailure { get; set; }
        public Func<HotKeyConfig, Action, HotkeyPersistenceAttempt>? PersistOverride { get; set; }
        public HotKeyConfig? PersistedConfig { get; set; }
        public Func<IEnumerable<Type>> ProviderTypes { get; set; } = () => [];
        public int PersistCalls { get; private set; }
        public int CallbackInvocations { get; private set; }

        public Fixture() => Service = new HotkeyService(Register, Persist, () => Config, () => ProviderTypes());

        public HotKeys Add(string id, Key key, ModifierKeys modifiers = ModifierKeys.Control, HotKeyKinds kind = HotKeyKinds.Windows,
            Control? control = null, bool expectRegistered = true)
        {
            HotKeys runtime = new(id, new Hotkey(key, modifiers), () => CallbackInvocations++)
            {
                Id = id,
                Kinds = kind,
                DefaultKinds = kind
            };
            bool registered = control == null ? Service.AddHotKeys(Host, runtime) : Service.AddHotKeys(control, runtime);
            Assert.Equal(expectRegistered, registered);
            Config.Hotkeys.Add(new HotkeySetting { Id = id, Hotkey = new(key, modifiers), Kinds = kind });
            return Service.HotKeys.Single(item => item.Id == id);
        }

        public FakeRegistration Handle(HotKeys runtime) => Assert.IsType<FakeRegistration>(runtime.Registration);

        private HotkeyRegistrationAttempt Register(Control control, HotKeys runtime)
        {
            RegisterCalls.Add((control, runtime.Id, new(runtime.Hotkey.Key, runtime.Hotkey.Modifiers), runtime.Kinds));
            string? error = RegisterFailure?.Invoke(control, runtime);
            if (error != null) return new(null, error);
            FakeRegistration registration = new(runtime.Id, control, runtime.Hotkey, runtime.HotKeyHandler);
            CreatedHandles.Add(registration);
            return new(registration);
        }

        private HotkeyPersistenceAttempt Persist(HotKeyConfig candidate, Action publish)
        {
            PersistCalls++;
            PersistenceCandidates.Add(CloneConfig(candidate));
            if (PersistOverride != null) return PersistOverride(candidate, publish);
            PersistedConfig = CloneConfig(candidate);
            publish();
            return new(ConfigSavePublicationStatus.PersistedAndPublished);
        }

        public void Dispose()
        {
            Service.UnregisterAll();
            Host.Close();
        }
    }

    private sealed class FakeRegistration(string id, Control control, Hotkey hotkey, HotKeyCallBackHanlder? callback) : IHotkeyRegistration
    {
        public string Id { get; } = id;
        public Control Control { get; } = control;
        public Hotkey Hotkey { get; } = new(hotkey.Key, hotkey.Modifiers);
        public HotKeyCallBackHanlder? Callback { get; } = callback;
        public bool IsRegistered { get; private set; } = true;
        public int DisposeCalls { get; private set; }
        public Exception? DisposeFailure { get; set; }
        public void Dispose()
        {
            DisposeCalls++;
            if (DisposeFailure != null) throw DisposeFailure;
            IsRegistered = false;
        }
    }

    private static HotkeyDefinition ProviderDefinition(string id, Key key) => new(id, id, new(key, ModifierKeys.Control), () => { });

    private sealed class HealthyFirstProvider : IHotkeyProvider
    {
        public HealthyFirstProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() => [ProviderDefinition("healthy-first", Key.F6)];
    }

    private sealed class HealthyLastProvider : IHotkeyProvider
    {
        public HealthyLastProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() => [ProviderDefinition("healthy-last", Key.F7)];
    }

    private sealed class ThrowingConstructorProvider : IHotkeyProvider
    {
        public ThrowingConstructorProvider() => throw new InvalidOperationException("provider-constructor-failed");
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() => [];
    }

    private sealed class ThrowingMethodProvider : IHotkeyProvider
    {
        public ThrowingMethodProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() => throw new InvalidOperationException("provider-method-failed");
    }

    private sealed class ThrowingIteratorProvider : IHotkeyProvider
    {
        public ThrowingIteratorProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions()
        {
            yield return ProviderDefinition("partial-provider", Key.F8);
            throw new InvalidOperationException("provider-iterator-failed");
        }
    }

    private sealed class ThrowingLegacyPropertyProvider : IHotKey
    {
        public ThrowingLegacyPropertyProvider() { }
        public HotKeys HotKeys => throw new InvalidOperationException("legacy-provider-property-failed");
    }

    private sealed class NullSequenceProvider : IHotkeyProvider
    {
        public NullSequenceProvider() { }
        public IEnumerable<HotkeyDefinition> GetHotkeyDefinitions() => null!;
    }
}
