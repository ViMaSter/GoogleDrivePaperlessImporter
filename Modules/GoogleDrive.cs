using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using File = Google.Apis.Drive.v3.Data.File;

namespace GoogleDrivePaperlessImporter.Modules
{
    internal class GoogleDrive
    {
        private readonly DriveService _driveService;
        private const string CREDENTIAL_PATH = "googleCredentials/token.json";
        private readonly UserCredential _credentials;

        private void RefreshTokenIfRequired()
        {
            if (!_credentials.Token.IsStale) return;
            Console.WriteLine("[GOOGLE] Refreshing token");
            _credentials.RefreshTokenAsync(CancellationToken.None).Wait();
            // Persist the refreshed token
            using var stream = new FileStream(CREDENTIAL_PATH, FileMode.Create, FileAccess.Write);
            new FileDataStore(Path.GetDirectoryName(CREDENTIAL_PATH), true).StoreAsync("user", _credentials).Wait();
        }

        public GoogleDrive()
        {
            string[] scopes = { DriveService.Scope.Drive };
            const string APPLICATION_NAME = "Drive API .NET Quickstart";
            const string CONFIG_PATH = "config.json";
            var googleDriveConfig = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(CONFIG_PATH)).googleDrive.credentials.installed;

            var secrets = new ClientSecrets()
            {
                ClientId = googleDriveConfig.client_id,
                ClientSecret = googleDriveConfig.client_secret
            };

            var dataStore = new FileDataStore(Path.GetDirectoryName(CREDENTIAL_PATH), true);

            // Try to load existing credentials
            Console.WriteLine("[GOOGLE] Authorizing...");
            _credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            scopes,
            "user",
            CancellationToken.None,
            dataStore
            ).Result;

            Console.WriteLine("[GOOGLE] Initializing Service...");
            _driveService = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = _credentials,
                ApplicationName = APPLICATION_NAME,
            });
        }

        public File FindFile(string filter)
        {
            RefreshTokenIfRequired();
            var listRequest = _driveService.Files.List();
            listRequest.PageSize = 10;
            listRequest.Q = filter;
            listRequest.Fields = "nextPageToken, files(id, name)";
            return listRequest.Execute().Files.FirstOrDefault();
        }

        public File MoveFile(File file, File sourceFolder, File targetFolder)
        {
            RefreshTokenIfRequired();
            var fileID = file.Id;

            var update = _driveService.Files.Update(new File(), fileID);
            update.AddParents = targetFolder.Id;
            update.RemoveParents = sourceFolder.Id;
            return update.Execute();
        }

        public Stream GetFileContents(File file)
        {
            RefreshTokenIfRequired();
            var request = _driveService.Files.Get(file.Id);
            request.ModifyRequest = message =>
            {
                Debug.Assert(message.RequestUri != null);
                message.RequestUri = new Uri(message.RequestUri.AbsoluteUri + "?alt=media");
            };
            return request.ExecuteAsStream();
        }

        public void DeleteFile(File file)
        {
            var response = _driveService.Files.Delete(file.Id).Execute();
            if (!string.IsNullOrWhiteSpace(response))
            {
                throw new Exception($"Deleting file '{file.Id}' failed: '{response}'");
            }
        }
    }
}