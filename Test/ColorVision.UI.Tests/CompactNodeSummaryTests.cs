using FlowEngineLib;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Node.Camera;
using FlowEngineLib.Node.OLED;
using FlowEngineLib.Node.Spectrum;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class CompactNodeSummaryTests
{
    [Fact]
    public void SingleInputServerNodesShowTheirTemplateName()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var calibration = new InspectableCalibrationNode();
            calibration.Create();
            editor.Nodes.Add(calibration);
            calibration.TempName = "Calibration.Template";
            var algorithm = new InspectableAlgorithmNode();
            algorithm.Create();
            editor.Nodes.Add(algorithm);
            algorithm.Algorithm = AlgorithmType.发光区检测;
            algorithm.TempName = "FocusPoints.Template";

            Assert.True(calibration.DrawsCompactSummary);
            Assert.Equal("Calibration.Template", calibration.CompactSummaryValue);
            Assert.True(algorithm.DrawsCompactSummary);
            Assert.Equal("FocusPoints.Template", algorithm.CompactSummaryValue);
        });
    }

    [Fact]
    public void SpectrumShowsOnlyItsIntegrationTimeSummary()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var spectrum = new InspectableSpectrumNode();
            spectrum.Create();
            editor.Nodes.Add(spectrum);
            spectrum.Temp = 321.5f;

            Assert.True(spectrum.DrawsCompactSummary);
            Assert.Equal(ST.Library.UI.Lang.Get("积分时间") + ":", spectrum.CompactSummaryLabel);
            Assert.Equal(spectrum.Temp.ToString(), spectrum.CompactSummaryValue);
        });
    }

    [Fact]
    public void TwoInputNodesKeepTheirExistingCompactSizeWithoutSummary()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var algorithm = new InspectableAlgorithm2InNode();
            algorithm.Create();
            editor.Nodes.Add(algorithm);
            algorithm.TempName = "This.Template.Must.Not.Be.Drawn";

            Assert.Equal(2, algorithm.InputOptionsCount);
            Assert.False(algorithm.DrawsCompactSummary);
            Assert.Equal(FlowEngineLib.Base.CVCommonNode.StandardNodeWidth, algorithm.Width);
            Assert.Equal(FlowEngineLib.Base.CVCommonNode.StandardNodeMinHeight, algorithm.Height);
        });
    }

    [Fact]
    public void CameraNodesKeepTheirPurposeSpecificSingleLineDisplays()
    {
        RunInSta(() =>
        {
            using var editor = CreateEditor();
            var commonCamera = new InspectableCommCameraNode();
            commonCamera.Create();
            editor.Nodes.Add(commonCamera);
            commonCamera.CamTempName = "Camera.Template";
            commonCamera.TempName = "Exposure.Template";
            var lvCamera = new InspectableLVCameraNode();
            lvCamera.Create();
            editor.Nodes.Add(lvCamera);
            lvCamera.ExpTime = 400f;
            var cvCamera = new InspectableCVCameraNode();
            cvCamera.Create();
            editor.Nodes.Add(cvCamera);
            cvCamera.TempR = 100f;
            cvCamera.TempG = 200f;
            cvCamera.TempB = 300f;

            Assert.Equal("Camera.Template", Assert.IsType<STNodeEditText<string>>(Assert.Single(commonCamera.VisibleControls)).Value);
            Assert.Equal(400f, Assert.IsType<STNodeEditText<float>>(Assert.Single(lvCamera.VisibleControls)).Value);
            Assert.Equal("100/200/300", Assert.IsType<STNodeEditText<string>>(Assert.Single(cvCamera.VisibleControls)).Value);
        });
    }

    [Fact]
    public void LongSummaryIsClippedInsideTheNode()
    {
        RunInSta(() =>
        {
            using var editor = new TestNodeEditor
            {
                ClientSize = new System.Drawing.Size(260, 160),
                ShowNodeShadow = false
            };
            var calibration = new InspectableCalibrationNode();
            calibration.Create();
            calibration.Left = 20;
            calibration.Top = 20;
            editor.Nodes.Add(calibration);
            calibration.TempName = new string('W', 80);

            using var bitmap = editor.RenderNodes(new System.Drawing.Rectangle(0, 0, 260, 160));
            int summaryTop = calibration.Top + calibration.TitleHeight + 30;
            bool hasSummaryText = false;
            for (int y = summaryTop; y < summaryTop + 18; y++)
            {
                for (int x = calibration.Left + 5; x < calibration.Right - 5; x++)
                {
                    System.Drawing.Color pixel = bitmap.GetPixel(x, y);
                    hasSummaryText |= pixel.R > 180 && pixel.G > 180 && pixel.B > 180;
                }
                for (int x = calibration.Right + 1; x < calibration.Right + 30; x++)
                {
                    Assert.Equal(0, bitmap.GetPixel(x, y).A);
                }
            }
            Assert.True(hasSummaryText);
        });
    }

    private static TestNodeEditor CreateEditor()
    {
        return new TestNodeEditor
        {
            ClientSize = new System.Drawing.Size(800, 600)
        };
    }

    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (exception != null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    private sealed class TestNodeEditor : STNodeEditor
    {
        public System.Drawing.Bitmap RenderNodes(System.Drawing.Rectangle viewport)
        {
            var bitmap = new System.Drawing.Bitmap(viewport.Width, viewport.Height);
            using var graphics = System.Drawing.Graphics.FromImage(bitmap);
            using var pen = new System.Drawing.Pen(System.Drawing.Color.Black);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.Black);
            OnDrawNode(new DrawingTools
            {
                Graphics = graphics,
                Pen = pen,
                SolidBrush = brush
            }, viewport);
            return bitmap;
        }
    }

    private sealed class InspectableCalibrationNode : CalibrationNode
    {
        public bool DrawsCompactSummary => ShouldDrawCompactSummary();
        public string CompactSummaryValue => GetCompactSummaryValue();
    }

    private sealed class InspectableAlgorithmNode : AlgorithmNode
    {
        public bool DrawsCompactSummary => ShouldDrawCompactSummary();
        public string CompactSummaryValue => GetCompactSummaryValue();
    }

    private sealed class InspectableSpectrumNode : SpectrumNode
    {
        public bool DrawsCompactSummary => ShouldDrawCompactSummary();
        public string CompactSummaryLabel => GetCompactSummaryLabel();
        public string CompactSummaryValue => GetCompactSummaryValue();
    }

    private sealed class InspectableAlgorithm2InNode : Algorithm2InNode
    {
        public bool DrawsCompactSummary => ShouldDrawCompactSummary();
    }

    private sealed class InspectableCommCameraNode : CommCameraNode
    {
        public STNodeControl[] VisibleControls => Controls.Cast<STNodeControl>().Where(control => control.Visable).ToArray();
    }

    private sealed class InspectableLVCameraNode : LVCameraNode
    {
        public STNodeControl[] VisibleControls => Controls.Cast<STNodeControl>().Where(control => control.Visable).ToArray();
    }

    private sealed class InspectableCVCameraNode : CVCameraNode
    {
        public STNodeControl[] VisibleControls => Controls.Cast<STNodeControl>().Where(control => control.Visable).ToArray();
    }
}
