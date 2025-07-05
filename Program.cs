using System;
using GoogleDrivePaperlessImporter.Modules;

namespace GoogleDrivePaperlessImporter
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("[CORE] Initializing...");
            var drive = new GoogleDrive();
            var paperless = new Paperless();

            Console.WriteLine("[PROCESSOR] Starting...");
            var processor = new Processor(drive, paperless);
            processor.Run().Wait();
        }
    }
}