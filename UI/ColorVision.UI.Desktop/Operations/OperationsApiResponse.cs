namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsApiResponse
    {
        public int StatusCode { get; init; }

        public string ContentType { get; init; } = "application/json; charset=utf-8";

        public string Body { get; init; } = string.Empty;

        public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();
    }
}
