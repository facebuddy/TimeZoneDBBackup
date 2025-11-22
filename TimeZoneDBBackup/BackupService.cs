using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace TimeZoneDBBackup
{
    internal class BackupService
    {
        private readonly string _backupDirectory;
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly string _zipDestinationDirectory;

        private const int DefaultCommandTimeoutSeconds = 3600; // 1 hour for large databases
        private const string DefaultZipDestinationDirectory = @"D:\\dbBackup";

        public BackupService(string backupDirectory)
        {
            _backupDirectory = backupDirectory;
            _connectionString = BuildMasterConnectionString();
            _commandTimeoutSeconds = GetCommandTimeoutSeconds();
            _zipDestinationDirectory = GetZipDestinationDirectory();
        }

        public bool BackupDatabase(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new ArgumentException("Database name must be provided.", nameof(databaseName));
            }

            Directory.CreateDirectory(_backupDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            var fileName = string.Format(CultureInfo.InvariantCulture, "{0}{1}.bak", databaseName, timestamp);
            var path = Path.Combine(_backupDirectory, fileName);
            var logFile = Path.Combine(_backupDirectory, "backup.log");

            // SQL Server Express does not support BACKUP ... WITH COMPRESSION, so omit it to keep backups compatible
            var sql = "BACKUP DATABASE [{0}] TO DISK = @path WITH COPY_ONLY, INIT, FORMAT";
            var commandText = string.Format(CultureInfo.InvariantCulture, sql, databaseName);

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[{0}] Starting backup for '{1}'...", timestamp, databaseName));

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@path", path);
                    command.CommandTimeout = _commandTimeoutSeconds;

                    connection.Open();
                    Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "[{0}] Connection to '{1}' established successfully.", timestamp, databaseName));
                    command.ExecuteNonQuery();
                }

                var successMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Backed up '{1}' to {2}", timestamp, databaseName, path);
                WriteLog(logFile, successMessage);
                Console.WriteLine(successMessage);

                try
                {
                    var zipPath = CreateZipArchive(path, databaseName, timestamp);
                    var zipSuccessMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Compressed '{1}' backup to {2}", timestamp, databaseName, zipPath);
                    WriteLog(logFile, zipSuccessMessage);
                    Console.WriteLine(zipSuccessMessage);
                }
                catch (Exception zipEx)
                {
                    var zipFailureMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Failed to compress backup for '{1}': {2}", timestamp, databaseName, zipEx.Message);
                    WriteLog(logFile, zipFailureMessage);
                    Console.Error.WriteLine(zipFailureMessage);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                var failureMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Failed to back up '{1}': {2}", timestamp, databaseName, ex.Message);
                WriteLog(logFile, failureMessage);

                Console.Error.WriteLine(failureMessage);

                return false;
            }
        }

        private static void WriteLog(string logFilePath, string message)
        {
            File.AppendAllText(logFilePath, message + Environment.NewLine);
        }

        private string CreateZipArchive(string backupFilePath, string databaseName, string timestamp)
        {
            Directory.CreateDirectory(_zipDestinationDirectory);

            var zipFileName = string.Format(CultureInfo.InvariantCulture, "{0}{1}.zip", databaseName, timestamp);
            var temporaryZipPath = Path.Combine(_backupDirectory, zipFileName);
            var destinationZipPath = Path.Combine(_zipDestinationDirectory, zipFileName);

            if (File.Exists(temporaryZipPath))
            {
                File.Delete(temporaryZipPath);
            }

            if (File.Exists(destinationZipPath))
            {
                File.Delete(destinationZipPath);
            }

            using (var archive = ZipFile.Open(temporaryZipPath, ZipArchiveMode.Create))
            {
                var entryName = Path.GetFileName(backupFilePath);
                archive.CreateEntryFromFile(backupFilePath, entryName, CompressionLevel.Optimal);
            }

            File.Move(temporaryZipPath, destinationZipPath);

            return destinationZipPath;
        }

        private static string BuildMasterConnectionString()
        {
            var rawConnection = ConfigurationManager.ConnectionStrings["KGCHRMContext"];
            if (rawConnection == null || string.IsNullOrWhiteSpace(rawConnection.ConnectionString))
            {
                throw new InvalidOperationException("KGCHRMContext connection string is missing.");
            }

            var builder = new SqlConnectionStringBuilder(rawConnection.ConnectionString)
            {
                InitialCatalog = "master"
            };

            return builder.ToString();
        }

        private static int GetCommandTimeoutSeconds()
        {
            var timeoutSetting = ConfigurationManager.AppSettings["BackupCommandTimeoutSeconds"];

            if (int.TryParse(timeoutSetting, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutSeconds) && timeoutSeconds >= 0)
            {
                return timeoutSeconds;
            }

            return DefaultCommandTimeoutSeconds;
        }

        private static string GetZipDestinationDirectory()
        {
            var configuredZipDirectory = ConfigurationManager.AppSettings["ZipBackupDirectory"];

            if (!string.IsNullOrWhiteSpace(configuredZipDirectory))
            {
                return configuredZipDirectory;
            }

            return DefaultZipDestinationDirectory;
        }
    }
}
