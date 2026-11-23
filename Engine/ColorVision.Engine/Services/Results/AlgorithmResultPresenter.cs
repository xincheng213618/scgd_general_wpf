using ColorVision.ImageEditor;
using log4net;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Results;

internal sealed class AlgorithmResultPresenter(bool clearImageOnSelection)
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(AlgorithmResultPresenter));
    private long _selectionVersion;

    internal async Task ShowAsync(ViewResultContext context, ViewResultAlg? result, Action<ViewResultAlg> prepareImage)
    {
        long selectionVersion = ++_selectionVersion;
        ImageView image = context.ImageView;
        ClearDetails(context, clearColumns: clearImageOnSelection);
        if (selectionVersion != _selectionVersion) return;
        if (clearImageOnSelection || result == null)
            image.Clear();
        if (result == null || selectionVersion != _selectionVersion) return;

        Task? openCompletion = null;
        long openRevision = 0;
        string stage = "Match";
        try
        {
            var handler = ResultHandleRegistry.GetInstance().ResultHandles.FirstOrDefault(item => item.CanHandle1(result));
            if (selectionVersion != _selectionVersion) return;
            if (handler == null)
            {
                if (!clearImageOnSelection) image.Clear();
                return;
            }

            stage = "Load";
            handler.Load(context, result);
            if (selectionVersion != _selectionVersion) return;
            stage = "PrepareImage";
            prepareImage(result);
            if (selectionVersion != _selectionVersion) return;

            stage = "Handle";
            Task previousOpen = image.ImageOpenCompletion;
            long previousRevision = image.ImageRevision;
            handler.Handle(context, result);
            Task nextOpen = image.ImageOpenCompletion;
            if (selectionVersion != _selectionVersion || nextOpen.IsCompletedSuccessfully) return;
            if (ReferenceEquals(previousOpen, nextOpen))
            {
                // Consecutive selections of the same file can share its in-progress open.
                string? activePath = image.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                if (!image.IsCurrentImageRevision(previousRevision) || string.IsNullOrEmpty(result.FilePath)
                    || !string.Equals(activePath, result.FilePath, StringComparison.Ordinal)) return;
            }

            stage = "OpenImage";
            openCompletion = nextOpen;
            openRevision = image.ImageRevision;
            await nextOpen;
        }
        catch (Exception error)
        {
            if (selectionVersion != _selectionVersion || (openCompletion != null &&
                (!ReferenceEquals(image.ImageOpenCompletion, openCompletion) || !image.IsCurrentImageRevision(openRevision))))
                return;

            Log.Warn($"算法结果显示失败：resultId={result.Id}, batchId={result.AlgResultMasterModel?.BatchId}, resultType={result.ResultType}, stage={stage}", error);
            ClearDetails(context, clearColumns: true);
            if (selectionVersion != _selectionVersion) return;
            image.Clear();
            if (selectionVersion != _selectionVersion) return;
            context.SideTextBox.Text = EngineLocalization.Format($"无法显示算法结果（ID {result.Id}）。请检查结果记录、图像文件和运行日志。");
            context.SideTextBox.Visibility = Visibility.Visible;
        }
    }

    internal void Invalidate() => ++_selectionVersion;

    internal void Clear(ViewResultContext context)
    {
        Invalidate();
        ClearDetails(context, clearColumns: true);
        context.ImageView.Clear();
    }

    private static void ClearDetails(ViewResultContext context, bool clearColumns)
    {
        context.ListView.ItemsSource = null;
        if (clearColumns)
        {
            if (context.ListView.View is GridView gridView)
                gridView.Columns.Clear();
            context.LeftGridViewColumnVisibilitys.Clear();
        }
        context.SideTextBox.Clear();
        context.SideTextBox.Visibility = Visibility.Collapsed;
    }
}
