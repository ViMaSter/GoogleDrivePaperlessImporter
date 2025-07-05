using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GoogleDrivePaperlessImporter.Modules
{
    internal class Paperless
    {
        private readonly HttpClient _client;
        public Paperless()
        {
            const string CONFIG_PATH = "config.json";
            var paperlessConfig = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText(CONFIG_PATH)).paperless;
            var absoluteInstanceURL = (string)paperlessConfig.absoluteInstanceURL;
            var username = (string)paperlessConfig.username;
            var password = (string)paperlessConfig.password;
            _client = new HttpClient { BaseAddress = new Uri(absoluteInstanceURL) };
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Console.WriteLine("[PAPERLESS] Authorizing...");
            var tokenResponse = _client.PostAsync("/api/token/", new StringContent(JsonConvert.SerializeObject(new Dictionary<string, string>()
            {
                {"username", username},
                {"password", password}
            }), Encoding.UTF8, "application/json")).Result;
            if (!tokenResponse.IsSuccessStatusCode)
            {
                var content = tokenResponse.StatusCode + ":" + tokenResponse.Content.ReadAsStringAsync().Result;
                File.WriteAllText("token-" + DateTimeOffset.Now.ToString("s").Replace(":", "-"), content);
                throw new NotSupportedException(content);
            }

            string token = (JsonConvert.DeserializeObject<dynamic>(tokenResponse.Content.ReadAsStringAsync().Result)).token;

            _client.DefaultRequestHeaders.Accept.Clear();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);
        }

        public void PostFile(string fileName, Stream fileStream)
        {
            using var formContent = new MultipartFormDataContent("NKdKd9Yk")
            {
                {new StreamContent(fileStream), "document", fileName}
            };

            try
            {
                var message = _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/documents/post_document/")
                {
                    Content = formContent
                }).Result;
                if (message.IsSuccessStatusCode)
                {
                    return;
                }
                var content = message.StatusCode + ":" + message.Content.ReadAsStringAsync().Result;
                File.WriteAllText("error-"+DateTimeOffset.Now.ToString("s").Replace(":", "-"), content);
                throw new NotSupportedException(content);
            }
            catch (Exception ex)
            {
                File.WriteAllText("exception-"+DateTimeOffset.Now.ToString("s").Replace(":", "-"), ex.ToString());
                throw;
            }
        }
    }
}