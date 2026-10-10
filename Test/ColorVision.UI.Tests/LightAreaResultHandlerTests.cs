using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Results.LocalFindLuminousArea;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class LightAreaResultHandlerTests
{
    // The registry picks the first match; ownership must not depend on discovery order.
    [Theory]
    [InlineData(ViewResultAlgType.FindLightArea, "AA", "RobustV2", true, false)]
    [InlineData(ViewResultAlgType.ARVR_AAFindPoints, "AA", "aa-template", true, false)]
    [InlineData(ViewResultAlgType.FindLightArea, "RobustV2", "aa-template", false, true)]
    [InlineData(ViewResultAlgType.FindLightArea, null, "RobustV2", false, true)]
    [InlineData(ViewResultAlgType.FindLightArea, "", "RobustV2", false, true)]
    [InlineData(ViewResultAlgType.FindLightArea, "JND", "RobustV2", false, false)]
    [InlineData(ViewResultAlgType.FindLightArea, null, "other-template", false, false)]
    [InlineData((ViewResultAlgType)8, null, "old-template", false, false)]
    [InlineData(ViewResultAlgType.ARVR_AAFindPoints, "RobustV2", "RobustV2", false, false)]
    public void ResultOwnershipIsExclusive(ViewResultAlgType type, string? version, string template, bool aa, bool local)
    {
        var result = new ViewResultAlg { ResultType = type, Version = version, POITemplateName = template };
        Assert.Equal(aa, new ViewHandleAAFindPoints().CanHandle1(result));
        Assert.Equal(local, new ViewHandleLocalFindLuminousArea().CanHandle1(result));
    }

    // Protocol IDs are persisted in result tables and sent by the unmodified service.
    [Fact]
    public void RetiredResultIdLoadsAsUnsupportedWithoutRenumberingCurrentResults()
    {
        Assert.False(Enum.IsDefined((ViewResultAlgType)8));
        Assert.False(Enum.IsDefined((CVCommCore.CVResultType)8));
        Assert.Equal(9, (int)ViewResultAlgType.Distortion);
        Assert.Equal(9, (int)CVCommCore.CVResultType.Algorithm_Distortion);
        Assert.Equal(36, (int)ViewResultAlgType.FindLightArea);
        Assert.Equal(36, (int)CVCommCore.CVResultType.Algorithm_FindLightArea);
        Assert.Equal(61, (int)ViewResultAlgType.ARVR_AAFindPoints);
        Assert.Equal(61, (int)CVCommCore.CVResultType.Algorithm_ARVR_AAFindPoints);

        var previous = JsonConvert.DeserializeObject<AlgResultMasterModel>("{\"ImgFileType\":8}")!;
        Assert.Equal(8, (int)previous.ImgFileType);
        Assert.False(Enum.IsDefined(previous.ImgFileType));
    }

    [Fact]
    public void SharedModelUsesCommonNamespaceAndPreservesStoredCoordinates()
    {
        Assert.Equal("ColorVision.Engine.Services.Results.LightArea.AlgResultLightAreaModel", typeof(AlgResultLightAreaModel).FullName);
        Assert.Equal("ColorVision.Engine.Services.Results.LightArea.AlgResultLightAreaDao", typeof(AlgResultLightAreaDao).FullName);
        Assert.Equal("t_scgd_algorithm_result_detail_light_area", typeof(AlgResultLightAreaModel).GetCustomAttribute<SugarTable>()!.TableName);
        foreach (var (name, column) in new[] { ("Pid", "pid"), ("PosX", "pos_x"), ("PosY", "pos_y") })
            Assert.Equal(column, typeof(AlgResultLightAreaModel).GetProperty(name)!.GetCustomAttribute<SugarColumn>()!.ColumnName);
        var point = JsonConvert.DeserializeObject<AlgResultLightAreaModel>("{\"Id\":5,\"Pid\":42,\"PosX\":10.25,\"PosY\":20.75}")!;
        Assert.Equal(42, point.Pid);
        Assert.Equal(10.25f, point.PosX);
        Assert.Equal(20.75f, point.PosY);
    }

    [Fact]
    public void AaJsonMapsCornersIntoSharedModelWithoutUsingSamplingPoints()
    {
        var payload = JsonConvert.DeserializeObject<AAFindPoint>("""
            {
              "Points": { "location": [{ "id": 99, "x": 999, "y": 888 }], "nummber_x": 1, "nummber_y": 1 },
              "corner": [{ "id": 3, "x": 10, "y": 20 }, { "id": 2, "x": 210, "y": 20 },
                         { "id": 1, "x": 210, "y": 120 }, { "id": 0, "x": 10, "y": 120 }]
            }
            """)!;
        var result = new AAFindPointsViewReslut { DetailCommonModel = new DetailCommonModel { PId = 42 }, AAFindPoint = payload };
        var corners = result.LightAreaModels;
        Assert.Equal(new[] { 3, 2, 1, 0 }, corners.Select(point => point.Id));
        Assert.All(corners, point => Assert.Equal(42, point.Pid));
        Assert.Equal(new[] { 10f, 210f, 210f, 10f }, corners.Select(point => point.PosX));
        Assert.Equal(new[] { 20f, 20f, 120f, 120f }, corners.Select(point => point.PosY));
        Assert.Equal(999, Assert.Single(payload.Points!.Location).X);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FlowHandlersReuseDetailsAndKeepEmptyFailuresEmpty(bool aa, bool empty)
    {
        WpfTestHost.Invoke(() =>
        {
            var resources = Application.Current.Resources;
            resources["TextBox.Small"] = new Style(typeof(TextBox));
            resources["ComboBox.Small"] = new Style(typeof(ComboBox));
            resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
            resources["ToolBarImage"] = new Style(typeof(Image));
            resources["BaseStyle"] = new Style(typeof(Control));
            resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
            resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
            using ImageView imageView = new();
            imageView.Config.IsLayoutUpdated = false;
            var context = new ViewResultContext { ImageView = imageView, ListView = new ListView { View = new GridView() }, LeftGridViewColumnVisibilitys = new(), SideTextBox = new TextBox() };
            ObservableCollection<IViewResult> details = [];
            if (!empty)
                foreach (var (x, y) in new[] { (10.25f, 20.75f), (210.5f, 20.75f), (210.5f, 120.25f), (10.25f, 120.25f) })
                    details.Add(new AlgResultLightAreaModel { Pid = 42, PosX = x, PosY = y });
            var result = new ViewResultAlg { ResultType = ViewResultAlgType.FindLightArea, Version = aa ? "AA" : "RobustV2", ViewResults = details };
            IResultHandleBase handler = aa ? new ViewHandleAAFindPoints() : new ViewHandleLocalFindLuminousArea();
            handler.Load(context, result);
            Assert.Same(details, result.ViewResults);
            handler.Handle(context, result);
            var rows = context.ListView.Items.Cast<AlgResultLightAreaModel>().ToArray();
            Assert.Equal(details.Count, rows.Length);
            Assert.Equal(new[] { "PosX", "PosY" }, ((GridView)context.ListView.View).Columns.Select(column => column.Header));
            if (empty) Assert.Empty(imageView.ImageShow.Visuals.OfType<DVPolygon>());
            else
            {
                Assert.Equal(10.25f, rows[0].PosX);
                DVPolygon polygon = Assert.Single(imageView.ImageShow.Visuals.OfType<DVPolygon>());
                Assert.Contains(new Point(10, 20), polygon.Points);
                Assert.Contains(new Point(210, 120), polygon.Points);
            }
        });
    }
}
