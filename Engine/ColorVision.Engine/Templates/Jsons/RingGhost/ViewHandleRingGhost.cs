using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.ImageEditor.Algorithms;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Engine.Templates.Jsons.RingGhost;

public sealed class RingGhostView : IViewResult
{
    public required LocalRingGhostResult Measurement { get; init; }
    public required string ResultFilePath { get; init; }
    public double GMR1 => Measurement.GMR1;
    public double GMR2 => Measurement.GMR2;
    public string Status => Measurement.MeasurementStatus;
}

public sealed class ViewHandleRingGhost : IResultHandleBase
{
    public override string Name => "圆环鬼影";
    public override List<ViewResultAlgType> CanHandle { get; } = [ViewResultAlgType.Ghost];
    public override bool CanHandle1(ViewResultAlg result) => result.Version == LocalRingGhostResult.ResultVersion && base.CanHandle1(result);
    public override void Load(ViewResultContext context, ViewResultAlg result)
    {
        if (result.ViewResults != null) return;
        var details = DeatilCommonDao.Instance.GetAllByPid(result.Id);
        if (details.Count != 1 || details[0].PId != result.Id) throw new InvalidDataException("鬼影结果需要唯一明细。");
        string? file = JsonConvert.DeserializeObject<ResultFile>(details[0].ResultJson)?.ResultFileName;
        if (string.IsNullOrWhiteSpace(file) || !Path.IsPathFullyQualified(file)) throw new InvalidDataException("鬼影结果文件路径无效。");
        result.ViewResults = new ObservableCollection<IViewResult>
        { new RingGhostView { Measurement = LocalRingGhostResult.Read(file), ResultFilePath = file } };
    }
    public override void Handle(ViewResultContext context, ViewResultAlg result)
    {
        Load(context, result);
        OpenSourceImage(context, result);
        var view = result.ViewResults.OfType<RingGhostView>().Single();
        RingGhostOverlayRenderer.Render(context.ImageView, view.Measurement.ImageWidth, view.Measurement.ImageHeight, view.Measurement.Regions);
        if (context.ListView.View is GridView grid)
        {
            context.LeftGridViewColumnVisibilitys.Clear(); grid.Columns.Clear();
            foreach (string key in new[] { "GMR1", "GMR2" }) grid.Columns.Add(new GridViewColumn
            { Header = key + " (%)", DisplayMemberBinding = new Binding(key) { StringFormat = "F4" } });
            context.ListView.ItemsSource = result.ViewResults;
        }
        context.SideTextBox.Text = $"GMR1: {view.GMR1:F4}%\nGMR2: {view.GMR2:F4}%\n{view.Status}";
    }
    public override void SideSave(ViewResultAlg result, string directory)
    {
        var view = result.ViewResults.OfType<RingGhostView>().Single();
        File.WriteAllText(Path.Combine(directory, result.Batch + "_Ghost.json"), JsonConvert.SerializeObject(new { view.GMR1, view.GMR2 }));
    }
}
