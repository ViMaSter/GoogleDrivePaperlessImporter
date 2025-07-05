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
        private readonly File _sourceFolder;
        private readonly File _processingFolder;
        private readonly TimeSpan _pauseAfterCompletedList;

        public Processor(GoogleDrive googleDrive, Paperless paperless)
        {
            _googleDrive = googleDrive;
            _paperless = paperless;

            _sourceFolder = _googleDrive.FindFile("trashed = false AND name='paperless'");
            _processingFolder = _googleDrive.FindFile($"trashed = false AND name='processing' AND '{_googleDrive.FindFile("trashed = false AND name='paperless'").Id}' IN parents");

            const string CONFIG_PATH = "config.json";
            var processorConfig = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(CONFIG_PATH)).processor;
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
            Console.WriteLine("[GOOGLE] Found new file: " + newFile.Name);

            _googleDrive.MoveFile(newFile, _sourceFolder, _processingFolder);
            return true;
        }

        public void ProcessNextFile()
        {
            var nextFile = _googleDrive.FindFile($"trashed = false AND name!='processing' AND '{_processingFolder.Id}' IN parents");
            var stream = _googleDrive.GetFileContents(nextFile);
            Console.WriteLine("[GOOGLE] Uploading to paperless: " + nextFile.Name);
            _paperless.PostFile(nextFile.Name, stream);
            _googleDrive.DeleteFile(nextFile);
        }

        public Task Run()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    Console.WriteLine("[LOOP] Checking for new files...");
                    while (HasFiles())
                    {
                        ProcessNextFile();
                    }
                    Console.WriteLine("[LOOP] Next check at " + DateTime.Now.Add(_pauseAfterCompletedList).ToString("O"));
                    Task.Delay((int)_pauseAfterCompletedList.TotalMilliseconds).Wait();
                }
                // ReSharper disable once FunctionNeverReturns Justification: Intended infinite loop
            });
        }
    }
}