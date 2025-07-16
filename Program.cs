using GoogleDrivePaperlessImporter.Modules;

namespace GoogleDrivePaperlessImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var drive = new GoogleDrive();
            var paperless = new Paperless();
            var backup = new Backup("backup");

            var processor = new Processor(drive, paperless, backup);
            processor.Run().Wait();
        }
    }
}