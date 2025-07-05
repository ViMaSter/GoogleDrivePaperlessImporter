using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Serilog;

namespace GoogleDrivePaperlessImporter.Modules
{
    internal class Paperless
    {
        private readonly ILogger _logger;
        private readonly HttpClient _client;
        
        public Paperless(ILogger logger, PaperlessOptions options)
        {
            _logger = logger;
            _client = new HttpClient { BaseAddress = new Uri(options.AbsoluteInstanceURL) };
            _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _logger.Information("Authorizing...");
            var tokenResponse = _client.PostAsync("/api/token/", new StringContent(JsonConvert.SerializeObject(new Dictionary<string, string>()
            {
                {"username", options.Username},
                {"password", options.Password}
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
                    _logger.Information("File {FileName} uploaded successfully.", fileName);
                    return;
                }
                _logger.Error("Failed to upload file {FileName}. Status code: {StatusCode} Content: {Content}", fileName, message.StatusCode, message.Content.ReadAsStringAsync().Result);
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