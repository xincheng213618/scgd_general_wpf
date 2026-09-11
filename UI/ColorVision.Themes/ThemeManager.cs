using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Themes;

public delegate void ThemeChangedHandler(Theme newtheme);

public partial class ThemeManager : IDisposable
{
    public static ThemeManager Current { get; set; } = new();
    public static ReadOnlyCollection<Theme> SupportedThemes { get; } = Array.AsReadOnly(new[] { Theme.UseSystem, Theme.Light, Theme.Dark });
    public static Theme NormalizeTheme(Theme theme) => theme is Theme.UseSystem or Theme.Light or Theme.Dark ? theme : Theme.UseSystem;

    // Kept for existing package hosts. List changes take effect on the next resource application.
    public static List<string> ResourceDictionaryBase { get; set; } = new()
    {
        "/ColorVision.Themes;component/Themes/Base.xaml",
        "/ColorVision.Themes;component/Themes/Menu.xaml",
        "/ColorVision.Themes;component/Themes/GroupBox.xaml",
        "/ColorVision.Themes;component/Themes/Icons.xaml",
        "/ColorVision.Themes;component/Themes/Window/BaseWindow.xaml"
    };
    public static List<string> ResourceDictionaryDark { get; set; } = new()
    {
        "/HandyControl;component/Themes/basic/colors/colorsdark.xaml",
        "/HandyControl;component/Themes/Theme.xaml",
        "/ColorVision.Themes;component/Themes/Dark.xaml"
    };
    public static List<string> ResourceDictionaryWhite { get; set; } = new()
    {
        "/HandyControl;component/Themes/basic/colors/colors.xaml",
        "/HandyControl;component/Themes/Theme.xaml",
        "/ColorVision.Themes;component/Themes/White.xaml"
    };

    private readonly CancellationTokenSource monitorCancellation = new();
    private Application? application;
    private ThemeResourceDictionary? appliedResources;
    private bool monitoring;
    private bool monitorScheduled;
    private bool disposed;

    /// <summary>The requested policy. Resources are applied before either theme notification is published.</summary>
    public Theme? CurrentTheme { get; private set; } = Theme.Light;
    public Theme CurrentUITheme { get; private set; } = Theme.Light;
    public event ThemeChangedHandler? CurrentThemeChanged;
    public event ThemeChangedHandler? CurrentUIThemeChanged;
    public event ThemeChangedHandler? AppsThemeChanged;
    public event ThemeChangedHandler? SystemThemeChanged;

    private Theme appsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;
    public Theme AppsTheme
    {
        get => appsTheme;
        set => Dispatch(() =>
        {
            if (appsTheme == value) return;
            appsTheme = value;
            if (CurrentTheme == Theme.UseSystem && application != null)
                ApplyCore(application, Theme.UseSystem, false, false);
            AppsThemeChanged?.Invoke(value);
        });
    }
    private Theme systemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;
    public Theme SystemTheme
    {
        get => systemTheme;
        set => Dispatch(() =>
        {
            if (systemTheme == value) return;
            systemTheme = value;
            SystemThemeChanged?.Invoke(value);
        });
    }

    public void ApplyTheme(Application app, Theme theme) => app.Dispatcher.Invoke(() => ApplyCore(app, theme, true, false));
    /// <summary>Refresh resources, resolving UseSystem, without changing the selected policy.</summary>
    public void ApplyThemeChanged(Application app, Theme theme) => app.Dispatcher.Invoke(() => ApplyCore(app, theme, false, true));

    private void ApplyCore(Application app, Theme requested, bool updateSelection, bool force)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        requested = NormalizeTheme(requested);
        Theme resolved = requested == Theme.UseSystem ? (AppsTheme == Theme.Dark ? Theme.Dark : Theme.Light) : requested;
        var dictionaries = app.Resources.MergedDictionaries;
        if (force || appliedResources == null || appliedResources.AppliedTheme != resolved || !dictionaries.Contains(appliedResources))
        {
            // Loading can throw. Keep active resources and published state until preparation succeeds.
            var replacement = new ThemeResourceDictionary(resolved);
            int first = -1;
            for (int i = 0; i < dictionaries.Count; i++)
                if (ThemeResourceDictionary.IsThemeResource(dictionaries[i]) && first < 0) first = i;
            if (first < 0)
                dictionaries.Insert(0, replacement);
            else
            {
                dictionaries[first] = replacement;
                for (int i = dictionaries.Count - 1; i > first; i--)
                    if (ThemeResourceDictionary.IsThemeResource(dictionaries[i])) dictionaries.RemoveAt(i);
            }
            appliedResources = replacement;
        }
        if (application != app)
        {
            if (application != null) application.Exit -= Application_Exit;
            application = app;
            app.Exit += Application_Exit;
        }
        bool selectionChanged = updateSelection && CurrentTheme != requested;
        bool actualChanged = CurrentUITheme != resolved;
        if (updateSelection) CurrentTheme = requested;
        CurrentUITheme = resolved;
        if (!monitorScheduled)
        {
            monitorScheduled = true;
            _ = InitializeMonitoringAsync();
        }
        if (selectionChanged) CurrentThemeChanged?.Invoke(requested);
        if (actualChanged) CurrentUIThemeChanged?.Invoke(resolved);
    }

    private void Dispatch(Action action)
    {
        var dispatcher = application?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else if (!dispatcher.HasShutdownStarted) dispatcher.Invoke(action);
    }

    private async Task InitializeMonitoringAsync()
    {
        try
        {
            // SystemEvents initialization is deferred to keep it off the startup path.
            await Task.Delay(TimeSpan.FromSeconds(10), monitorCancellation.Token).ConfigureAwait(false);
            Dispatch(() =>
            {
                if (disposed) return;
                SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
                SystemParameters.StaticPropertyChanged += SystemPropertyChanged;
                monitoring = true;
                RefreshSystemThemes();
            });
        }
        catch (OperationCanceledException) { }
    }

    private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) => QueueSystemRefresh();
    private void SystemPropertyChanged(object? sender, PropertyChangedEventArgs e) => QueueSystemRefresh();
    private void QueueSystemRefresh()
    {
        var dispatcher = application?.Dispatcher ?? Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted)
            dispatcher.BeginInvoke(() => { if (!disposed) RefreshSystemThemes(); });
    }
    private void RefreshSystemThemes()
    {
        AppsTheme = AppsUseLightTheme() ? Theme.Light : Theme.Dark;
        SystemTheme = SystemUsesLightTheme() ? Theme.Light : Theme.Dark;
    }
    public static bool AppsUseLightTheme() => ReadLightTheme("AppsUseLightTheme");
    public static bool SystemUsesLightTheme() => ReadLightTheme("SystemUsesLightTheme");
    private static bool ReadLightTheme(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue(valueName) is not int value || value > 0;
    }
    private void Application_Exit(object sender, ExitEventArgs e) => Dispose();
    public void Dispose()
    {
        Dispatch(() =>
        {
            if (disposed) return;
            disposed = true;
            monitorCancellation.Cancel();
            if (monitoring)
            {
                SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
                SystemParameters.StaticPropertyChanged -= SystemPropertyChanged;
                monitoring = false;
            }
            if (application != null) application.Exit -= Application_Exit;
            application = null;
            appliedResources = null;
            monitorCancellation.Dispose();
        });
        GC.SuppressFinalize(this);
    }
}
