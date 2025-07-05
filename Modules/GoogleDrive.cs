using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using Google.Apis.Util;
using Newtonsoft.Json;
using File = Google.Apis.Drive.v3.Data.File;

namespace GoogleDrivePaperlessImporter.Modules
{
    class GoogleDrive
    {
        private readonly DriveService _driveService;
        private readonly string _credentialPath = "googleCredentials/token.json";
        private UserCredential _credentials;

        private void RefreshTokenIfRequried()
        {
            if (_credentials.Token.IsExpired(SystemClock.Default))
            {
                _credentials.RefreshTokenAsync(CancellationToken.None).Wait();
                // Persist the refreshed token
                using (var stream = new FileStream(_credentialPath, FileMode.Create, FileAccess.Write))
                {
                    new FileDataStore(Path.GetDirectoryName(_credentialPath), true).StoreAsync("user", _credentials).Wait();
                }
            }
        }

        public GoogleDrive()
        {
            string[] scopes = { DriveService.Scope.Drive };
            const string applicationName = "Drive API .NET Quickstart";
            const string configPath = "config.json";
            var googleDriveConfig = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(configPath)).googleDrive;

            ClientSecrets secrets = new ClientSecrets()
            {
            ClientId = googleDriveConfig.clientId,
            ClientSecret = googleDriveConfig.clientSecret
            };

            var dataStore = new FileDataStore(Path.GetDirectoryName(_credentialPath), true);

            // Try to load existing credentials
            _credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            scopes,
            "user",
            CancellationToken.None,
            dataStore
            ).Result;

            _driveService = new DriveService(new DriveService.Initializer
            {
            HttpClientInitializer = _credentials,
            ApplicationName = applicationName,
            });
        }

        public File FindFile(string filter)
        {
            RefreshTokenIfRequried();
            var listRequest = _driveService.Files.List();
            listRequest.PageSize = 10;
            listRequest.Q = filter;
            listRequest.Fields = "nextPageToken, files(id, name)";
            return listRequest.Execute().Files.FirstOrDefault();
        }

        public File MoveFile(File file, File sourceFolder, File targetFolder)
        {
            RefreshTokenIfRequried();
            var fileID = file.Id;
            
            var update = _driveService.Files.Update(new(), fileID);
            update.AddParents = targetFolder.Id;
            update.RemoveParents = sourceFolder.Id;
            return update.Execute();
        }

        public Stream GetFileContents(File file)
        {
            RefreshTokenIfRequried();
            var request = _driveService.Files.Get(file.Id);
            request.ModifyRequest = message =>
            {
                Debug.Assert(message.RequestUri != null, "message.RequestUri != null");
                message.RequestUri = new(message.RequestUri.AbsoluteUri + "?alt=media");
            };
            return request.ExecuteAsStream();
        }

        public void DeleteFile(File file)
        {
            var response = _driveService.Files.Delete(file.Id).Execute();
            if (!string.IsNullOrWhiteSpace(response))
            {
                throw new($"Deleting file '{file.Id}' failed: '{response}'");
            }
        }
    }
}