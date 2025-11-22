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

        public BackupService(string backupDirectory)
        {
            _backupDirectory = backupDirectory;
            _connectionString = BuildMasterConnectionString();
        }

        public void BackupDatabase(string databaseName)
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

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                using (var command = new SqlCommand(commandText, connection))
                {
                    command.Parameters.AddWithValue("@path", path);

                    connection.Open();
                    command.ExecuteNonQuery();
                }

                var successMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Backed up '{1}' to {2}", timestamp, databaseName, path);
                WriteLog(logFile, successMessage);
                Console.WriteLine(successMessage);
            }
            catch (Exception ex)
            {
                var failureMessage = string.Format(CultureInfo.InvariantCulture, "[{0}] Failed to back up '{1}': {2}", timestamp, databaseName, ex.Message);
                WriteLog(logFile, failureMessage);
                throw;
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
    }
}
