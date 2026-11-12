using ColorVision.Algorithms;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Jsons.FindCross;

public sealed class ViewHandleLocalRgbCross : IResultHandleBase
{
    private readonly Func<int, IReadOnlyList<DetailCommonModel>> readDetails;
    private static readonly ConditionalWeakTable<ImageView, RenderState> States = new();
    private sealed class RenderState
    {
        internal long Generation;
        internal IDisposable? Registration;
        internal ResultImagePlaceholderCache Placeholder = new();
    }

    public ViewHandleLocalRgbCross() : this(id => DeatilCommonDao.Instance.GetAllByPid(id)) { }
    internal ViewHandleLocalRgbCross(Func<int, IReadOnlyList<DetailCommonModel>> readDetails) => this.readDetails = readDetails;
    public override string Name => "十字 RGB 分离";
    public override List<ViewResultAlgType> CanHandle { get; } = [ViewResultAlgType.FindCross];
    public override bool CanHandle1(ViewResultAlg result) => result.Version == LocalRgbCrossResultPersistence.ResultVersion && base.CanHandle1(result);

    public override void Load(ViewResultContext context, ViewResultAlg result)
    {
        if (result.ViewResults != null) return;
        LocalRgbCrossHistory history;
        try { history = LocalRgbCrossHistory.Read(readDetails(result.Id), result.Id); }
        catch (Exception error) { history = LocalRgbCrossHistory.Failure(error.Message); }
        result.ViewResults = new ObservableCollection<IViewResult> { history };
    }

    public override void Handle(ViewResultContext context, ViewResultAlg result) => _ = PresentAsync(context, result);

    // Image opening can be asynchronous. The historical selection entry point cannot await Handle.
    internal async Task PresentAsync(ViewResultContext context, ViewResultAlg result)
    {
        Load(context, result);
        RenderState state = States.GetValue(context.ImageView, view =>
        {
            RenderState created = new();
            view.ImageShow.VisualsChanged += (_, args) =>
            {
                if (args.ChangeType != VisualChangeType.Clear) return;
                created.Registration?.Dispose();
                created.Registration = null;
            };
            return created;
        });
        long generation = ++state.Generation;
        state.Registration?.Dispose(); state.Registration = null;
        LocalRgbCrossHistory history = result.ViewResults.OfType<LocalRgbCrossHistory>().Single();
        if (context.ListView.View is GridView grid)
        {
            context.LeftGridViewColumnVisibilitys.Clear(); grid.Columns.Clear();
            foreach (var (property, title) in new[] { ("Point", "点位"), ("Row", "行"), ("Column", "列"), ("Status", "测量状态"), ("RG", "R-G (px)"), ("BG", "B-G (px)"), ("Diagnostic", "诊断") })
                grid.Columns.Add(new GridViewColumn { Header = title, DisplayMemberBinding = new Binding(property) { StringFormat = property is "RG" or "BG" ? "F3" : null } });
        }
        context.ListView.ItemsSource = history.Rows;
        context.SideTextBox.Visibility = Visibility.Visible;
        context.SideTextBox.Text = history.Diagnostic + "\n" + string.Join("\n", history.Rows.Where(row => row.Diagnostic.Length > 0).Select(row => row.Point + ": " + row.Diagnostic));
        bool Current() => state.Generation == generation && ReferenceEquals(context.ListView.ItemsSource, history.Rows);
        try
        {
            bool sourceExists = File.Exists(result.FilePath);
            if (sourceExists)
            {
                if (!string.Equals(context.ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), result.FilePath, StringComparison.Ordinal)) context.ImageView.Clear();
                OpenSourceImage(context, result);
                await context.ImageView.ImageOpenCompletion;
                if (!Current() || !string.Equals(context.ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), result.FilePath, StringComparison.Ordinal)) return;
                if (history.Width == 0) return;
                if (context.ImageView.ViewBitmapSource is not BitmapSource bitmap || bitmap.PixelWidth != history.Width || bitmap.PixelHeight != history.Height)
                { context.SideTextBox.AppendText("\n原图加载失败或尺寸与历史结果不一致，未绘制叠图。"); return; }
            }
            else
            {
                context.ImageView.Clear();
                context.SideTextBox.AppendText("\n原图不存在；以下叠图使用保存的原图坐标与白色底图。");
                if (history.Width == 0) return;
                context.ImageView.SetImageSource(state.Placeholder.GetOrCreate(history.Width, history.Height), enableEditorImageServices: false, configureDefaultLayerController: false);
                context.ImageView.UpdateZoomAndScale();
            }
            if (!Current()) return;
            using AlgorithmResult overlay = new()
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmGeometryArtifact("rgb-cross-history-regions", AlgorithmCoordinateSpace.Pixel, history.Geometries),
                    new AlgorithmOverlayArtifact("rgb-cross-history", AlgorithmOverlayLifetime.Transient, history.Overlays)]
            };
            state.Registration = AlgorithmOverlayRenderer.Apply(context.ImageView.EditorContext.ProcessingContext, context.ImageView.EditorContext.DrawEditorContext, overlay);
        }
        catch (Exception error)
        {
            if (Current()) context.SideTextBox.AppendText("\n十字结果展示失败：" + error.Message);
        }
    }
}
