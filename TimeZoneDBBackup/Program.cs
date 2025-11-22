using System;
using System.Configuration;
using System.IO;

namespace TimeZoneDBBackup
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var backupDirectory = ConfigurationManager.AppSettings["BackupDirectory"];
            if (string.IsNullOrWhiteSpace(backupDirectory))
            {
                backupDirectory = @"D:\\AutoBackup";
            }

            var databases = new[]
            {
                "TZKLLDB",
                "KCLHRM"
            };

            Console.WriteLine("Starting database backup...");
            var service = new BackupService(backupDirectory);

            foreach (var database in databases)
            {
                var success = service.BackupDatabase(database);

                if (!success)
                {
                    Console.Error.WriteLine($"Backup failed for '{database}'. See log for details.");
                }
            }

            Console.WriteLine("Backup process completed.");

            Console.WriteLine("Press any key to close the application...");
            Console.ReadKey();
        }
    }
}
