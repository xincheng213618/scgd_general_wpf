using ColorVision.UI.Desktop.Operations;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace ColorVision.UI.Desktop.LanRemote;

internal static class LanRemoteText
{
    internal static string Get(string key) => Properties.Resources.ResourceManager.GetString("LanRemote_" + key, CultureInfo.CurrentUICulture) ?? key;
    internal static string Format(string key, params object[] arguments) => string.Format(CultureInfo.CurrentCulture, Get(key), arguments);

    internal static string JobKey(string capability) => capability switch
    {
        "ops.window.snapshot.capture" => "Snapshot",
        "ops.diagnostics.bundle.create" => "Diagnostics",
        "ops.service.restart" => "ServiceRestart",
        "ops.application.restart" => "ApplicationRestart",
        "ops.messaging.reconnect" => "Reconnect",
        "ops.flow.cancel" => "CancelFlow",
        _ => "Other",
    };
}

// The application applies language changes on restart, as with its other settings pages.
public sealed class LanRemoteTextExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => LanRemoteText.Get(key);
}

public sealed class LanRemoteItemTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        OperationsJob job => LanRemoteText.Get($"Job{parameter}_{LanRemoteText.JobKey(job.CapabilityId)}"),
        OperationsSupportSession session => session.Mode switch
        {
            "diagnostics" => LanRemoteText.Get("SupportDiagnostics"),
            "guided" => LanRemoteText.Get("SupportGuided"),
            _ => session.Mode,
        },
        _ => string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
