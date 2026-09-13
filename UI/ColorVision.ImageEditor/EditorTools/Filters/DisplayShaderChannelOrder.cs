namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public enum DisplayShaderChannelOrder
    {
        Rgb = 0,
        // Keep Brg at 1 for compatibility with settings saved by the original
        // two-option implementation.
        Brg = 1,
        Rbg = 2,
        Grb = 3,
        Gbr = 4,
        Bgr = 5
    }

    internal static class DisplayShaderChannelOrderExtensions
    {
        internal static (int Red, int Green, int Blue) GetChannelIndices(this DisplayShaderChannelOrder channelOrder)
        {
            return channelOrder switch
            {
                DisplayShaderChannelOrder.Rbg => (0, 2, 1),
                DisplayShaderChannelOrder.Grb => (1, 0, 2),
                DisplayShaderChannelOrder.Gbr => (1, 2, 0),
                DisplayShaderChannelOrder.Brg => (2, 0, 1),
                DisplayShaderChannelOrder.Bgr => (2, 1, 0),
                _ => (0, 1, 2)
            };
        }
    }
}
