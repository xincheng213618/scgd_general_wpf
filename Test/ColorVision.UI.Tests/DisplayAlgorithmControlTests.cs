using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Algorithm;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class DisplayAlgorithmControlTests
{
    [Fact]
    public void MovingCalculateButtonsBetweenCachedViewsPreservesExecutionAndRequestState()
    {
        WpfTestHost.Invoke(() =>
        {
            var first = new TestAlgorithm();
            var second = new TestAlgorithm();
            var firstView = new DisplayAlgorithmControl(first);
            var secondView = new DisplayAlgorithmControl(second);
            var header = new ContentControl();

            var firstButton = firstView.DetachCalculateButton();
            header.Content = firstButton;
            firstButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, first.Executions);
            Assert.False(firstButton.IsEnabled);

            header.Content = null;
            var secondButton = secondView.DetachCalculateButton();
            header.Content = secondButton;
            Assert.True(secondButton.IsEnabled);
            secondButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(1, second.Executions);
            Assert.False(secondButton.IsEnabled);
            object secondStatus = secondButton.Content;

            first.Record.MsgRecordState = MsgRecordState.Success;
            Assert.True(firstButton.IsEnabled);
            Assert.False(secondButton.IsEnabled);
            Assert.Equal(secondStatus, secondButton.Content);

            header.Content = null;
            header.Content = firstView.DetachCalculateButton();
            Assert.Same(firstButton, header.Content);
            Assert.True(firstButton.IsEnabled);
            firstButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.Equal(2, first.Executions);
            Assert.Equal(1, second.Executions);
            second.Record.MsgRecordState = MsgRecordState.Timeout;
            Assert.True(secondButton.IsEnabled);
            Assert.False(firstButton.IsEnabled);
            first.Record.MsgRecordState = MsgRecordState.Fail;
            Assert.True(firstButton.IsEnabled);
            Assert.Equal(ColorVision.Engine.Properties.Resources.Calculate, firstButton.Content);
            header.Content = null;
        });
    }

    private sealed class TestConfiguration : DisplayAlgorithmConfigBase { }

    private sealed class TestAlgorithm : DisplayAlgorithmBase<TestConfiguration>
    {
        public int Executions { get; private set; }
        public MsgRecord Record { get; } = new();

        public TestAlgorithm() : base(new TestConfiguration()) { }

        public override MsgRecord Execute()
        {
            Executions++;
            Record.MsgRecordState = MsgRecordState.Sended;
            return Record;
        }
    }
}
