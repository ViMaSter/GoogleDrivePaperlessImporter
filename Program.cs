using GoogleDrivePaperlessImporter.Modules;

namespace GoogleDrivePaperlessImporter
{
    class Program
    {
        static void Main(string[] args)
        {
            var drive = new GoogleDrive();
            var paperless = new Paperless();

            var processor = new Processor(drive, paperless);
            processor.Run().Wait();
        }
    }
}