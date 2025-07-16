using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace GoogleDrivePaperlessImporter.Modules
{
    public class Backup
    {
        private readonly string _backupDirectory;

        private void ScheduleOrDelete(string jsonFile, DateTime uploadedAt)
        {
            DateTime deleteAt = uploadedAt.AddDays(30);
            TimeSpan delay = deleteAt - DateTime.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                // Already expired, delete immediately
                DeleteFiles(jsonFile);
                return;
            }

            Task.Delay(delay).ContinueWith(_ =>
            {
                DeleteFiles(jsonFile);
            });
        }

        private void DeleteFiles(string jsonFile)
        {
            string pdfFile = Path.ChangeExtension(jsonFile, ".pdf");
            try
            {
                File.Delete(pdfFile);
                File.Delete(jsonFile);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting files: {ex.Message}");
            }
        }

        private class JsonMetadata
        {
            public string Timestamp { get; set; }
            public string FileName { get; set; }
            public DateTime UploadedAt { get; set; }
        }

        public Backup(string directoryPath)
        {
            _backupDirectory = directoryPath;
            if (!Directory.Exists(_backupDirectory))
            {
                Directory.CreateDirectory(_backupDirectory);
            }

            // Schedule deletion for each JSON file
            foreach (var jsonFile in Directory.GetFiles(_backupDirectory, "backup_*.json"))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonFile);
                    var metadata = JsonSerializer.Deserialize<JsonMetadata>(jsonContent);
                    if (metadata == null || !DateTime.TryParse(metadata.UploadedAt.ToString(), out DateTime uploadedAt))
                    {
                        Console.WriteLine($"Error parsing metadata for {jsonFile}");
                        return;
                    }
                    Console.WriteLine($"Scheduling deletion for: {jsonFile} at {uploadedAt.AddDays(30)}");
                    ScheduleOrDelete(jsonFile, uploadedAt);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {jsonFile}: {ex.Message}");
                }
            }
        }

        public void StorePDF(string fileName, Stream pdfStream)
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
            string pdfFileName = Path.Combine(_backupDirectory, $"{fileName}.pdf");
            string jsonFileName = Path.ChangeExtension(pdfFileName, ".json");

            // Save PDF
            using (var fileStream = new FileStream(pdfFileName, FileMode.Create, FileAccess.Write))
            {
                pdfStream.CopyTo(fileStream);
            }

            // Save JSON metadata
            var metadata = new
            {
                Timestamp = timestamp,
                FileName = pdfFileName,
                UploadedAt = DateTime.UtcNow
            };

            string json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(jsonFileName, json);

            // Schedule deletion after 30 days
            ScheduleOrDelete(jsonFileName, DateTime.UtcNow);
        }
    }
}