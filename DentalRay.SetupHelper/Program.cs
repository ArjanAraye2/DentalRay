using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace DentalRay.SetupHelper
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                if (HasFlag(args, "--discover"))
                    return await DiscoverAsync(GetArgumentValue(args, "--output"));

                string serverName = GetArgumentValue(args, "--server") ?? @".\DENTALRAY";
                string masterConnectionString = BuildMasterConnectionString(serverName);

                if (!await WaitForSqlServerAsync(masterConnectionString, 30, 2))
                {
                    Console.Error.WriteLine("SQL Server is not reachable.");
                    return 20;
                }

                // Installer must never create or modify the DentalRay database.
                bool databaseExists = await DatabaseExistsAsync(masterConnectionString, "DentalRay");
                if (!databaseExists)
                {
                    Console.Error.WriteLine("DentalRay database does not exist.");
                    return 30;
                }

                await ConfigureServiceDatabaseAccessAsync(masterConnectionString);

                Console.WriteLine("DentalRay database exists and is ready.");
                return 0;
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"SQL Error {ex.Number}: {ex.Message}");
                return 40;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.ToString());
                return 100;
            }
        }

        private static string BuildMasterConnectionString(string serverName) =>
            new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                Encrypt = true,
                ConnectTimeout = 5
            }.ConnectionString;

        private static async Task<int> DiscoverAsync(string? outputPath)
        {
            var candidates = DiscoverSqlInstances().Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            if (!candidates.Contains(".", StringComparer.OrdinalIgnoreCase))
                candidates.Add(".");

            var results = new List<(string Server, bool Reachable, bool HasDentalRay)>();

            foreach (string server in candidates)
            {
                try
                {
                    string cs = BuildMasterConnectionString(server);
                    if (!await CanConnectAsync(cs))
                        continue;

                    bool hasDb = await DatabaseExistsAsync(cs, "DentalRay");
                    results.Add((server, true, hasDb));
                }
                catch
                {
                    // Instance نصب‌شده ولی غیرقابل دسترسی، از انتخاب خودکار حذف می‌شود.
                }
            }

            var selected = results.FirstOrDefault(x => x.HasDentalRay);
            if (string.IsNullOrWhiteSpace(selected.Server))
                selected = results.FirstOrDefault(x => x.Reachable);

            if (string.IsNullOrWhiteSpace(selected.Server))
            {
                Console.Error.WriteLine("No reachable SQL Server instance was found.");
                return 21;
            }

            string target = outputPath ?? Path.Combine(Path.GetTempPath(), "DentalRay.SqlDiscovery.txt");
            await File.WriteAllTextAsync(target,
                $"{selected.Server}|{(selected.HasDentalRay ? "EXISTS" : "MISSING")}");

            Console.WriteLine(selected.Server);
            Console.WriteLine(selected.HasDentalRay ? "EXISTS" : "MISSING");
            return 0;
        }

        private static IEnumerable<string> DiscoverSqlInstances()
        {
            var names = new List<string>();

            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey? key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view)
                        .OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");

                    if (key != null)
                    {
                        foreach (string instance in key.GetValueNames())
                        {
                            if (string.Equals(instance, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase))
                                names.Add(".");
                            else
                                names.Add($".\\{instance}");
                        }
                    }
                }
                catch { }
            }

            return names;
        }

        private static async Task<bool> CanConnectAsync(string connectionString)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            return connection.State == System.Data.ConnectionState.Open;
        }

        private static async Task<bool> DatabaseExistsAsync(string masterConnectionString, string databaseName)
        {
            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(
                "SELECT CASE WHEN DB_ID(@name) IS NULL THEN 0 ELSE 1 END;",
                connection);
            command.Parameters.AddWithValue("@name", databaseName);
            return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
        }

        private static async Task<bool> WaitForSqlServerAsync(
            string connectionString, int maxAttempts, int delaySeconds)
        {
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    if (await CanConnectAsync(connectionString))
                        return true;
                }
                catch { }

                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
            }

            return false;
        }

        private static async Task ConfigureServiceDatabaseAccessAsync(string masterConnectionString)
        {
            const string sql = @"
IF NOT EXISTS
(
    SELECT 1 FROM sys.server_principals
    WHERE name = N'NT AUTHORITY\SYSTEM'
)
BEGIN
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS;
END;

USE [DentalRay];

IF NOT EXISTS
(
    SELECT 1 FROM sys.database_principals
    WHERE name = N'NT AUTHORITY\SYSTEM'
)
BEGIN
    CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
END;

IF IS_ROLEMEMBER(N'db_datareader', N'NT AUTHORITY\SYSTEM') <> 1
    ALTER ROLE [db_datareader] ADD MEMBER [NT AUTHORITY\SYSTEM];

IF IS_ROLEMEMBER(N'db_datawriter', N'NT AUTHORITY\SYSTEM') <> 1
    ALTER ROLE [db_datawriter] ADD MEMBER [NT AUTHORITY\SYSTEM];
";

            await using var connection = new SqlConnection(masterConnectionString);
            await connection.OpenAsync();

            await using var command = new SqlCommand(sql, connection)
            {
                CommandTimeout = 60
            };
            await command.ExecuteNonQueryAsync();
        }

        private static bool HasFlag(string[] args, string name) =>
            args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

        private static string? GetArgumentValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return null;
        }
    }
}