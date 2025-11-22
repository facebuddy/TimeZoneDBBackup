using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;

namespace TimeZoneDBBackup
{
    internal class BackupService
    {
        private readonly string _backupDirectory;
        private readonly string _connectionString;
        private readonly int _commandTimeoutSeconds;

        private const int DefaultCommandTimeoutSeconds = 3600; // 1 hour for large databases

        public BackupService(string backupDirectory)
        {
            _backupDirectory = backupDirectory;
            _connectionString = BuildMasterConnectionString();
            _commandTimeoutSeconds = GetCommandTimeoutSeconds();
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
    }
}
