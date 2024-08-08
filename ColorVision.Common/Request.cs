#pragma warning disable CS8625
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace ColorVision.Common
{
    public class Request
    {
        private static readonly HttpClient client = new HttpClient();

        // 异步POST方法
        public static async Task<string> PostAsync(string url, Dictionary<string, string> data, Dictionary<string, string> headers = null)
        {
            var content = new FormUrlEncodedContent(data ?? new Dictionary<string, string>());

            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }
            return await response.Content.ReadAsStringAsync();
        }

        // 同步POST方法
        public static string Post(string url, Dictionary<string, string> data, Dictionary<string, string> headers = null)
        {
            var content = new FormUrlEncodedContent(data ?? new Dictionary<string, string>());

            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = client.PostAsync(url, content).Result;
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }
            return response.Content.ReadAsStringAsync().Result;
        }

        // 异步GET方法
        public static async Task<string> GetAsync(string url, Dictionary<string, string> data = null, Dictionary<string, string> headers = null)
        {
            if (data != null)
            {
                var uriBuilder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                foreach (var kvp in data)
                {
                    query[kvp.Key] = kvp.Value;
                }

                uriBuilder.Query = query.ToString();
                url = uriBuilder.ToString();
            }

            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }

            return await response.Content.ReadAsStringAsync();
        }

        // 同步GET方法
        public static string Get(string url, Dictionary<string, string> data = null, Dictionary<string, string> headers = null)
        {
            if (data != null)
            {
                var uriBuilder = new UriBuilder(url);
                var query = HttpUtility.ParseQueryString(uriBuilder.Query);

                foreach (var kvp in data)
                {
                    query[kvp.Key] = kvp.Value;
                }

                uriBuilder.Query = query.ToString();
                url = uriBuilder.ToString();
            }

            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }

            return response.Content.ReadAsStringAsync().Result;
        }

        // 异步文件下载方法
        public static async Task DownloadFileAsync(string url, string destinationPath, Dictionary<string, string> headers = null)
        {
            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }

            using (var stream = await response.Content.ReadAsStreamAsync())
            {
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
        }

        // 同步文件下载方法
        public static void DownloadFile(string url, string destinationPath, Dictionary<string, string> headers = null)
        {
            // 添加headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
            }

            var response = client.GetAsync(url).Result;
            response.EnsureSuccessStatusCode();

            // 清除headers
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.Remove(header.Key);
                }
            }

            using (var stream = response.Content.ReadAsStreamAsync().Result)
            {
                using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.CopyTo(fileStream);
                }
            }
        }
    }


}
