using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.UI.Tests;

// Shared input boundaries: no silent clamping, drafts reach the binding, and network names remain editable.
public sealed class IPAddressBoxTests
{
    [Theory]
    [InlineData("192.168.100.100", true)]
    [InlineData(" 0.0.0.0 ", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("192.168.100.256", false)]
    [InlineData("192.168..100", false)]
    [InlineData("1.2.3", false)]
    [InlineData("https://192.168.100.100", false)]
    [InlineData("1.2.3.4:5025", false)]
    public void FullAddressPasteAcceptsOnlyFourValidOctets(string value, bool valid) => WpfTestHost.Invoke(() =>
    {
        var box = new IPAddressBox { Text = "1.2.3.4" };
        Assert.Equal(valid, box.TryPasteAddress(value));
        Assert.Equal(valid ? value.Trim() : "1.2.3.4", box.Text);
    });

    [Fact]
    public void SegmentEditingAndExternalChangesPreserveTwoWayBinding() => WpfTestHost.Invoke(() =>
    {
        var model = new AddressModel { Address = "192.168.100.100" };
        var box = BoundBox(model);
        var part = (TextBox)box.FindName("Part1");
        part.Text = "17";
        Assert.Equal("192.17.100.100", model.Address);
        part.Clear();
        Assert.Equal("192..100.100", model.Address);
        Assert.True(box.TryPasteAddress("10.0.0.1"));
        Assert.Equal("10.0.0.1", model.Address);
        model.Address = "172.16.0.2";
        Assert.Equal("16", part.Text);
        Assert.NotNull(BindingOperations.GetBindingExpression(box, IPAddressBox.TextProperty));
    });

    [Fact]
    public void ReadOnlyBlocksTypingAndPasteWithoutChangingTheAddress() => WpfTestHost.Invoke(() =>
    {
        var box = new IPAddressBox { Text = "1.2.3.4", IsReadOnly = true };
        var part = (TextBox)box.FindName("Part0");
        Type(part, "5");
        Paste(part, "10.0.0.1");
        Assert.False(box.TryPasteAddress("10.0.0.1"));
        Assert.Equal("1.2.3.4", box.Text);
    });

    [Fact]
    public void TypingAndSingleSegmentPasteRejectOutOfRangeAndNonDigits() => WpfTestHost.Invoke(() =>
    {
        var box = new IPAddressBox { Text = "25.2.3.4" };
        var part = (TextBox)box.FindName("Part0");
        part.Select(part.Text.Length, 0);
        Type(part, "6");
        Type(part, "x");
        Assert.Equal("25.2.3.4", box.Text);
        part.SelectAll();
        Paste(part, "999");
        Assert.Equal("25.2.3.4", box.Text);
        Paste(part, "127");
        Assert.Equal("127.2.3.4", box.Text);
        Paste(part, "10.20.30.40");
        Assert.Equal("10.20.30.40", box.Text);
    });

    [Theory]
    [InlineData("localhost")]
    [InlineData("db.internal")]
    [InlineData("::1")]
    [InlineData("COM7")]
    public void NetworkTextModePreservesLegacyNamesAndCanReturnToIPv4(string address) => WpfTestHost.Invoke(() =>
    {
        var model = new AddressModel { Address = address };
        var box = BoundBox(model, true);
        Assert.False(box.IsSegmented);
        Assert.Equal(address, ((TextBox)box.FindName("PlainText")).Text);
        model.Address = "192.168.100.100";
        Assert.True(box.IsSegmented);
        var mode = (Button)box.FindName("ModeButton");
        ((MenuItem)mode.ContextMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var text = (TextBox)box.FindName("PlainText");
        text.Text = "10.0.0.1";
        Assert.False(box.IsSegmented);
        Assert.Equal("10.0.0.1", model.Address);
        ((MenuItem)mode.ContextMenu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.True(box.IsSegmented);
    });

    [Fact]
    public void PropertyEditorUsesReadOnlyMetadata() => WpfTestHost.Invoke(() =>
    {
        var panel = new IPAddressPropertiesEditor().GenProperties(typeof(AddressModel).GetProperty(nameof(AddressModel.ReadOnlyAddress))!, new AddressModel());
        var box = Assert.IsType<IPAddressBox>(panel.Children[1]);
        Assert.True(box.IsReadOnly);
        Assert.False(box.TryPasteAddress("10.0.0.1"));
    });

    [Fact]
    public void EnablingNetworkTextModeAfterBindingKeepsTheExistingHostVisible() => WpfTestHost.Invoke(() =>
    {
        var box = new IPAddressBox { Text = "localhost", AllowTextMode = true };
        Assert.False(box.IsSegmented);
        Assert.Equal("localhost", ((TextBox)box.FindName("PlainText")).Text);
    });

    private static IPAddressBox BoundBox(AddressModel model, bool textMode = false)
    {
        var box = new IPAddressBox { AllowTextMode = textMode };
        box.SetBinding(IPAddressBox.TextProperty, new Binding(nameof(AddressModel.Address))
        { Source = model, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        return box;
    }

    private static void Type(TextBox part, string text) => part.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice,
        new TextComposition(InputManager.Current, part, text)) { RoutedEvent = TextCompositionManager.PreviewTextInputEvent });
    private static void Paste(TextBox part, string text) => part.RaiseEvent(new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, text), false, DataFormats.UnicodeText)
        { RoutedEvent = DataObject.PastingEvent });

    private sealed class AddressModel : ViewModelBase
    {
        public string Address { get => address; set => SetProperty(ref address, value); }
        private string address = string.Empty;
        [ReadOnly(true)] public string ReadOnlyAddress { get; set; } = "1.2.3.4";
    }
}
