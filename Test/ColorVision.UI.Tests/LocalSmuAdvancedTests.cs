using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Engine.Services.Devices.SMU.Local;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Themes;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.End;
using ST.Library.UI.NodeContainer;
using SqlSugar;

namespace ColorVision.UI.Tests;

public sealed class LocalSmuAdvancedTests
{
    // Protect the versioned native ABI and the existing mixed-unit storage boundary.
    [Fact]
    public async Task AdvancedResultsRetainNativeOhmsAndConvertCurrentExactlyOnce()
    {
        Assert.Equal(176, Marshal.SizeOf<SmuNativeSettings>());
        Assert.Equal(88, Marshal.OffsetOf<SmuNativeSettings>(nameof(SmuNativeSettings.SourceRange)).ToInt32());
        var native = new FakeNative();
        await using var session = new LocalSmuSession(native);
        var connection = Connection(); await session.OpenAsync(connection);
        var settings = new LocalSmuSettings { IsSourceV = false, SourceValue = 10, Limit = 5,
            Measurement = SmuMeasurement.Resistance, Sweep = SmuSweep.List, Values = "10,20,10", CloseOutput = false };
        var result = await session.CaptureAdvancedAsync(connection, settings, true);
        Assert.Equal(new double[] { 10, 10, 10 }, result.Currents);
        Assert.Equal(new double?[] { 501, 502, null }, result.Details!.Resistance);
        var response = LocalSmuResultService.Response(result, 0);
        Assert.Equal(new double[] { .01, .01, .01 }, response["IList"]!.ToObject<double[]>());
        Assert.Equal(new double[] { .01, .02, .01 }, response["ScanList"]!.ToObject<double[]>());
        Assert.Equal(new double[] { .05, .05, .05 }, response["Power"]!.ToObject<double[]>());
        Assert.Equal(new double[] { .01, .01, .01 }, JsonConvert.DeserializeObject<double[]>(LocalSmuResultService.CreateScan(result,"test").IResult!));
        var restored = JsonConvert.DeserializeObject<LocalSmuDetails>(LocalSmuResultService.CreateScan(result,"test").LocalDetails!)!;
        Assert.Equal(settings.Measurement, restored.Settings.Measurement);
        Assert.Equal(result.Details.Resistance, restored.Resistance);
        var view=LocalSmuResultService.CreateView(result,"test",0);
        Assert.Equal(501,view.SMUDatas[0].Resistance);
        Assert.Null(view.SMUDatas[2].Resistance);
        Assert.Empty(native.ClosedChannels);
        var interop = SmuNativeSettings.From(settings,3);
        Assert.Equal(5,interop.Limit); Assert.Equal(0.01,interop.Nplc);
    }

    [Fact]
    public async Task SourceOnlyDoesNotProduceInventedMeasurementsOrResults()
    {
        var native = new FakeNative(); await using var session = new LocalSmuSession(native);
        var connection = Connection(); await session.OpenAsync(connection);
        var result = await session.CaptureAdvancedAsync(connection,new() { Operation=SmuOperation.SourceOnly,CloseOutput=false },true);
        Assert.False(result.HasMeasurements); Assert.Empty(result.Voltages);Assert.Empty(result.Currents);
        Assert.Equal(0,LocalSmuResultService.Save(result,"test"));
        Assert.False(LocalSmuResultService.Response(result,0).Value<bool>("HasMeasurements"));
    }

    [Fact]
    public void LegacyDatabaseQueriesDoNotRequireNewColumns()
    {
        using var db=new SqlSugarClient(new ConnectionConfig { ConnectionString="Server=127.0.0.1;Database=offline;User ID=offline;Password=offline;",DbType=DbType.MySql,IsAutoCloseConnection=true });
        Assert.DoesNotContain("local_details",db.Queryable<ColorVision.Engine.Services.Devices.SMU.Dao.SMUResultModel>().ToSql().Key,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local_details",db.Queryable<ColorVision.Engine.Services.Devices.SMU.Dao.SmuScanModel>().ToSql().Key,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local_details",db.Insertable(new ColorVision.Engine.Services.Devices.SMU.Dao.SMUResultModel { LocalDetails="{}" }).ToSql().Key,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("local_details",db.Insertable(new ColorVision.Engine.Services.Devices.SMU.Dao.SmuScanModel { LocalDetails="{}" }).ToSql().Key,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdvancedCancellationKeepsHandleUntilSdkReturnsAndCannotBypassFlowOwner()
    {
        var native=new FakeNative { Block=true };await using var session=new LocalSmuSession(native);
        var connection=Connection();await session.OpenAsync(connection);
        object owner=new(); using var lease=await session.AcquireFlowLeaseAsync(owner,()=>false);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>session.CaptureAdvancedAsync(connection,new(),true));
        lease.ConfigureChannel(Channel.A,true);
        using var cancellation=new CancellationTokenSource();
        var settings=new LocalSmuSettings { CloseOutput=false };
        var task=session.CaptureAdvancedAsync(connection,settings,true,cancellation.Token,owner);
        await native.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        settings.SourceValue=99;cancellation.Cancel();
        Assert.False(task.IsCompleted);Assert.Empty(native.ClosedChannels);Assert.Equal(0,native.Closes);
        native.Release.TrySetResult();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);
        Assert.Equal(1,native.Source);Assert.Contains(Channel.A,native.ClosedChannels);Assert.Equal(1,native.Closes);
    }

    [Fact]
    public async Task InvalidModesAndOldDllAreRejectedBeforeMeasurement()
    {
        var native=new FakeNative { Family=0 };await using var session=new LocalSmuSession(native);
        var connection=Connection();await session.OpenAsync(connection);
        await Assert.ThrowsAsync<NotSupportedException>(()=>session.CaptureAdvancedAsync(connection,new(),true));
        Assert.Equal(0,native.Captures);Assert.Empty(native.ClosedChannels);
        var log=new LocalSmuSettings { Sweep=SmuSweep.Logarithmic,Start=-1,Stop=-100,Points=3 };
        Assert.Equal(new double[]{-1,-10,-100},log.SourceValues(),new DoubleComparer());
        log.Stop=1;Assert.Throws<ArgumentException>(()=>log.SourceValues());
        Assert.Throws<ArgumentException>(()=>new LocalSmuSettings { Channel=Channel.B }.ValidateFamily(1));
        Assert.Throws<ArgumentException>(()=>new LocalSmuSettings { OhmsMode=SmuOhmsMode.Auto }.ValidateFamily(4));
        Assert.Throws<ArgumentException>(()=>new LocalSmuSettings { Waveform=SmuWaveform.Pulse }.ValidateFamily(4));
    }

    [Fact]
    public async Task HighImpedanceCloseDoesNotRunMeasurementAndRestoresLegacyReadBoundary()
    {
        var native=new FakeNative();await using var session=new LocalSmuSession(native);
        var connection=Connection();await session.OpenAsync(connection);
        var settings=new LocalSmuSettings { OffMode=SmuOffMode.HighImpedance };
        await session.CloseAdvancedOutputAsync(settings,CancellationToken.None,null);
        Assert.Equal(0,native.Captures);Assert.Equal(SmuOffMode.HighImpedance,native.OffMode);Assert.Contains(Channel.A,native.ClosedChannels);
        await session.CaptureAdvancedAsync(connection,settings,true);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>session.CaptureAsync(connection,new(){LimitValue=10},false,false,true,read:true));
    }

    [Theory]
    [InlineData(SmuWaveform.DC)]
    [InlineData(SmuWaveform.Pulse)]
    public async Task AdvancedReconnectNeverReplaysFailedMeasurementsOrPulses(SmuWaveform waveform)
    {
        var native = new FakeNative();
        await using var session = new LocalSmuSession(native);
        var connection = Connection();
        var settings = new LocalSmuSettings { Waveform = waveform, CloseOutput = false,
            SourceRange = 6, MeasureRange = 0.1, AutoZero = SmuAutoZero.Off };
        await session.OpenAsync(connection);
        await session.CaptureAdvancedAsync(connection, settings, true);
        native.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAdvancedAsync(
            connection with { BaudRate = 115200 }, settings, true));
        Assert.Equal(2, native.Opens);
        Assert.Equal(1, native.Closes);
        Assert.Equal(2, native.Captures);
        Assert.Equal(2, native.ClosedChannels.Count);
        await session.CloseAdvancedOutputAsync(settings, CancellationToken.None, null);
        Assert.Equal(2, native.Opens);
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task AdvancedNodeRunsItsOwnModesAndClosesAtWholeFlowEnd(bool sourceOnly,bool fails)
    {
        var native=new FakeNative { Fail=fails };var session=new LocalSmuSession(native);
        var dictionaries=new List<ResourceDictionary>();
        var managerField=typeof(ServiceManager).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic)!;
        object? previousManager=null;IConfigService? previousConfig=null;
        DeviceSMU? device=null;CVNodeContainer? container=null;FlowEngineControl? control=null;ProbeEnd? end=null;RuntimeTestStartNode? start=null;
        var completion=new TaskCompletionSource<FlowEngineEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            WpfTestHost.Invoke(()=>
            {
                previousManager=managerField.GetValue(null);previousConfig=ConfigService.Instance;
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave=false });
                foreach(string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                { var d=new ResourceDictionary { Source=new Uri(uri,UriKind.RelativeOrAbsolute) };dictionaries.Add(d);Application.Current.Resources.MergedDictionaries.Add(d); }
                var manager=(ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));manager.DeviceServices=[];managerField.SetValue(null,manager);
                device=new DeviceSMU(new(){Id=1,Code="advanced-smu-flow-"+Guid.NewGuid(),Value=JsonConvert.SerializeObject(new ConfigSMU{DevName="synthetic-"+Guid.NewGuid(),DevType=Pss_Type.Keithley_2600})},session);
                manager.DeviceServices.Add(device);
                var node=new LocalSmuNode { DeviceCode=device.Code,CloseOutputOnFinish=true,LocalSettings=new()
                { Operation=sourceOnly?SmuOperation.SourceOnly:SmuOperation.SourceMeasure,Measurement=SmuMeasurement.Resistance,
                    Sweep=sourceOnly?SmuSweep.Fixed:SmuSweep.List,Values="1,2,3",CloseOutput=true } };
                container=new CVNodeContainer();start=new RuntimeTestStartNode();end=new ProbeEnd();
                foreach(var item in new STNode[]{start,node,end}){item.Create();container.Nodes.Add(item);}
                Assert.Equal(ConnectionStatus.Connected,start.m_op_start.ConnectOption(node.GetAllInputOptions()[0],false));
                Assert.Equal(ConnectionStatus.Connected,node.GetAllOutputOptions()[0].ConnectOption(end.m_in_start,false));
                control=new FlowEngineControl(container,false,new FlowNodeManager()){PersistResults=false};
                control.Finished+=(_,result)=>completion.TrySetResult(result);
                Assert.True(control.TryStartNode(start.NodeName,"advanced-smu-flow"));
            });
            var result=await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(fails?StatusTypeEnum.Failed:StatusTypeEnum.Completed,result.Status);
            Assert.Equal(1,native.Captures);Assert.False(native.RequestedCloseOutput);
            Assert.Equal(new[]{Channel.A},native.ClosedChannels);Assert.Equal(0,start!.PublishCount);
            if(fails)Assert.Equal(0,end!.Calls);
            else
            {
                Assert.Equal(1,end!.Calls);Assert.Equal(!sourceOnly,end.HasPoint);
                Assert.Equal(!sourceOnly,end.HasScan);
                Assert.Equal(sourceOnly?1:3,end.Capture!.Details!.SourceValues.Length);
            }
        }
        finally
        {
            WpfTestHost.Invoke(()=>
            {
                control?.Dispose();container?.Dispose();device?.Dispose();
                if(device!=null)DisplayConfigManager.Instance.Configs.TryRemove(device.Code,out _);
                managerField.SetValue(null,previousManager);foreach(var d in dictionaries)Application.Current.Resources.MergedDictionaries.Remove(d);
                if(previousConfig!=null)ConfigService.SetInstance(previousConfig);
            });
            await session.DisposeAsync();
        }
    }

    private sealed class ProbeEnd : CVEndNode
    {
        internal bool HasPoint,HasScan;internal int Calls;internal LocalSmuCapture? Capture;
        protected override void DoNodeEnded(CVStartCFC action)
        {
            // End nodes also receive finish/reset notifications; count only downstream execution.
            if (action.IsRunning)
            {
                Calls++;
                HasPoint=action.Data.ContainsKey("SMUResult");HasScan=action.Data.ContainsKey("LocalSmuScan");
                action.Data.TryGetValue("LocalSmuScan",out var scan);action.Data.TryGetValue("LocalSmu",out var point);
                Capture=scan as LocalSmuCapture??point as LocalSmuCapture;
            }
            base.DoNodeEnded(action);
        }
    }

    // Verify a real WPF window, the device context-menu entry, draft isolation, and binary flow persistence.
    [Fact]
    public void WindowUsesNodeDraftAndSavesModesInFlowWithoutOpeningHardware()
    {
        WpfTestHost.Invoke(()=>
        {
            var dictionaries=new List<ResourceDictionary>();
            var managerField=typeof(ServiceManager).GetField("_instance",BindingFlags.Static|BindingFlags.NonPublic)!;
            var previousManager=managerField.GetValue(null);var previousConfig=ConfigService.Instance;
            DeviceSMU? device=null;LocalSmuWindow? window=null;
            try
            {
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave=false });
                foreach(string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                { var dictionary=new ResourceDictionary { Source=new Uri(uri,UriKind.RelativeOrAbsolute) };dictionaries.Add(dictionary);Application.Current.Resources.MergedDictionaries.Add(dictionary); }
                var manager=(ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));manager.DeviceServices=[];managerField.SetValue(null,manager);
                var native=new FakeNative();
                device=new DeviceSMU(new(){Id=1,Code="local-smu-window-"+Guid.NewGuid(),Value=JsonConvert.SerializeObject(new ConfigSMU{DevName="synthetic-"+Guid.NewGuid(),DevType=Pss_Type.Keithley_2600})},new LocalSmuSession(native));
                device.Config.Name="Keithley 2600 · 离线预览";
                manager.DeviceServices.Add(device);
                Assert.Contains(device.ContextMenu.Items.OfType<MenuItem>(),item=>ReferenceEquals(item.Command,device.OpenLocalSmuManagerCommand));
                var node=new LocalSmuNode { DeviceCode=device.Code };node.Create();
                Assert.True(node.UseDeviceDelay);
                var delayEditor=PropertyEditorHelper.GenProperties(node,nameof(LocalSmuNode.MeasureDelayMs));
                Assert.Equal(Visibility.Collapsed,delayEditor.Visibility);
                node.UseDeviceDelay=false;node.MeasureDelayMs=0;
                Assert.Equal(Visibility.Visible,delayEditor.Visibility);
                node.UseWindowSettings=true;Assert.Equal(Visibility.Collapsed,delayEditor.Visibility);
                node.UseWindowSettings=false;Assert.Equal(Visibility.Visible,delayEditor.Visibility);
                window=new LocalSmuWindow(device);window.AttachNode(node);
                window.Settings.SourceValue=3;window.Settings.Measurement=SmuMeasurement.Resistance;
                Assert.Null(node.LocalSettings);Assert.Equal(1,node.MeasureValue);
                ((Button)window.FindName("ApplyNodeButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(3,node.MeasureValue);Assert.Equal(SmuMeasurement.Resistance,node.LocalSettings!.Measurement);
                var method=typeof(STNode).GetMethod("OnSaveNode",BindingFlags.Instance|BindingFlags.NonPublic,null,Type.EmptyTypes,null)!;
                var saved=(Dictionary<string,byte[]>)method.Invoke(node,null)!;
                var restored=new LocalSmuNode();restored.Create();restored.OnLoadNode(saved);
                Assert.Equal(SmuMeasurement.Resistance,restored.LocalSettings!.Measurement);
                Assert.Equal(3,restored.MeasureValue);
                Assert.False(restored.UseDeviceDelay);Assert.Equal(0,restored.MeasureDelayMs);
                Assert.True(restored.UseWindowSettings);
                restored.UseWindowSettings=false;Assert.Null(restored.LocalSettings);
                saved.Remove("LocalSmuSettings.v1");restored.OnLoadNode(saved);Assert.Null(restored.LocalSettings);
                saved.Remove(nameof(LocalSmuNode.UseDeviceDelay));saved.Remove(nameof(LocalSmuNode.MeasureDelayMs));
                var legacy=new LocalSmuNode();legacy.Create();legacy.OnLoadNode(saved);
                Assert.True(legacy.UseDeviceDelay);Assert.False(legacy.ShowMeasureDelay);
                Assert.Equal(0,native.Captures);Assert.Equal(0,native.Opens);
                string? preview=Environment.GetEnvironmentVariable("CV_SMU_PREVIEW_PATH");
                if(!string.IsNullOrEmpty(preview))
                {
                    window.AttachNode(node);
                    window.Left=-10000;window.Top=-10000;window.ShowActivated=false;window.ShowInTaskbar=false;window.Show();window.UpdateLayout();
                    var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
                    var background=new DrawingVisual();
                    using(var drawing=background.RenderOpen())drawing.DrawRectangle(window.Background,null,new Rect(0,0,window.ActualWidth,window.ActualHeight));
                    bitmap.Render(background);bitmap.Render(window);
                    var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=System.IO.File.Create(preview);encoder.Save(output);
                }
            }
            finally
            {
                window?.Close();device?.Dispose();if(device!=null)DisplayConfigManager.Instance.Configs.TryRemove(device.Code,out _);
                managerField.SetValue(null,previousManager);foreach(var dictionary in dictionaries)Application.Current.Resources.MergedDictionaries.Remove(dictionary);ConfigService.SetInstance(previousConfig);
            }
        });
    }
    private sealed class DoubleComparer : IEqualityComparer<double>
    {
        public bool Equals(double x,double y)=>Math.Abs(x-y)<Math.Abs(y)*1e-12;
        public int GetHashCode(double obj)=>0;
    }
    private static LocalSmuConnection Connection()=>new(false,"synthetic-"+Guid.NewGuid(),Pss_Type.Keithley_2600,0,false,false);
    private sealed class FakeNative : ILocalSmuNative
    {
        internal int Family=4,Captures,Opens,Closes;internal bool Block,Fail,RequestedCloseOutput;internal double Source;internal SmuOffMode OffMode;
        internal List<Channel>ClosedChannels=[];private Channel channel;
        internal TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Open(LocalSmuConnection c){Opens++;return 0;}
        public int GetIdn(int h,StringBuilder text,ref int length){text.Append("KEITHLEY INSTRUMENTS INC.,MODEL 2602B,offline,1");return 1;}
        public int GetLocalFamily(int h)=>Family;
        public int CloseAdvancedOutput(int h,int ch,int mode){OffMode=(SmuOffMode)mode;ClosedChannels.Add((Channel)ch);return 1;}
        public int SetWiring(int h,bool wire,bool front)=>1;
        public int SetDelay(int h,double delay)=>1;
        public int SetChannel(int h,bool a){channel=a?Channel.A:Channel.B;return 1;}
        public int SetSource(int h,bool voltage)=>1;
        public void ExecuteAdvanced(int h,LocalSmuSettings s,double[] sources,double[] limits,double[] v,double[] i,double[] r,int[] status)
        {
            Captures++;Source=s.SourceValue;RequestedCloseOutput=s.CloseOutput;Started.TrySetResult();if(Block)Release.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
            if(Fail)throw new InvalidOperationException("synthetic capture failure");
            for(int n=0;n<v.Length;n++){v[n]=5;i[n]=.01;r[n]=n==2?9.91e37:501+n;status[n]=0;}
        }
        public int Measure(int h,LocalSmuParameters p,bool step,ref double v,ref double i)=>throw new NotSupportedException();
        public int Read(int h,ref double v,ref double i)=>throw new NotSupportedException();
        public int Sweep(int h,LocalSmuParameters p,double[]v,double[]i)=>throw new NotSupportedException();
        public int CloseOutput(int h){ClosedChannels.Add(channel);return 1;}
        public int Close(int h){Closes++;return 1;}
    }
}
