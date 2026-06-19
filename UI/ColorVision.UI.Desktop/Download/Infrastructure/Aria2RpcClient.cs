using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Desktop.Download
{
    internal sealed class Aria2RpcClient : IDisposable
    {
        private readonly Func<int> _getPort;
        private readonly string _secret;
        private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(3) };
        private int _requestId;

        public Aria2RpcClient(Func<int> getPort, string secret)
        {
            _getPort = getPort;
            _secret = secret;
        }

        private string RpcUrl => $"http://127.0.0.1:{_getPort()}/jsonrpc";

        public Task<JObject?> CallAsync(string method, params object[] parameters)
        {
            return CallAsync(method, CancellationToken.None, parameters);
        }

        public async Task<JObject?> CallAsync(string method, CancellationToken cancellationToken, params object[] parameters)
        {
            int id = Interlocked.Increment(ref _requestId);
            var rpcParameters = new object[parameters.Length + 1];
            rpcParameters[0] = $"token:{_secret}";
            Array.Copy(parameters, 0, rpcParameters, 1, parameters.Length);

            var request = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.ToString(),
                ["method"] = method,
                ["params"] = JToken.FromObject(rpcParameters)
            };

            string json = request.ToString(Formatting.None);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = await _httpClient.PostAsync(RpcUrl, content, cancellationToken).ConfigureAwait(false);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return JObject.Parse(responseBody);
        }

        public void Dispose()
        {
            _httpClient.Dispose();
        }
    }
}
