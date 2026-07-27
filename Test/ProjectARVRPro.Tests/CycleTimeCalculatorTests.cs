using Xunit;

namespace ProjectARVRPro.Tests
{
    public class CycleTimeCalculatorTests
    {
        [Fact]
        public void CalculateGroupsBySnAndSumsRunTime()
        {
            DateTime start = new(2026, 7, 25, 10, 0, 0);
            CycleTimeResultSample[] samples =
            [
                new() { Id = 1, SN = "SN-A", TestType = 0, RunTime = 1200, CreateTime = start },
                new() { Id = 2, SN = "SN-A", TestType = 1, RunTime = 2300, CreateTime = start.AddSeconds(3) },
                new() { Id = 3, SN = "SN-B", TestType = 0, RunTime = 900, CreateTime = start.AddSeconds(6) }
            ];

            IReadOnlyList<CycleTimeGroup> groups = CycleTimeCalculator.Calculate(samples);

            Assert.Equal(2, groups.Count);
            Assert.Equal("SN-B", groups[0].SN);
            Assert.Equal(900, groups[0].TotalRunTime);
            Assert.Equal("SN-A", groups[1].SN);
            Assert.Equal(2, groups[1].ResultCount);
            Assert.Equal(3500, groups[1].TotalRunTime);
            Assert.Equal("3.500 s", groups[1].TotalRunTimeText);
            Assert.Equal(start, groups[1].FirstTime);
            Assert.Equal(start.AddSeconds(3), groups[1].LastTime);
        }

        [Fact]
        public void CalculateSplitsRepeatedSnWhenProcessOrderRestarts()
        {
            DateTime start = new(2026, 7, 25, 10, 0, 0);
            CycleTimeResultSample[] samples =
            [
                new() { Id = 10, SN = "SN-A", TestType = 0, RunTime = 1000, CreateTime = start },
                new() { Id = 11, SN = "SN-A", TestType = 1, RunTime = 2000, CreateTime = start.AddSeconds(2) },
                new() { Id = 12, SN = "SN-A", TestType = 2, RunTime = 3000, CreateTime = start.AddSeconds(5) },
                new() { Id = 13, SN = "SN-A", TestType = 0, RunTime = 1100, CreateTime = start.AddMinutes(5) },
                new() { Id = 14, SN = "SN-A", TestType = 1, RunTime = 2100, CreateTime = start.AddMinutes(5).AddSeconds(2) }
            ];

            IReadOnlyList<CycleTimeGroup> groups = CycleTimeCalculator.Calculate(samples);

            Assert.Collection(groups,
                secondRun =>
                {
                    Assert.Equal(2, secondRun.ExecutionIndex);
                    Assert.Equal(2, secondRun.ResultCount);
                    Assert.Equal(3200, secondRun.TotalRunTime);
                    Assert.Equal(13, secondRun.FirstId);
                    Assert.Equal(14, secondRun.LatestId);
                },
                firstRun =>
                {
                    Assert.Equal(1, firstRun.ExecutionIndex);
                    Assert.Equal(3, firstRun.ResultCount);
                    Assert.Equal(6000, firstRun.TotalRunTime);
                    Assert.Equal(10, firstRun.FirstId);
                    Assert.Equal(12, firstRun.LatestId);
                });
        }

        [Fact]
        public void CalculateIgnoresRowsWithoutSn()
        {
            CycleTimeResultSample[] samples =
            [
                new() { Id = 1, SN = string.Empty, TestType = 0, RunTime = 1000, CreateTime = DateTime.Now },
                new() { Id = 2, SN = " ", TestType = 0, RunTime = 2000, CreateTime = DateTime.Now }
            ];

            Assert.Empty(CycleTimeCalculator.Calculate(samples));
        }
    }
}
