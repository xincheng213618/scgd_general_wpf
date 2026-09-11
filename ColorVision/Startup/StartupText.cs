using System;
using System.Collections.Generic;
using System.Globalization;
using System.Resources;

namespace ColorVision.Startup;

/// <summary>Localized startup presentation without changing initializer execution or application settings.</summary>
public static class StartupText
{
    private static readonly ResourceManager Resources = new("ColorVision.Startup.StartupResources", typeof(StartupText).Assembly);
    private static readonly string[] HeadlineResourceNames =
    [
        nameof(Headline),
        "Headline2",
        "Headline3",
        "Headline4",
        "Headline5",
        "Headline6"
    ];

    private static string Get(string name)
    {
        CultureInfo culture = CultureInfo.CurrentUICulture;
        // Preserve Chinese script/region fallback; other languages use the English presentation.
        if (culture.TwoLetterISOLanguageName != "zh") culture = CultureInfo.GetCultureInfo("en");
        return Resources.GetString(name, culture) ?? name;
    }

    public static string Title => Get(nameof(Title));
    public static string Headline => Get(nameof(Headline));
    public static IReadOnlyList<string> Headlines => Array.ConvertAll(HeadlineResourceNames, Get);
    public static string Capabilities => Get(nameof(Capabilities));
    public static string PreparingWorkspace => Get(nameof(PreparingWorkspace));
    public static string ConnectingServices => Get(nameof(ConnectingServices));
    public static string LoadingWorkspace => Get(nameof(LoadingWorkspace));
    public static string LoadingTemplates => Get(nameof(LoadingTemplates));
    public static string PreparingDevicePanels => Get(nameof(PreparingDevicePanels));
    public static string PreparingCompute => Get(nameof(PreparingCompute));
    public static string LoadingExtensions => Get(nameof(LoadingExtensions));
    public static string OpeningWorkspace => Get(nameof(OpeningWorkspace));
    public static string ProgressAutomationName => Get(nameof(ProgressAutomationName));

    public static string GetRandomHeadline()
    {
        IReadOnlyList<string> headlines = Headlines;
        return headlines[Random.Shared.Next(headlines.Count)];
    }

    public static string GetStage(string initializerTypeName) => initializerTypeName switch
    {
        "MySqlInitializer" or "MqttInitializer" or "RCInitializer" or "SocketInitializer" => ConnectingServices,
        "SolutionManagerInitializer" => LoadingWorkspace,
        "TemplateInitializer" => LoadingTemplates,
        "ServiceInitializer" => PreparingDevicePanels,
        "CudaInitializer" => PreparingCompute,
        _ => LoadingExtensions
    };
}
