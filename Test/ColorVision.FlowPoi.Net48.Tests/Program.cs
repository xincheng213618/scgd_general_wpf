using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ColorVision.FlowPoi.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.FlowPoi.Tests
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var tests = new List<KeyValuePair<string, Func<Task>>>
            {
                Test("Name request, segmented UTF-8, large JSON, distinct X/x", FragmentedResponse),
                Test("Template ID request", () => IdRequest(42)),
                Test("Local negative template ID request", () => IdRequest(-1000000001)),
                Test("Coalesced replies match both event and message ID", CoalescedReplies),
                Test("Business failure preserves batch identity", BusinessFailure),
                Test("Missing chromaticity cannot become zero", MissingCoordinate),
                Test("Older host without correlated handler fails immediately", MissingHandler),
                Test("Timeout closes connection without resending", ResponseTimeout),
                Test("Caller cancellation unblocks pending read", CallerCancellation),
                Test("Disconnect before reply never succeeds", EarlyDisconnect),
                Test("Truncated JSON never becomes a completed measurement", TruncatedResponse),
                Test("Invalid selectors are rejected", InvalidSelectors)
            };
            if (args.Length == 2 && args[0] == "--demo")
                tests.Add(Test("Packaged console executable calls TCP and writes result", () => ConsoleDemo(args[1])));
            int failed = 0;
            foreach (var test in tests)
            {
                try
                {
                    await test.Value().ConfigureAwait(false);
                    Console.WriteLine("PASS " + test.Key);
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.Error.WriteLine("FAIL " + test.Key + ": " + ex);
                }
            }
            Console.WriteLine("{0}/{1} passed", tests.Count - failed, tests.Count);
            return failed == 0 ? 0 : 1;
        }

        private static KeyValuePair<string, Func<Task>> Test(string name, Func<Task> run)
        {
            return new KeyValuePair<string, Func<Task>>(name, run);
        }

        private static async Task FragmentedResponse()
        {
            string pointName = "中心{点} \\\"测试\\\" " + new string('白', 5000);
            await WithServer(async (request, stream) =>
            {
                Equal("FlowRunPoi", (string)request["EventName"]);
                Equal("sample-1", (string)request["SerialNumber"]);
                Equal(JTokenType.String, request["Params"].Type);
                JObject parameters = JObject.Parse((string)request["Params"]);
                Equal(1, parameters.Count);
                Equal("白画面", (string)parameters["FlowName"]);
                JObject response = Response(request);
                response["Data"]["Poi"][0]["Name"] = pointName;
                byte[] bytes = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));
                // Split at every byte for the prefix, then span multiple stream buffers.
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int count = Math.Min(offset < 100 ? 1 : 127, bytes.Length - offset);
                    await stream.WriteAsync(bytes, offset, count).ConfigureAwait(false);
                    offset += count;
                    await Task.Yield();
                }
            }, async port =>
            {
                FlowPoiResponse response = await new FlowPoiClient("127.0.0.1", port)
                    .RunByNameAsync(" 白画面 ", "sample-1", TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Equal(true, response.IsSuccess);
                Equal(10d, response.Data.Poi[0].X);
                Equal(0.3, response.Data.Poi[0].ChromaticityX);
                Equal(20d, response.Data.Poi[0].Y);
                Equal(0.4, response.Data.Poi[0].ChromaticityY);
                Equal(pointName, response.Data.Poi[0].Name);
            }).ConfigureAwait(false);
        }

        private static Task IdRequest(int id)
        {
            return WithServer(async (request, stream) =>
            {
                JObject parameters = JObject.Parse((string)request["Params"]);
                Equal(1, parameters.Count);
                Equal(id, (int)parameters["FlowId"]);
                await Send(stream, Response(request)).ConfigureAwait(false);
                // Keep the connection open: the client must finish on the JSON
                // boundary, without requiring a newline or server disconnect.
                Equal(0, await stream.ReadAsync(new byte[1], 0, 1).ConfigureAwait(false));
            }, async port =>
            {
                Equal(200, (await new FlowPoiClient("127.0.0.1", port).RunByIdAsync(id).ConfigureAwait(false)).Code);
            });
        }

        private static Task CoalescedReplies()
        {
            return WithServer(async (request, stream) =>
            {
                JObject wrongId = Response(request);
                wrongId["MsgID"] = "other-request";
                wrongId["Code"] = 422;
                JObject wrongEvent = Response(request);
                wrongEvent["EventName"] = "OtherEvent";
                wrongEvent["Code"] = 409;
                string json = wrongId.ToString(Formatting.None) + wrongEvent.ToString(Formatting.None)
                    + Response(request).ToString(Formatting.None);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }, async port => Equal(200, (await new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42).ConfigureAwait(false)).Code));
        }

        private static Task BusinessFailure()
        {
            return WithServer(async (request, stream) =>
            {
                JObject response = Response(request);
                response["Code"] = 422;
                response["Msg"] = "本批次无 POI";
                response["Data"]["Poi"] = new JArray();
                await Send(stream, response).ConfigureAwait(false);
            }, async port =>
            {
                FlowPoiResponse response = await new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42).ConfigureAwait(false);
                Equal(false, response.IsSuccess);
                Equal(422, response.Code);
                Equal(137, response.Data.BatchId);
                Equal(0, response.Data.Poi.Count);
            });
        }

        private static Task MissingCoordinate()
        {
            return WithServer(async (request, stream) =>
            {
                JObject response = Response(request);
                ((JObject)response["Data"]["Poi"][0]).Remove("x");
                await Send(stream, response).ConfigureAwait(false);
            }, port => ThrowsAsync<JsonSerializationException>(() => new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42)));
        }

        private static Task ResponseTimeout()
        {
            return WithServer(async (request, stream) =>
            {
                byte[] remaining = new byte[1];
                Equal(0, await stream.ReadAsync(remaining, 0, 1).ConfigureAwait(false));
            }, port => ThrowsAsync<TimeoutException>(() => new FlowPoiClient("127.0.0.1", port)
                .RunByIdAsync(42, timeout: TimeSpan.FromMilliseconds(300))));
        }

        private static Task MissingHandler()
        {
            return WithServer((request, stream) => Send(stream, new JObject
            {
                ["Code"] = 404, ["Msg"] = "Handler not found for event: FlowRunPoi"
            }), port => ThrowsAsync<InvalidDataException>(() => new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42)));
        }

        private static async Task CallerCancellation()
        {
            using (var cancel = new CancellationTokenSource())
            {
                await WithServer(async (request, stream) =>
                {
                    cancel.Cancel();
                    byte[] remaining = new byte[1];
                    Equal(0, await stream.ReadAsync(remaining, 0, 1).ConfigureAwait(false));
                }, port => ThrowsAsync<OperationCanceledException>(() => new FlowPoiClient("127.0.0.1", port)
                    .RunByIdAsync(42, cancellationToken: cancel.Token))).ConfigureAwait(false);
            }
        }

        private static Task EarlyDisconnect()
        {
            return WithServer((request, stream) => Task.CompletedTask,
                port => ThrowsAsync<EndOfStreamException>(() => new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42)));
        }

        private static Task TruncatedResponse()
        {
            return WithServer(async (request, stream) =>
            {
                byte[] bytes = Encoding.UTF8.GetBytes("{\"EventName\":\"FlowRunPoi\",\"MsgID\":");
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }, port => ThrowsAsync<JsonReaderException>(() => new FlowPoiClient("127.0.0.1", port).RunByIdAsync(42)));
        }

        private static async Task InvalidSelectors()
        {
            var client = new FlowPoiClient("127.0.0.1", 1);
            await ThrowsAsync<ArgumentException>(() => client.RunByNameAsync(" ")).ConfigureAwait(false);
            await ThrowsAsync<ArgumentOutOfRangeException>(() => client.RunByIdAsync(0)).ConfigureAwait(false);
            await ThrowsAsync<ArgumentOutOfRangeException>(() => client.RunByIdAsync(42, timeout: TimeSpan.Zero)).ConfigureAwait(false);
        }

        private static async Task ConsoleDemo(string executable)
        {
            string output = Path.Combine(Path.GetTempPath(), "flow-poi-client-" + Guid.NewGuid().ToString("N") + ".json");
            await WithServer((request, stream) => Send(stream, Response(request)), async port =>
            {
                var start = new ProcessStartInfo(executable,
                    "--host 127.0.0.1 --port " + port + " --id 42 --timeout 5 --output \"" + output + "\"")
                {
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (Process process = Process.Start(start))
                {
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    if (!await Task.Run(() => process.WaitForExit(10000)).ConfigureAwait(false))
                    {
                        process.Kill();
                        throw new InvalidOperationException("Console demo did not exit.");
                    }
                    string display = await stdout.ConfigureAwait(false);
                    string errors = await stderr.ConfigureAwait(false);
                    if (process.ExitCode != 0) throw new InvalidOperationException(display + errors);
                    Equal(true, display.Contains("Code=200"));
                    Equal(137, (int)JObject.Parse(File.ReadAllText(output))["Data"]["BatchId"]);
                }
            }).ConfigureAwait(false);
            File.Delete(output);
        }

        private static JObject Response(JObject request)
        {
            JObject response = JObject.Parse(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "flow-poi-response.json")));
            response["MsgID"] = (string)request["MsgID"];
            response["SerialNumber"] = (string)request["SerialNumber"];
            return response;
        }

        private static async Task Send(NetworkStream stream, JObject response)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }

        private static async Task WithServer(Func<JObject, NetworkStream, Task> serve, Func<int, Task> call)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            TcpClient accepted = null;
            listener.Start();
            using (var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (guard.Token.Register(() => { listener.Stop(); if (accepted != null) accepted.Close(); }))
            {
                Task server = Task.Run(async () =>
                {
                    using (accepted = await listener.AcceptTcpClientAsync().ConfigureAwait(false))
                    using (NetworkStream stream = accepted.GetStream())
                    using (var text = new StreamReader(stream, Encoding.UTF8, false, 8192, true))
                    using (var reader = new JsonTextReader(text) { CloseInput = false })
                    {
                        JObject request = await JObject.LoadAsync(reader, guard.Token).ConfigureAwait(false);
                        await serve(request, stream).ConfigureAwait(false);
                    }
                });
                try
                {
                    await call(((IPEndPoint)listener.LocalEndpoint).Port).ConfigureAwait(false);
                    await server.ConfigureAwait(false);
                    Equal(false, listener.Pending());
                }
                finally
                {
                    listener.Stop();
                    if (accepted != null) accepted.Close();
                    try { await server.ConfigureAwait(false); } catch { if (!server.IsCompleted) throw; }
                }
            }
        }

        private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try { await action().ConfigureAwait(false); }
            catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException("Expected " + expected + ", actual " + actual);
        }
    }
}
