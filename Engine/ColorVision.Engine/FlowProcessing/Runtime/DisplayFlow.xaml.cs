#pragma warning disable CS8601
using ColorVision.Engine.Services.RC;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing;

public partial class DisplayFlow : UserControl, IDisPlayControl, IIcon, IDisposable
{
    private static readonly ILog log = LogManager.GetLogger(typeof(DisplayFlow));
    private static readonly string[] RestartServiceNames =
        ["RegistrationCenterService", "CVMainService_x64", "CVMainService_dev"];

    public ViewFlow View => FlowEngineManager.View;
    public FlowEngineManager FlowEngineManager { get; }
    public string DisPlayName => "Flow";
    public ImageSource Icon { get; set; }

    public event RoutedEventHandler? Selected;
    public event RoutedEventHandler? Unselected;
    public event EventHandler? SelectChanged;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            SelectChanged?.Invoke(this, new RoutedEventArgs());
            if (value)
                Selected?.Invoke(this, new RoutedEventArgs());
            else
                Unselected?.Invoke(this, new RoutedEventArgs());
        }
    }

    public DisplayFlow(FlowEngineManager flowEngineManager)
    {
        FlowEngineManager = flowEngineManager;
        InitializeComponent();
    }

    private void UserControl_Initialized(object sender, EventArgs e)
    {
        DataContext = FlowEngineManager;
        this.AddViewConfig(View, Properties.Resources.Workflow);
        Unselected += (_, _) => View.EditorCanvas.HideNodePropertyPanel();
        this.ApplyChangedSelectedColor(DisPlayBorder);
        EnsureTimedButtonOperations();
        ServiceConfig.Instance.PropertyChanged += ServiceConfig_PropertyChanged;
    }

    private TimedButtonOperationRegistry EnsureTimedButtonOperations()
    {
        TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(actionKey => $"flow:{actionKey}");
        operations.Register(RestartServicesButton, "restart-cv-windows-services", options =>
        {
            options.ContentFactory = stats =>
                TimedButtonOperationTextFormatter.BuildCompactContent(BuildRestartServicesButtonText(), stats);
            options.ToolTipFactory = stats =>
                TimedButtonOperationTextFormatter.BuildTooltip(BuildRestartServicesButtonText(), stats);
            options.RunningText = Properties.Resources.RestartService;
        });
        return operations;
    }

    private static string BuildRestartServicesButtonText()
    {
        string version = ServiceConfig.Instance.RegistrationCenterServiceInfo.FileVersion;
        return string.IsNullOrWhiteSpace(version)
            ? Properties.Resources.RestartService
            : string.Format(Properties.Resources.Flow_RestartServiceVersionFormat, version);
    }

    private void ServiceConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(
                e.PropertyName,
                nameof(ServiceConfig.RegistrationCenterServiceInfo),
                StringComparison.Ordinal))
            return;

        if (Dispatcher.CheckAccess())
            RefreshRestartServicesButton();
        else
            _ = Dispatcher.BeginInvoke(RefreshRestartServicesButton);
    }

    private void RefreshRestartServicesButton()
    {
        this.TryGetTimedButtonOperations()?.RefreshIdleState(RestartServicesButton);
    }

    private double GetExpectedRestartDurationMs()
    {
        TimedButtonOperationStats? stats =
            EnsureTimedButtonOperations().Get(RestartServicesButton)?.CurrentStats;
        if (stats?.SuccessCount > 0 && stats.AverageElapsedMs > 0)
            return stats.AverageElapsedMs;
        if (stats?.WarmupCount > 0 && stats.WarmupElapsedMs > 0)
            return stats.WarmupElapsedMs;
        return 15000;
    }

    private async void Button_RestartServices_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox1.Show(
                Application.Current.GetActiveWindow(),
                Properties.Resources.Flow_ConfirmRestartColorVisionServices,
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        TimedButtonOperationScope? operationScope = EnsureTimedButtonOperations().Begin(
            RestartServicesButton,
            GetExpectedRestartDurationMs(),
            Properties.Resources.RestartService);
        bool success = false;
        try
        {
            await RestartColorVisionServicesAsync();
            success = true;
        }
        catch (Exception ex)
        {
            log.Error("重启 ColorVision 服务失败", ex);
            MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
        }
        finally
        {
            operationScope?.Complete(success);
            this.TryGetTimedButtonOperations()?.RefreshIdleState(RestartServicesButton);
        }
    }

    public static async Task RestartColorVisionServicesAsync()
    {
        foreach (string serviceName in RestartServiceNames)
            await RunServiceHostCommandAsync(serviceName, start: false);

        await Task.Delay(1000);

        foreach (string serviceName in RestartServiceNames)
            await RunServiceHostCommandAsync(serviceName, start: true);

        await Task.Delay(1000);
        await RefreshServiceConnectionAsync();
    }

    private static async Task RunServiceHostCommandAsync(string serviceName, bool start)
    {
        ServiceHostResponse response = start
            ? await ColorVisionServiceHostClient.Default.StartServiceAsync(
                serviceName,
                timeoutSeconds: 45,
                timeout: TimeSpan.FromSeconds(60))
            : await ColorVisionServiceHostClient.Default.StopServiceAsync(
                serviceName,
                timeoutSeconds: 45,
                timeout: TimeSpan.FromSeconds(60));

        if (!response.Success)
            throw new InvalidOperationException(
                string.Format(
                    start
                        ? Properties.Resources.Flow_StartServiceFailed
                        : Properties.Resources.Flow_StopServiceFailed,
                    serviceName,
                    response.Message));
    }

    private static async Task RefreshServiceConnectionAsync()
    {
        ServiceConfig.Instance.RefreshInstalledServices();
        MqttRCService rcService = MqttRCService.GetInstance();
        rcService.Regist();
        for (int i = 0; i < 20 && !rcService.IsConnect; i++)
            await Task.Delay(250);

        if (rcService.IsConnect)
            rcService.QueryServices();
        else
            log.Warn("服务重启完成，但注册中心重新连接未确认。");
    }

    public void Dispose()
    {
        ServiceConfig.Instance.PropertyChanged -= ServiceConfig_PropertyChanged;
        this.DisposeTimedButtonOperations();
        GC.SuppressFinalize(this);
    }
}
