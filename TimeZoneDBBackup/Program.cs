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
                try
                {
                    service.BackupDatabase(database);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to back up database '{database}': {ex.Message}");
                }
            }

            Console.WriteLine("Backup process completed.");
        }
    }
}
