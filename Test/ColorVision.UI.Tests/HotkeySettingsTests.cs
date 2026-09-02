using ColorVision.UI.Desktop.Settings;
using ColorVision.UI.HotKey;
using Newtonsoft.Json;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class HotkeySettingsTests
{
    [Fact]
    public void SearchMatchesNameDescriptionIdentityAndNormalizedCombination()
    {
        Fixture fixture = new();
        HotkeySettingsViewModel model = fixture.Model;
        foreach (string query in new[] { "设置", "偏好", "options", "ctrl + i", "CtrlI" })
        {
            model.Search = query;
            Assert.Equal("options", Assert.Single(model.Rows).Value.Id);
        }
        model.Search = "no-such-action";
        Assert.True(model.IsEmpty);
        model.Search = "";
        Assert.Equal(4, model.Rows.Count);
        Assert.Empty(fixture.Applied);
    }

    [Fact]
    public void ClearAndResetApplyOnlyTheTargetAndKeepOtherBindings()
    {
        Fixture fixture = new();
        Assert.True(fixture.Model.Clear(fixture.Model.Rows[0]));
        Assert.False(fixture.Model.Rows[0].IsAssigned);
        Assert.True(fixture.Model.Rows[0].IsModified);
        Assert.Equal("options", Assert.Single(Assert.Single(fixture.Applied)).Id);
        Assert.Equal(Key.L, fixture.Model.Rows[1].Value.Hotkey.Key);
        Assert.True(fixture.Model.Reset(fixture.Model.Rows[0]));
        Assert.Equal(Key.I, fixture.Model.Rows[0].Value.Hotkey.Key);
        Assert.False(fixture.Model.Rows[0].IsModified);
        Assert.True(fixture.Model.ResetAll());
        Assert.Equal(4, fixture.Applied.Last().Count);
    }

    [Fact]
    public void MultipleBindingsCanBeAddedEditedAndIndependentlyRemoved()
    {
        Fixture fixture = new();
        Assert.True(fixture.Model.SaveBinding(fixture.Model.Rows[0], null, new(Key.O, ModifierKeys.Control | ModifierKeys.Shift), HotKeyKinds.Windows));
        Assert.Equal(2, fixture.Model.Rows[0].Bindings.Count);
        Assert.Equal(2, fixture.Applied.Last().Single().GetBindings().Count);
        Assert.True(fixture.Model.SaveBinding(fixture.Model.Rows[0], 1, new(Key.P, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.Equal(new[] { Key.I, Key.P }, fixture.Values[0].GetBindings().Select(key => key.Key));
        Assert.True(fixture.Model.RemoveBinding(fixture.Model.Rows[0].Bindings[0]));
        Assert.Equal(Key.P, Assert.Single(fixture.Model.Rows[0].Bindings).Key.Key);
        Assert.True(fixture.Model.RemoveBinding(fixture.Model.Rows[0].Bindings[0]));
        Assert.True(fixture.Model.Rows[0].IsUnassigned);
        Assert.Equal(HotkeyEditorText.Unassigned, fixture.Model.Rows[0].Shortcut);
        Assert.Equal(4, fixture.Model.Rows.Count);
        Assert.Equal(Key.L, fixture.Values[1].Hotkey.Key);
    }

    [Fact]
    public void DuplicateAndOtherActionsAlternateBindingConflictBeforeSaving()
    {
        Fixture fixture = new();
        fixture.Values[1].AdditionalHotkeys = [new(Key.P, ModifierKeys.Control)];
        fixture.Model.Refresh();
        Assert.False(fixture.Model.SaveBinding(fixture.Model.Rows[0], null, new(Key.I, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.Equal(HotkeyEditorText.Duplicate, fixture.Model.Status);
        Assert.False(fixture.Model.SaveBinding(fixture.Model.Rows[0], null, new(Key.P, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.Contains("日志", fixture.Model.Status);
        Assert.Empty(fixture.Applied);
    }

    [Fact]
    public void SearchFindsAlternateBindingsDescriptionsAndStatesAndCanClearFilters()
    {
        Fixture fixture = new();
        fixture.Values[0].AdditionalHotkeys = [new(Key.O, ModifierKeys.Control | ModifierKeys.Shift)];
        fixture.Values[2].SetBindings([]);
        fixture.Model.Refresh();
        foreach (string query in new[] { "Ctrl + Shift + O", "偏好 设置", "options CtrlShiftO", "已修改 设置" })
        {
            fixture.Model.Search = query;
            Assert.Equal("options", Assert.Single(fixture.Model.Rows).Value.Id);
        }
        fixture.Model.Search = HotkeyEditorText.Unassigned;
        Assert.Equal("update", Assert.Single(fixture.Model.Rows).Value.Id);
        fixture.Model.ClearFilters();
        fixture.Model.FilterIndex = 1;
        Assert.Equal("update", Assert.Single(fixture.Model.Rows).Value.Id);
        fixture.Model.FilterIndex = 2;
        Assert.Equal(2, fixture.Model.Rows.Count);
        fixture.Model.Search = "not-present";
        Assert.True(fixture.Model.IsEmpty);
        fixture.Model.ClearFilters();
        Assert.False(fixture.Model.HasFilter);
        Assert.Equal(4, fixture.Model.Rows.Count);
        Assert.Empty(fixture.Applied);
    }

    [Fact]
    public void ResetRestoresEveryDefaultBindingAndKeepsSearch()
    {
        Fixture fixture = new();
        fixture.Values[0].DefaultAdditionalHotkeys = [new(Key.O, ModifierKeys.Control | ModifierKeys.Shift)];
        fixture.Values[0].SetBindings([new(Key.P, ModifierKeys.Control)]);
        fixture.Model.Refresh();
        fixture.Model.Search = "设置";
        Assert.True(fixture.Model.Reset(Assert.Single(fixture.Model.Rows)));
        Assert.Equal("设置", fixture.Model.Search);
        Assert.Equal(new[] { Key.I, Key.O }, Assert.Single(fixture.Model.Rows).Bindings.Select(binding => binding.Key.Key));
        Assert.False(fixture.Model.Rows[0].IsModified);
        Assert.True(fixture.Model.Clear(fixture.Model.Rows[0]));
        Assert.True(fixture.Model.ResetAll());
        Assert.Equal(2, fixture.Values[0].GetBindings().Count);
        Assert.Equal(4, fixture.Applied.Last().Count);
    }

    [Fact]
    public void NeverAssignedActionRemainsVisibleAndResetRestoresItsEmptyDefault()
    {
        Fixture fixture = new();
        fixture.Values[0].SetBindings([]);
        fixture.Values[0].SetDefaultBindings([]);
        fixture.Model.Refresh();
        Assert.True(fixture.Model.Rows[0].IsUnassigned);
        Assert.False(fixture.Model.Rows[0].IsModified);
        Assert.True(fixture.Model.SaveBinding(fixture.Model.Rows[0], null, new(Key.P, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.True(fixture.Model.Reset(fixture.Model.Rows[0]));
        Assert.True(fixture.Model.Rows[0].IsUnassigned);
        Assert.False(fixture.Model.Rows[0].IsModified);
        Assert.Equal(4, fixture.Model.Rows.Count);
        HotkeySettingsViewModel empty = new(() => [], () => [], _ => throw new InvalidOperationException("must not save"));
        Assert.True(empty.IsEmpty);
        Assert.Equal(HotkeyEditorText.NoActions, empty.EmptyTitle);
    }

    [Fact]
    public void AddingInDialogStartsUnassignedAndEditingSecondBindingPreservesFirst()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            HotkeyEditWindow add = new(fixture.Model, fixture.Model.Rows[0], null);
            add.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal(HotkeyEditorText.Unassigned, ((TextBox)add.FindName("CaptureBox")).Text);
            Assert.False(((Button)add.FindName("SaveButton")).IsEnabled);
            add.HandleKeyInput(Key.O, ModifierKeys.Control | ModifierKeys.Shift, true);
            ((Button)add.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { Key.I, Key.O }, fixture.Values[0].GetBindings().Select(key => key.Key));
            HotkeyEditWindow edit = new(fixture.Model, fixture.Model.Rows[0], 1);
            edit.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.Equal("Ctrl+Shift+O", ((TextBox)edit.FindName("CaptureBox")).Text);
            edit.HandleKeyInput(Key.P, ModifierKeys.Control, true);
            ((Button)edit.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(new[] { Key.I, Key.P }, fixture.Values[0].GetBindings().Select(key => key.Key));
        });
    }

    [Fact]
    public void FailedMultipleBindingSaveKeepsTheDisplayedBindings()
    {
        Fixture fixture = new();
        fixture.Values[0].AdditionalHotkeys = [new(Key.O, ModifierKeys.Control | ModifierKeys.Shift)];
        fixture.Model.Refresh();
        fixture.Fail = true;
        Assert.False(fixture.Model.RemoveBinding(fixture.Model.Rows[0].Bindings[0]));
        Assert.Equal(new[] { Key.I, Key.O }, fixture.Model.Rows[0].Bindings.Select(binding => binding.Key.Key));
        Assert.True(fixture.Model.IsError);
    }

    [Fact]
    public void ConflictIsShownBeforeApplyingAndFailureKeepsOriginalValue()
    {
        Fixture fixture = new();
        Assert.False(fixture.Model.Save(fixture.Model.Rows[0], new(Key.L, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.Empty(fixture.Applied);
        Assert.True(fixture.Model.IsError);
        Assert.Contains("日志", fixture.Model.Status);
        fixture.Fail = true;
        Assert.False(fixture.Model.Save(fixture.Model.Rows[0], new(Key.P, ModifierKeys.Control), HotKeyKinds.Global));
        Assert.Equal(Key.I, fixture.Model.Rows[0].Value.Hotkey.Key);
        Assert.False(fixture.Model.Rows[0].IsGlobal);
        Assert.Contains("injected save failure", fixture.Model.Status);
    }

    [Fact]
    public void CancelingDetachedDialogDoesNotApplyOrMutateTheBinding()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            HotkeyEditWindow dialog = new(fixture.Model, fixture.Model.Rows[0]);
            Assert.Equal(HotkeyInput.Format(new(Key.I, ModifierKeys.Control)), ((TextBox)dialog.FindName("CaptureBox")).Text);
            Assert.False(((Button)dialog.FindName("SaveButton")).IsDefault);
            dialog.Close();
            Assert.Empty(fixture.Applied);
            Assert.Equal(Key.I, fixture.Model.Rows[0].Value.Hotkey.Key);
        });
    }

    [Theory]
    [InlineData(Key.I, ModifierKeys.Control, true)]
    [InlineData(Key.Home, ModifierKeys.None, true)]
    [InlineData(Key.Up, ModifierKeys.None, true)]
    [InlineData(Key.PageDown, ModifierKeys.None, true)]
    [InlineData(Key.Apps, ModifierKeys.Control, false)]
    [InlineData(Key.Clear, ModifierKeys.Control, false)]
    [InlineData(Key.F24, ModifierKeys.None, true)]
    [InlineData(Key.A, ModifierKeys.None, false)]
    [InlineData(Key.A, ModifierKeys.Shift, false)]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control, false)]
    [InlineData(Key.None, ModifierKeys.Control, false)]
    [InlineData(Key.Tab, ModifierKeys.None, false)]
    [InlineData(Key.Escape, ModifierKeys.None, false)]
    public void InputValidationProtectsPlainTypingAndNavigation(Key key, ModifierKeys modifiers, bool valid)
        => Assert.Equal(valid, HotkeyInput.IsValid(new(key, modifiers)));

    [Fact]
    public void PresentationDoesNotChangeSavedIdentityAndUsesExplicitDescriptions()
    {
        HotKeys key = new("选项(_O)", new(Key.I, ModifierKeys.Control), () => { })
        {
            Id = "stable.options", Description = "打开应用设置", Category = "工具", Source = "test"
        };
        HotkeyPresentationInfo info = HotkeyPresentation.For(key);
        Assert.Equal("选项", info.Name);
        Assert.Equal("打开应用设置", info.Description);
        Assert.Equal("选项(_O)", key.Name);
        Assert.Equal("stable.options", key.Id);
        string json = JsonConvert.SerializeObject(key);
        Assert.DoesNotContain("Description", json);
        Assert.DoesNotContain("Category", json);
        Assert.DoesNotContain("DisplayName", json);
    }

    [Fact]
    public void RuntimeAndDefaultKeysDoNotShareMutableProviderObjects()
    {
        Hotkey providerKey = new(Key.I, ModifierKeys.Control);
        HotkeyDefinition definition = new("options", "Options", providerKey, () => { });
        HotKeys first = definition.CreateRuntimeHotKeys();
        HotKeys second = definition.CreateRuntimeHotKeys();
        providerKey.Key = Key.P;
        first.Hotkey.Key = Key.A;
        first.DefaultHotkey.Key = Key.B;
        Assert.Equal(Key.I, definition.DefaultHotkey.Key);
        Assert.Equal(Key.I, second.Hotkey.Key);
        Assert.Equal(Key.I, second.DefaultHotkey.Key);
        Assert.NotSame(second.Hotkey, second.DefaultHotkey);
        HotKeys empty = new();
        empty.DefaultHotkey.Key = Key.F10;
        Assert.True(Hotkey.None.IsEmpty);
    }

    [Fact]
    public void CaptureRestoreFailureLocksRecordingAndCannotBeReenabledByClear()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            int releases = 0;
            int saves = 0;
            HotkeySettingsViewModel model = new(() => fixture.Values, () => fixture.Values, _ => { saves++; return new(); },
                () => new HotkeyCaptureLease(() => { releases++; return new(restoreErrors: [new("test", "restore failed")]); }));
            HotkeyEditWindow dialog = new(model, model.Rows[0]);
            try
            {
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                InvokeHandler(dialog, "Save_Click");
                InvokeHandler(dialog, "Clear_Click");
                Assert.False(((TextBox)dialog.FindName("CaptureBox")).IsEnabled);
                Assert.False(((Button)dialog.FindName("SaveButton")).IsEnabled);
                Assert.False(((Button)dialog.FindName("ClearButton")).IsEnabled);
                Assert.False(((CheckBox)dialog.FindName("GlobalCheckBox")).IsEnabled);
                Assert.True(model.IsError);
                Assert.Contains("restore failed", model.Status);
                Assert.Equal(0, saves);
            }
            finally { dialog.Close(); }
            Assert.Equal(1, releases);
        });
    }

    [Fact]
    public void FailedRefreshOrDefaultReadIsReportedWithoutEscapingClickHandler()
    {
        Fixture fixture = new();
        bool failRead = false;
        HotkeySettingsViewModel model = new(() => failRead ? throw new IOException("snapshot unavailable") : fixture.Values,
            () => throw new IOException("defaults unavailable"), _ => { failRead = true; throw new IOException("save failed"); });
        Assert.False(model.Clear(model.Rows[0]));
        Assert.Contains("save failed", model.Status);
        Assert.False(model.ResetAll());
        Assert.Contains("defaults unavailable", model.Status);
    }

    [Fact]
    public void ValidationUsesTheServiceScopeAndCandidateKindInsteadOfGlobalDuplicateGuessing()
    {
        Fixture fixture = new();
        HotkeySetting? validated = null;
        HotkeySettingsViewModel model = new(() => fixture.Values, () => fixture.Values, _ => new(), validate: settings =>
        {
            validated = Assert.Single(settings);
            return validated.Kinds == HotKeyKinds.Global ? new([new("options", "global conflict")]) : new();
        });
        Assert.Null(model.Validate("options", new(Key.L, ModifierKeys.Control), HotKeyKinds.Windows));
        Assert.Equal("global conflict", model.Validate("options", new(Key.L, ModifierKeys.Control), HotKeyKinds.Global)!.Split(": ").Last());
        Assert.Equal(HotKeyKinds.Global, validated!.Kinds);
    }

    private static void InvokeHandler(HotkeyEditWindow dialog, string method)
        => typeof(HotkeyEditWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, [dialog, new RoutedEventArgs()]);

    [Fact]
    public void RecordingInvalidKeyCannotBeSavedByTogglingScopeAndValidKeyRecovers()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            HotkeyEditWindow dialog = new(fixture.Model, fixture.Model.Rows[0]);
            try
            {
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.True(dialog.HandleKeyInput(Key.A, ModifierKeys.None, true));
                Assert.Equal("A", ((TextBox)dialog.FindName("CaptureBox")).Text);
                Assert.False(((Button)dialog.FindName("SaveButton")).IsEnabled);
                ((CheckBox)dialog.FindName("GlobalCheckBox")).IsChecked = true;
                Assert.False(((Button)dialog.FindName("SaveButton")).IsEnabled);
                InvokeHandler(dialog, "Save_Click");
                Assert.Empty(fixture.Applied);
                Assert.True(dialog.HandleKeyInput(Key.K, ModifierKeys.Control | ModifierKeys.Alt, true));
                Assert.Equal("Ctrl+Alt+K", ((TextBox)dialog.FindName("CaptureBox")).Text);
                Assert.True(((Button)dialog.FindName("SaveButton")).IsEnabled);
                Assert.False(InputMethod.GetIsInputMethodEnabled((TextBox)dialog.FindName("CaptureBox")));
                InvokeHandler(dialog, "Save_Click");
                HotkeySetting saved = Assert.Single(Assert.Single(fixture.Applied));
                Assert.Equal(new(Key.K, ModifierKeys.Control | ModifierKeys.Alt), saved.Hotkey);
                Assert.Equal(HotKeyKinds.Global, saved.Kinds);
            }
            finally { dialog.Close(); }
        });
    }

    [Fact]
    public void RecordingPreservesTabNavigationAndEscapeCancelsWithoutApplying()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            HotkeyEditWindow dialog = new(fixture.Model, fixture.Model.Rows[0]);
            dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
            Assert.False(dialog.HandleKeyInput(Key.Tab, ModifierKeys.None, true));
            Assert.False(dialog.HandleKeyInput(Key.Tab, ModifierKeys.Shift, true));
            Assert.False(dialog.HandleKeyInput(Key.K, ModifierKeys.Control, false));
            Assert.True(dialog.HandleKeyInput(Key.LeftCtrl, ModifierKeys.Control, true));
            Assert.Equal("Ctrl+I", ((TextBox)dialog.FindName("CaptureBox")).Text);
            Assert.True(dialog.HandleKeyInput(Key.F10, ModifierKeys.Alt, true));
            Assert.Equal("Alt+F10", ((TextBox)dialog.FindName("CaptureBox")).Text);
            Assert.True(dialog.HandleKeyInput(Key.Escape, ModifierKeys.None, true));
            Assert.Empty(fixture.Applied);
            Assert.Equal(Key.I, fixture.Values[0].Hotkey.Key);
        });
    }

    [Fact]
    public void RepeatedLoadedDoesNotLeakCaptureLeaseAndValidationFailureDoesNotEscape()
    {
        WpfTestHost.Invoke(() =>
        {
            Fixture fixture = new();
            int starts = 0;
            int releases = 0;
            bool validationFails = false;
            HotkeySettingsViewModel model = new(() => fixture.Values, () => fixture.Values, _ => new(),
                () => { starts++; return new(() => { releases++; return new(); }); },
                _ => validationFails ? throw new IOException("validation unavailable") : new());
            HotkeyEditWindow dialog = new(model, model.Rows[0]);
            try
            {
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                dialog.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                Assert.Equal(1, starts);
                validationFails = true;
                Assert.True(dialog.HandleKeyInput(Key.K, ModifierKeys.Windows, true));
                Assert.False(((Button)dialog.FindName("SaveButton")).IsEnabled);
                Assert.Contains("validation unavailable", ((TextBlock)dialog.FindName("ErrorText")).Text);
            }
            finally { dialog.Close(); }
            Assert.Equal(1, releases);
        });
    }

    [Theory]
    [InlineData(1180, true, "zh-CN")]
    [InlineData(980, true, "zh-CN")]
    [InlineData(980, false, "zh-CN")]
    [InlineData(980, false, "en-US")]
    public void RealSettingsShellFitsAndRendersWithoutLoadingProductionConfiguration(int width, bool dark, string culture)
    {
        WithSettings(width, dark, culture, (window, host, page, fixture) =>
        {
            fixture.Values[0].AdditionalHotkeys = [new(Key.O, ModifierKeys.Control | ModifierKeys.Shift)];
            page.ViewModel.Refresh();
            Layout(host, width);
            Assert.Equal(4, page.ViewModel.Rows.Count);
            Assert.True(page.ActualWidth >= 420);
            foreach (TextBlock text in Descendants(page).OfType<TextBlock>().Where(IsVisible)) AssertTextFits(text);
            TextBlock actionTitle = Descendants(page).OfType<TextBlock>().First(text => text.Text == fixture.Model.Rows[0].Name);
            Assert.Equal(((SolidColorBrush)page.FindResource("GlobalTextBrush")).Color, ((SolidColorBrush)actionTitle.Foreground).Color);
            foreach (Button button in Descendants(page).OfType<Button>().Where(IsVisible))
            {
                Rect bounds = button.TransformToAncestor(page).TransformBounds(new Rect(button.RenderSize));
                Assert.True(bounds.Right <= page.ActualWidth + 1, $"Button overflows: {button.ToolTip}");
                Assert.True(button.ActualHeight >= 30);
            }


            TextBox search = (TextBox)page.FindName("SearchBox");
            search.Text = "Ctrl + L";
            Layout(host, width);
            Assert.Equal("log", Assert.Single(page.ViewModel.Rows).Value.Id);
            search.Text = "does-not-exist";
            Layout(host, width);
            Assert.True(page.ViewModel.IsEmpty);
            Assert.Contains(Descendants(page).OfType<TextBlock>(), text => text.Text == HotkeyEditorText.Empty && IsVisible(text));
            Assert.Empty(fixture.Applied);
        });
    }

    [Fact]
    public void UnassignedUiHasAddButNoDeleteAndFiltersAreInteractive()
    {
        WithSettings(980, true, "zh-CN", (_, host, page, fixture) =>
        {
            HotkeySettingsRow unassigned = page.ViewModel.Rows.Single(row => row.IsUnassigned);
            Assert.Contains(Descendants(page).OfType<Button>(), button => IsVisible(button)
                && ReferenceEquals(button.DataContext, unassigned)
                && System.Windows.Automation.AutomationProperties.GetName(button) == HotkeyEditorText.Add);
            Assert.DoesNotContain(Descendants(page).OfType<Button>(), button => IsVisible(button)
                && button.DataContext is HotkeySettingsBindingRow binding && ReferenceEquals(binding.Owner, unassigned));
            ((ComboBox)page.FindName("StateFilter")).SelectedIndex = 1;
            Layout(host, 980);
            Assert.True(Assert.Single(page.ViewModel.Rows).IsUnassigned);
            TextBox search = (TextBox)page.FindName("SearchBox");
            search.Text = "no-match";
            Layout(host, 980);
            Assert.True(page.ViewModel.IsEmpty);
            Button clearFilters = Descendants(page).OfType<Button>().Single(button => IsVisible(button) && Equals(button.Content, HotkeyEditorText.ClearFilters));
            clearFilters.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(host, 980);
            Assert.Equal(4, page.ViewModel.Rows.Count);
            Assert.False(page.ViewModel.HasFilter);
            Assert.Empty(fixture.Applied);
        });
    }

    [Fact]
    public void DeletingLastBindingShowsUnassignedAndAddingItLeavesThatFilter()
    {
        WithSettings(980, true, "zh-CN", (_, host, page, fixture) =>
        {
            HotkeySettingsBindingRow binding = page.ViewModel.Rows[0].Bindings.Single();
            Button delete = Descendants(page).OfType<Button>().Single(button => IsVisible(button)
                && System.Windows.Automation.AutomationProperties.GetName(button) == binding.ClearLabel);
            delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Layout(host, 980);
            HotkeySettingsRow cleared = page.ViewModel.Rows.Single(row => row.Value.Id == "options");
            Assert.True(cleared.IsUnassigned);
            Assert.Contains(Descendants(page).OfType<Button>(), button => IsVisible(button) && ReferenceEquals(button.DataContext, cleared)
                && System.Windows.Automation.AutomationProperties.GetName(button) == HotkeyEditorText.Add);
            ((ComboBox)page.FindName("StateFilter")).SelectedIndex = 1;
            Layout(host, 980);
            Assert.Contains(page.ViewModel.Rows, row => row.Value.Id == "options");
            Assert.True(page.ViewModel.SaveBinding(cleared, null, new(Key.P, ModifierKeys.Control), HotKeyKinds.Windows));
            Layout(host, 980);
            Assert.DoesNotContain(page.ViewModel.Rows, row => row.Value.Id == "options");
            Assert.True(page.ViewModel.HasStatus);
            Assert.Equal(HotkeyEditorText.Saved, page.ViewModel.Status);
        });
    }

    [Theory]
    [InlineData("zh-CN")]
    [InlineData("en-US")]
    public void LongBindingsStayUsableAtMinimumPageWidth(string culture)
    {
        WithSettings(980, true, culture, (_, host, page, fixture) =>
        {
            fixture.Values[0].AdditionalHotkeys = [new(Key.F24, ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift | ModifierKeys.Windows)];
            page.ViewModel.Refresh();
            HotKeysSetting compact = new(page.ViewModel);
            compact.Measure(new Size(420, double.PositiveInfinity));
            compact.Arrange(new Rect(0, 0, 420, compact.DesiredSize.Height));
            compact.UpdateLayout();
            Assert.Equal(420, compact.ActualWidth);
            foreach (Button button in Descendants(compact).OfType<Button>().Where(IsVisible))
            {
                Rect bounds = button.TransformToAncestor(compact).TransformBounds(new Rect(button.RenderSize));
                Assert.True(bounds.Right <= 421, $"Button overflows at minimum width: {button.Content} {button.ToolTip}, {bounds}");
            }
            foreach (HotkeySettingsBindingRow binding in page.ViewModel.Rows.SelectMany(row => row.Bindings))
            {
                Rect[] buttons = Descendants(compact).OfType<Button>().Where(button => IsVisible(button) && ReferenceEquals(button.DataContext, binding))
                    .Select(button => button.TransformToAncestor(compact).TransformBounds(new Rect(button.RenderSize))).OrderBy(rect => rect.Left).ToArray();
                Assert.Equal(3, buttons.Length);
                Assert.True(buttons[0].Right <= buttons[1].Left + 1, $"Key overlaps edit: {binding.Shortcut}");
                Assert.True(buttons[1].Right <= buttons[2].Left + 1, $"Edit overlaps delete: {binding.Shortcut}");
            }
        });
    }

    private static void WithSettings(int width, bool dark, string culture, Action<SettingWindow, Grid, HotKeysSetting, Fixture> action)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUICulture = CultureInfo.CurrentUICulture;
            CultureInfo? previousResourceCulture = ColorVision.UI.Properties.Resources.Culture;
            ResourceDictionary resources = Application.Current.Resources;
            Dictionary<object, object> locals = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            List<ResourceDictionary> previousDictionaries = resources.MergedDictionaries.ToList();
            Func<HotKeysSetting>? previousFactory = PreviewHotkeysPage.Factory;
            SettingWindow? window = null;
            try
            {
                // Clear local stubs BEFORE loading BasedOn styles; restore all resources after the test.
                resources.Clear();
                resources.MergedDictionaries.Clear();
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture;
                ColorVision.UI.Properties.Resources.Culture = null;
                foreach (string source in new[]
                {
                    $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                    "/HandyControl;component/Themes/Theme.xaml",
                    $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml"
                }) resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                Fixture fixture = new(culture.StartsWith("en", StringComparison.Ordinal));
                // Show customized + unassigned states without touching real runtime/configuration.
                fixture.Values[2].Hotkey = new(Key.None, ModifierKeys.None);
                fixture.Values[3].Kinds = HotKeyKinds.Global;
                fixture.Model.Refresh();
                PreviewHotkeysPage.Factory = () => new HotKeysSetting(fixture.Model);
                ConfigSettingMetadata[] settings = [new() { Name = HotkeyEditorText.Title, Type = ConfigSettingType.TabItem, ViewType = typeof(PreviewHotkeysPage) }];
                ConstructorInfo constructor = typeof(SettingWindow).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(IEnumerable<ConfigSettingMetadata>)], null)!;
                window = (SettingWindow)constructor.Invoke([settings]);
                Grid host = Assert.IsType<Grid>(window.Content);
                Layout(host, width);
                HotKeysSetting page = Assert.Single(Descendants(host).OfType<HotKeysSetting>());
                action(window, host, page, fixture);
            }
            finally
            {
                window?.Close();
                PreviewHotkeysPage.Factory = previousFactory;
                resources.Clear();
                resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary dictionary in previousDictionaries) resources.MergedDictionaries.Add(dictionary);
                foreach ((object key, object value) in locals) resources[key] = value;
                ColorVision.UI.Properties.Resources.Culture = previousResourceCulture;
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUICulture;
            }
        });
    }

    private static void Layout(FrameworkElement host, int width)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            host.Measure(new Size(width, 760));
            host.Arrange(new Rect(0, 0, width, 760));
            host.UpdateLayout();
            host.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }
    }

    private static bool IsVisible(FrameworkElement element)
    {
        for (DependencyObject? current = element; current != null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        return element.ActualWidth > 0 && element.ActualHeight > 0;
    }

    private static void AssertTextFits(TextBlock text)
    {
        FormattedText measured = new(text.Text, CultureInfo.CurrentUICulture, text.FlowDirection,
            new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize,
            text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
        if (text.TextWrapping == TextWrapping.NoWrap) Assert.True(measured.WidthIncludingTrailingWhitespace <= text.ActualWidth + 2, $"Clipped: {text.Text}");
        else
        {
            measured.MaxTextWidth = Math.Max(1, text.ActualWidth);
            Assert.True(measured.Height <= text.ActualHeight + 3, $"Clipped wrapped text: {text.Text}");
        }
    }

    private static IEnumerable<FrameworkElement> Descendants(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement element) yield return element;
            foreach (FrameworkElement descendant in Descendants(child)) yield return descendant;
        }
    }

    public sealed class PreviewHotkeysPage : UserControl
    {
        internal static Func<HotKeysSetting>? Factory;
        public PreviewHotkeysPage() => Content = Factory!();
    }

    private sealed class Fixture
    {
        internal List<HotKeys> Values { get; }
        internal List<List<HotkeySetting>> Applied { get; } = new();
        internal bool Fail { get; set; }
        internal HotkeySettingsViewModel Model { get; }
        internal Fixture(bool english = false)
        {
            Values =
            [
                KeyValue("options", english ? "Options" : "设置", english ? "Open application settings and preferences." : "打开应用设置，调整界面与功能偏好。", Key.I),
                KeyValue("log", english ? "Log" : "日志", english ? "Open the application log viewer." : "查看运行日志与诊断信息。", Key.L),
                KeyValue("update", english ? "Check for updates" : "检查更新", english ? "Check for application updates." : "检查是否有可用的新版本。", Key.U),
                KeyValue("status", english ? "Status bar" : "状态栏", english ? "Show or hide the main window status bar." : "显示或隐藏主窗口底部的状态栏。", Key.B, ModifierKeys.Control | ModifierKeys.Shift)
            ];
            Model = new(() => Values.Select(Clone).ToList(), () => Values.Select(value => { HotKeys copy = Clone(value); copy.SetBindings(copy.GetDefaultBindings()); copy.Kinds = copy.DefaultKinds; return copy; }).ToList(), settings =>
            {
                List<HotkeySetting> changes = settings.ToList();
                Applied.Add(changes);
                if (Fail) return new HotkeyApplyResult([new("test", "injected save failure")]);
                foreach (HotkeySetting setting in changes)
                {
                    HotKeys value = Values.Single(key => key.Id == setting.Id);
                    value.SetBindings(setting.GetBindings());
                    value.Kinds = setting.Kinds;
                }
                return new HotkeyApplyResult();
            });
        }
        private static HotKeys KeyValue(string id, string name, string description, Key key, ModifierKeys modifiers = ModifierKeys.Control)
            => new(name, new(key, modifiers), () => { }) { Id = id, Description = description, Category = "Tools", Source = "ColorVision.UI" };
        private static HotKeys Clone(HotKeys key) => new()
        {
            Id = key.Id, Name = key.Name, Description = key.Description, Category = key.Category, Source = key.Source,
            Hotkey = new(key.Hotkey.Key, key.Hotkey.Modifiers), DefaultHotkey = new(key.DefaultHotkey.Key, key.DefaultHotkey.Modifiers),
            AdditionalHotkeys = key.AdditionalHotkeys, DefaultAdditionalHotkeys = key.DefaultAdditionalHotkeys,
            Kinds = key.Kinds, DefaultKinds = key.DefaultKinds
        };
    }
}
