using ColorVisionServiceHost;

namespace ColorVision.UI.Tests
{
    public sealed class Com0ComCommandServiceTests
    {
        [Fact]
        public void ParsePairsUsesAssignedComNamesAndKeepsInternalNames()
        {
            string output = """
                CNCA0 PortName=-
                CNCB0 PortName=-
                CNCA1 PortName=COM#,RealPortName=COM4
                CNCB1 PortName=COM#,RealPortName=COM5
                """;

            IReadOnlyList<Com0ComPairInfo> pairs = Com0ComCommandService.ParsePairs(output);

            Assert.Collection(
                pairs,
                pair =>
                {
                    Assert.Equal(0, pair.PairNumber);
                    Assert.Equal("CNCA0", pair.PortA);
                    Assert.Equal("CNCB0", pair.PortB);
                    Assert.Equal("CNCA0 ↔ CNCB0", pair.DisplayName);
                },
                pair =>
                {
                    Assert.Equal(1, pair.PairNumber);
                    Assert.Equal("COM4", pair.PortA);
                    Assert.Equal("COM5", pair.PortB);
                    Assert.Equal("CNCA1", pair.InternalPortA);
                    Assert.Equal("CNCB1", pair.InternalPortB);
                });
        }

        [Fact]
        public void ParsePairsIgnoresCommandNoiseAndOrdersPairNumbers()
        {
            string output = """
                command> list
                CNCB10 PortName=COM11
                unrelated output
                CNCA2 PortName=COM7
                CNCB2 PortName=COM8
                CNCA10 PortName=COM10
                """;

            IReadOnlyList<Com0ComPairInfo> pairs = Com0ComCommandService.ParsePairs(output);

            Assert.Equal([2, 10], pairs.Select(pair => pair.PairNumber));
            Assert.Equal("COM7 ↔ COM8", pairs[0].DisplayName);
            Assert.Equal("COM10 ↔ COM11", pairs[1].DisplayName);
        }

        [Theory]
        [InlineData(null, "COM#")]
        [InlineData("", "COM#")]
        [InlineData("4", "COM4")]
        [InlineData(" 005 ", "COM5")]
        [InlineData(" com1 ", "COM1")]
        [InlineData("com256", "COM256")]
        public void NormalizeRequestedPortAcceptsAutomaticOrValidComNames(string? value, string expected)
        {
            Assert.Equal(expected, Com0ComCommandService.NormalizeRequestedPort(value));
        }

        [Theory]
        [InlineData("0")]
        [InlineData("257")]
        [InlineData("COM0")]
        [InlineData("COM257")]
        [InlineData("CNCA1")]
        [InlineData("COM1 & whoami")]
        [InlineData("COM 1")]
        public void NormalizeRequestedPortRejectsInvalidOrExecutableInput(string value)
        {
            Assert.Throws<InvalidOperationException>(() => Com0ComCommandService.NormalizeRequestedPort(value));
        }

        [Fact]
        public void FindAvailablePortNumbersExcludesClaimedComPorts()
        {
            IReadOnlyList<int> available = Com0ComCommandService.FindAvailablePortNumbers(["COM1", "com3", "CNCA0"]);

            Assert.DoesNotContain(1, available);
            Assert.Contains(2, available);
            Assert.DoesNotContain(3, available);
            Assert.Contains(4, available);
            Assert.Equal(254, available.Count);
        }

        [Fact]
        public void FindSuggestedPairPrefersFirstConsecutivePairAtCom4OrAbove()
        {
            IReadOnlyList<int> available = Com0ComCommandService.FindAvailablePortNumbers(["COM3"]);

            Com0ComPortPairSuggestion? suggestion = Com0ComCommandService.FindSuggestedPair(available);

            Assert.NotNull(suggestion);
            Assert.Equal(4, suggestion.PortA);
            Assert.Equal(5, suggestion.PortB);
        }
    }
}
