using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Logical;
using FlowEngineLib.Node.Camera;
using ST.Library.UI.NodeEditor;
using System.Runtime.ExceptionServices;

namespace ColorVision.UI.Tests;

public class LogicalANDNodeVisualTests
{
    [Fact]
    public void MultipleInputsShareOneVisualAnchorAndKeepIndependentConnections()
    {
        RunInSta(() =>
        {
            using var editor = new STNodeEditor
            {
                ClientSize = new System.Drawing.Size(800, 600)
            };
            var logicalAnd = new LogicalANDNode();
            logicalAnd.Create();
            editor.Nodes.Add(logicalAnd);
            int initialHeight = logicalAnd.Height;
            var camera = new CommCameraNode();
            camera.Create();
            camera.Left = 300;
            camera.Top = 100;
            editor.Nodes.Add(camera);
            var calibration = new CalibrationNode();
            calibration.Create();
            calibration.Left = 500;
            calibration.Top = 100;
            editor.Nodes.Add(calibration);

            Assert.Equal(camera.Size, logicalAnd.Size);
            Assert.Equal(calibration.Size, logicalAnd.Size);

            var sources = Enumerable.Range(0, 3)
                .Select(_ =>
                {
                    var source = new FlowSourceNode();
                    source.Create();
                    editor.Nodes.Add(source);
                    return source;
                })
                .ToArray();

            foreach (FlowSourceNode source in sources)
            {
                STNodeOption availableInput = logicalAnd.GetAllInputOptions().Last();
                Assert.Equal(ConnectionStatus.Connected, source.Output.ConnectOption(availableInput));
                Assert.Equal(initialHeight, logicalAnd.Height);
            }

            STNodeOption[] inputs = logicalAnd.GetAllInputOptions();
            Assert.Equal(4, inputs.Length);
            Assert.Equal(3, inputs.Count(input => input.ConnectionCount == 1));
            Assert.Single(inputs.Select(input => input.DotTop).Distinct());
            Assert.Equal(CVCommonNode.StandardNodeWidth, logicalAnd.Width);
            Assert.Equal(CVCommonNode.StandardNodeMinHeight, logicalAnd.Height);

            STNodeOption availableAnchor = inputs.Last();
            var hit = editor.FindNodeFromPoint(new System.Drawing.PointF(
                availableAnchor.DotLeft + availableAnchor.DotSize / 2f,
                availableAnchor.DotTop + availableAnchor.DotSize / 2f));
            Assert.Same(availableAnchor, hit.NodeOption);
            Assert.Equal(0, availableAnchor.ConnectionCount);
        });
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

    private sealed class FlowSourceNode : STNode
    {
        public STNodeOption Output { get; private set; } = STNodeOption.Empty;

        protected override void OnCreate()
        {
            base.OnCreate();
            Output = OutputOptions.Add("OUT", typeof(CVStartCFC), bSingle: false);
        }
    }
}
