using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using File = Google.Apis.Drive.v3.Data.File;

namespace GoogleDrivePaperlessImporter.Modules
{
    class GoogleDrive
    {
        private readonly DriveService _driveService;
        public GoogleDrive()
        {
            string[] scopes = { DriveService.Scope.Drive };
            const string applicationName = "Drive API .NET Quickstart";
            const string credentialPath = "googleCredentials";

            const string configPath = "config.json";
            var googleDriveConfig = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(configPath)).googleDrive;
            
            var credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(googleDriveConfig.credentials)))).Secrets,
                scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credentialPath, true)).Result;

            _driveService = new(new()
            {
                HttpClientInitializer = credentials,
                ApplicationName = applicationName,
            });
        }

        public File FindFile(string filter)
        {
            var listRequest = _driveService.Files.List();
            listRequest.PageSize = 10;
            listRequest.Q = filter;
            listRequest.Fields = "nextPageToken, files(id, name)";
            return listRequest.Execute().Files.FirstOrDefault();
        }

        public File MoveFile(File file, File sourceFolder, File targetFolder)
        {
            var fileID = file.Id;
            
            var update = _driveService.Files.Update(new(), fileID);
            update.AddParents = targetFolder.Id;
            update.RemoveParents = sourceFolder.Id;
            return update.Execute();
        }

        public Stream GetFileContents(File file)
        {
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