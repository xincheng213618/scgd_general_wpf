using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Desktop.Download
{
    internal interface IAria2RpcClient : IDisposable
    {
        Task<JObject> CallAsync(string method, params object[] parameters);
        Task<JObject> CallAsync(string method, CancellationToken cancellationToken, params object[] parameters);
        Task<Aria2StatusResult[]> GetStatusesAsync(string[] gids, string[] keys, CancellationToken cancellationToken);
    }

    internal readonly record struct Aria2StatusResult(string Gid, JObject? Status, string? Error);
    internal sealed class Aria2RpcException(string message) : Exception(message);

    internal sealed class Aria2RpcClient : IAria2RpcClient
    {
        private readonly Func<int> _getPort;
        private readonly string _secret;
        private readonly HttpClient _httpClient;
        private int _requestId;

        public Aria2RpcClient(Func<int> getPort, string secret, HttpMessageHandler? handler = null)
        {
            _getPort = getPort;
            _secret = secret;
            _httpClient = new HttpClient(handler ?? new SocketsHttpHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };
        }

        public Task<JObject> CallAsync(string method, params object[] parameters) => CallAsync(method, CancellationToken.None, parameters);

        public Task<JObject> CallAsync(string method, CancellationToken cancellationToken, params object[] parameters)
        {
            var authenticatedParameters = new object[parameters.Length + 1];
            authenticatedParameters[0] = $"token:{_secret}";
            Array.Copy(parameters, 0, authenticatedParameters, 1, parameters.Length);
            return SendAsync(method, authenticatedParameters, cancellationToken);
        }

        public async Task<Aria2StatusResult[]> GetStatusesAsync(string[] gids, string[] keys, CancellationToken cancellationToken)
        {
            // system.multicall has no outer token; each nested aria2 call authenticates separately.
            var calls = gids.Select(gid => new { methodName = "aria2.tellStatus", @params = new object[] { $"token:{_secret}", gid, keys } }).ToArray();
            JObject response = await SendAsync("system.multicall", new object[] { calls }, cancellationToken).ConfigureAwait(false);
            if (response["result"] is not JArray results || results.Count != gids.Length)
                throw new Aria2RpcException("aria2 returned an invalid status batch.");
            return results.Select((item, index) => item is JArray values && values.First is JObject status
                ? new Aria2StatusResult(gids[index], status, null)
                : new Aria2StatusResult(gids[index], null, item["message"]?.ToString() ?? "aria2 status is unavailable.")).ToArray();
        }

        private async Task<JObject> SendAsync(string method, object[] parameters, CancellationToken cancellationToken)
        {
            var request = new JObject
            {
                ["jsonrpc"] = "2.0", ["id"] = Interlocked.Increment(ref _requestId).ToString(),
                ["method"] = method, ["params"] = JToken.FromObject(parameters)
            };
            using var content = new StringContent(request.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync($"http://127.0.0.1:{_getPort()}/jsonrpc", content, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            JObject result = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            if (result["error"] is JToken error)
                throw new Aria2RpcException(error["message"]?.ToString() ?? "aria2 RPC failed.");
            if (result["result"] == null)
                throw new Aria2RpcException("aria2 RPC returned no result.");
            return result;
        }

        public void Dispose() => _httpClient.Dispose();
    }
}
