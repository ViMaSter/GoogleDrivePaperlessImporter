using System;
using System.Threading.Tasks;
using Google.Apis.Drive.v3.Data;
using Newtonsoft.Json;
using Serilog;

namespace GoogleDrivePaperlessImporter.Modules
{
    internal class Processor
    {
        private readonly ILogger _logger;
        private readonly GoogleDrive _googleDrive;
        private readonly Paperless _paperless;
        private readonly File _sourceFolder;
        private readonly File _processingFolder;
        private readonly TimeSpan _pauseAfterCompletedList;

        public Processor(ILogger logger, GoogleDrive googleDrive, Paperless paperless, ProcessorOptions options)
        {
            _logger = logger;
            _googleDrive = googleDrive;
            _paperless = paperless;

            _sourceFolder = _googleDrive.FindFile("trashed = false AND name='paperless'");
            _processingFolder = _googleDrive.FindFile($"trashed = false AND name='processing' AND '{_googleDrive.FindFile("trashed = false AND name='paperless'").Id}' IN parents");

            const string CONFIG_PATH = "config.json";
            _pauseAfterCompletedList = TimeSpan.FromMinutes((double)options.PauseAfterCompletedListInMinutes);
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

            _logger.Information("Found new file: {FileName}...", newFile.Name);

            _googleDrive.MoveFile(newFile, _sourceFolder, _processingFolder);
            return true;
        }

        public void ProcessNextFile()
        {
            var nextFile = _googleDrive.FindFile($"trashed = false AND name!='processing' AND '{_processingFolder.Id}' IN parents");
            var stream = _googleDrive.GetFileContents(nextFile);
            _logger.Information("Uploading to paperless: {FileName}...", nextFile.Name);
            _paperless.PostFile(nextFile.Name, stream);
            _googleDrive.DeleteFile(nextFile);
        }

        public Task Run()
        {
            return Task.Run(() =>
            {
                while (true)
                {
                    _logger.Information("Checking for new files...");
                    while (HasFiles())
                    {
                        ProcessNextFile();
                    }
                    _logger.Information("Next check at {Timestamp:yyyy-MM-dd HH:mm:ss.fff}", DateTime.Now.Add(_pauseAfterCompletedList));
                    Task.Delay((int)_pauseAfterCompletedList.TotalMilliseconds).Wait();
                }
                // ReSharper disable once FunctionNeverReturns Justification: Intended infinite loop
            });
        }
    }
}