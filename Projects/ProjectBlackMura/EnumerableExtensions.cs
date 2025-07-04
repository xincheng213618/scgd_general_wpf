namespace ProjectBlackMura
{
    public static class EnumerableExtensions
    {
        public static double AverageOrDefault<T>(this IEnumerable<T> source, Func<T, double> selector)
        {
            if (source == null || !source.Any())
                return 0.0;
            return source.Average(selector);
        }
    }
}
