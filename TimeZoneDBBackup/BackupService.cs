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

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            var fileName = string.Format(CultureInfo.InvariantCulture, "{0}_{1}.bak", databaseName, timestamp);
            var path = Path.Combine(_backupDirectory, fileName);

            var sql = "BACKUP DATABASE [{0}] TO DISK = @path WITH COPY_ONLY, INIT, FORMAT, COMPRESSION";
            var commandText = string.Format(CultureInfo.InvariantCulture, sql, databaseName);

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(commandText, connection))
            {
                command.Parameters.AddWithValue("@path", path);

                connection.Open();
                command.ExecuteNonQuery();
            }

            Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Backed up '{0}' to {1}", databaseName, path));
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
