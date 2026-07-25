namespace ProjectARVRPro
{
    public sealed class CycleTimeResultSample
    {
        public int Id { get; set; }
        public string SN { get; set; } = string.Empty;
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public sealed class CycleTimeGroup
    {
        public string SN { get; init; } = string.Empty;
        public int ExecutionIndex { get; init; }
        public int ResultCount { get; init; }
        public long TotalRunTime { get; init; }
        public DateTime FirstTime { get; init; }
        public DateTime LastTime { get; init; }
        public int FirstId { get; init; }
        public int LatestId { get; init; }

        public string ExecutionText => $"第 {ExecutionIndex} 次";
        public string TotalRunTimeText => CycleTimeCalculator.FormatMilliseconds(TotalRunTime);
    }

    public static class CycleTimeCalculator
    {
        public static IReadOnlyList<CycleTimeGroup> Calculate(IEnumerable<CycleTimeResultSample> samples)
        {
            ArgumentNullException.ThrowIfNull(samples);

            var executions = new List<CycleTimeGroup>();
            foreach (IGrouping<string, CycleTimeResultSample> snGroup in samples
                .Where(sample => !string.IsNullOrWhiteSpace(sample.SN))
                .GroupBy(sample => sample.SN, StringComparer.Ordinal))
            {
                List<CycleTimeResultSample> currentExecution = [];
                int executionIndex = 1;

                foreach (CycleTimeResultSample sample in snGroup.OrderBy(sample => sample.Id))
                {
                    if (currentExecution.Count > 0 && sample.TestType <= currentExecution[^1].TestType)
                    {
                        executions.Add(CreateGroup(snGroup.Key, executionIndex++, currentExecution));
                        currentExecution = [];
                    }

                    currentExecution.Add(sample);
                }

                if (currentExecution.Count > 0)
                {
                    executions.Add(CreateGroup(snGroup.Key, executionIndex, currentExecution));
                }
            }

            return executions
                .OrderByDescending(group => group.LatestId)
                .ToList();
        }

        private static CycleTimeGroup CreateGroup(string sn, int executionIndex, List<CycleTimeResultSample> samples)
        {
            return new CycleTimeGroup
            {
                SN = sn,
                ExecutionIndex = executionIndex,
                ResultCount = samples.Count,
                TotalRunTime = samples.Sum(sample => sample.RunTime),
                FirstTime = samples.Min(sample => sample.CreateTime),
                LastTime = samples.Max(sample => sample.CreateTime),
                FirstId = samples.Min(sample => sample.Id),
                LatestId = samples.Max(sample => sample.Id)
            };
        }

        public static string FormatMilliseconds(long milliseconds)
        {
            return $"{milliseconds / 1000d:F3} s";
        }
    }
}
