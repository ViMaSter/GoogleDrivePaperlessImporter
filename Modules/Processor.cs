using System;
using System.Threading.Tasks;
using Google.Apis.Drive.v3.Data;
using Newtonsoft.Json;

namespace GoogleDrivePaperlessImporter.Modules
{
    internal class Processor
    {
        private readonly GoogleDrive _googleDrive;
        private readonly Paperless _paperless;
        private readonly Backup _backup;
        private readonly File _sourceFolder;
        private readonly File _processingFolder;
        private readonly TimeSpan _pauseAfterCompletedList;

        public Processor(GoogleDrive googleDrive, Paperless paperless, Backup backup)
        {
            _googleDrive = googleDrive;
            _paperless = paperless;
            _backup = backup;

            _sourceFolder = _googleDrive.FindFile("trashed = false AND name='paperless'");
            _processingFolder = _googleDrive.FindFile($"trashed = false AND name='processing' AND '{_googleDrive.FindFile("trashed = false AND name='paperless'").Id}' IN parents");

            const string configPath = "config.json";
            var processorConfig = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(configPath)).processor;
            _pauseAfterCompletedList = TimeSpan.FromMinutes((double)processorConfig.pauseAfterCompletedListInMinutes);
        }

        public bool HasFiles()
        {
            var existingFileInProcessing = _googleDrive.FindFile($"trashed = false AND '{_processingFolder.Id}' IN parents");
            if (existingFileInProcessing != null)
            {
                return true;
            }

            var newFile = _googleDrive.FindFile($"trashed = false AND mimeType != 'application/vnd.google-apps.folder' AND '{ _sourceFolder.Id}' IN parents");
            if (newFile == null)
            {
                return false;
            }

            _googleDrive.MoveFile(newFile, _sourceFolder, _processingFolder);
            return true;
        }

        public void ProcessNextFile()
        {
            var nextFile = _googleDrive.FindFile($"trashed = false AND name!='processing' AND '{_processingFolder.Id}' IN parents");
            var stream = _googleDrive.GetFileContents(nextFile);
            _backup.StorePDF(nextFile.Name, stream);
            _paperless.PostFile(nextFile.Name, stream);
            _googleDrive.TrashFile(nextFile);
        }

        public Task Run()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    while (HasFiles())
                    {
                        ProcessNextFile();
                    }
                    Task.Delay((int)_pauseAfterCompletedList.TotalMilliseconds).Wait();
                }
                // ReSharper disable once FunctionNeverReturns Justification: Intended infinite loop
            });
        }
    }
}