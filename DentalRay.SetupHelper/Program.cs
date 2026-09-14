using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace DentalRay.SetupHelper
{
    internal class Program
    {
        // این Helper هم برای تشخیص Instance و هم برای Upgrade دیتابیس
        // توسط Installer اجرا می‌شود.
        static async Task<int> Main(string[] args)
        {
            try
            {
                string? detectOutput =
                    GetArgumentValue(args, "--detect-output");

                if (!string.IsNullOrWhiteSpace(detectOutput))
                {
                    string? detected =
                        await DetectBestSqlServerAsync();

                    string ini =
                        "[Sql]" +
                        Environment.NewLine +
                        "Server=" +
                        (detected ?? string.Empty) +
                        Environment.NewLine;

                    await File.WriteAllTextAsync(
                        detectOutput,
                        ini);

                    return 0;
                }

                string serverName =
                    GetArgumentValue(args, "--server") ??
                    @".\DENTALRAY";

                if (HasArgument(args, "--probe"))
                {
                    return await CanConnectAsync(
                        serverName,
                        3)
                        ? 0
                        : 20;
                }

                string scriptPath =
                    GetArgumentValue(args, "--script") ??
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "Database",
                        "DentalRay.Database.Install.sql");

                if (!File.Exists(scriptPath))
                {
                    Console.Error.WriteLine(
                        "Database installation script was not found.");

                    return 10;
                }

                string masterConnectionString =
                    BuildMasterConnectionString(
                        serverName,
                        5);

                bool sqlReady =
                    await WaitForSqlServerAsync(
                        masterConnectionString,
                        3,
                        1);

                if (!sqlReady)
                {
                    Console.Error.WriteLine(
                        "SQL Server is not reachable.");

                    return 20;
                }

                string sqlScript =
                    await File.ReadAllTextAsync(
                        scriptPath);

                await ExecuteSqlScriptAsync(
                    masterConnectionString,
                    sqlScript);

                await ConfigureServiceDatabaseAccessAsync(
                    masterConnectionString);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 100;
            }
        }

        private static bool HasArgument(
            string[] args,
            string argumentName)
        {
            return args.Any(
                item =>
                    string.Equals(
                        item,
                        argumentName,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static string? GetArgumentValue(
            string[] args,
            string argumentName)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(
                    args[i],
                    argumentName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return i + 1 < args.Length
                    ? args[i + 1]
                    : null;
            }

            return null;
        }

        private static string BuildMasterConnectionString(
            string serverName,
            int timeoutSeconds)
        {
            return new SqlConnectionStringBuilder
            {
                DataSource = serverName,
                InitialCatalog = "master",
                IntegratedSecurity = true,
                TrustServerCertificate = true,
                Encrypt = true,
                ConnectTimeout = timeoutSeconds
            }
            .ConnectionString;
        }

        private static async Task<bool> CanConnectAsync(
            string serverName,
            int connectTimeoutSeconds)
        {
            try
            {
                await using var connection =
                    new SqlConnection(
                        BuildMasterConnectionString(
                            serverName,
                            connectTimeoutSeconds));

                await connection.OpenAsync();

                await using var command =
                    new SqlCommand(
                        "SELECT 1;",
                        connection);

                await command.ExecuteScalarAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Instanceهای نصب‌شده را از Registry پیدا می‌کند و فقط
        // Instanceای را برمی‌گرداند که اتصال واقعی به آن موفق باشد.
        private static async Task<string?>
            DetectBestSqlServerAsync()
        {
            var candidates =
                new List<string>();

            foreach (RegistryView view in new[]
            {
                RegistryView.Registry64,
                RegistryView.Registry32
            })
            {
                try
                {
                    using RegistryKey baseKey =
                        RegistryKey.OpenBaseKey(
                            RegistryHive.LocalMachine,
                            view);

                    using RegistryKey? key =
                        baseKey.OpenSubKey(
                            @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");

                    if (key == null)
                    {
                        continue;
                    }

                    foreach (string instanceName
                             in key.GetValueNames())
                    {
                        string candidate =
                            string.Equals(
                                instanceName,
                                "MSSQLSERVER",
                                StringComparison.OrdinalIgnoreCase)
                                ? "."
                                : @".\" + instanceName;

                        if (!candidates.Contains(
                            candidate,
                            StringComparer.OrdinalIgnoreCase))
                        {
                            candidates.Add(candidate);
                        }
                    }
                }
                catch
                {
                    // Registry discovery best effort است.
                }
            }

            foreach (string fallback in new[]
            {
                @".\DENTALRAY",
                @".\SQLEXPRESS",
                "."
            })
            {
                if (!candidates.Contains(
                    fallback,
                    StringComparer.OrdinalIgnoreCase))
                {
                    candidates.Add(fallback);
                }
            }

            foreach (string candidate in candidates)
            {
                if (await CanConnectAsync(
                    candidate,
                    2))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static async Task<bool>
            WaitForSqlServerAsync(
                string connectionString,
                int maxAttempts,
                int delaySeconds)
        {
            for (int attempt = 1;
                 attempt <= maxAttempts;
                 attempt++)
            {
                try
                {
                    await using var connection =
                        new SqlConnection(
                            connectionString);

                    await connection.OpenAsync();

                    await using var command =
                        new SqlCommand(
                            "SELECT 1;",
                            connection);

                    await command.ExecuteScalarAsync();

                    return true;
                }
                catch
                {
                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(
                                delaySeconds));
                    }
                }
            }

            return false;
        }

        private static async Task ExecuteSqlScriptAsync(
            string connectionString,
            string script)
        {
            string[] batches =
                Regex.Split(
                    script,
                    @"^\s*GO\s*;?\s*$",
                    RegexOptions.Multiline |
                    RegexOptions.IgnoreCase);

            await using var connection =
                new SqlConnection(
                    connectionString);

            await connection.OpenAsync();

            foreach (string batch in batches)
            {
                string sql =
                    batch.Trim();

                if (string.IsNullOrWhiteSpace(sql))
                {
                    continue;
                }

                await using var command =
                    new SqlCommand(
                        sql,
                        connection);

                command.CommandTimeout = 120;

                await command.ExecuteNonQueryAsync();
            }
        }

        private static async Task
            ConfigureServiceDatabaseAccessAsync(
                string masterConnectionString)
        {
            const string sql = @"
IF NOT EXISTS
(
    SELECT 1
    FROM sys.server_principals
    WHERE name = N'NT AUTHORITY\SYSTEM'
)
BEGIN
    CREATE LOGIN [NT AUTHORITY\SYSTEM]
    FROM WINDOWS;
END;

USE [DentalRay];

IF NOT EXISTS
(
    SELECT 1
    FROM sys.database_principals
    WHERE name = N'NT AUTHORITY\SYSTEM'
)
BEGIN
    CREATE USER [NT AUTHORITY\SYSTEM]
    FOR LOGIN [NT AUTHORITY\SYSTEM];
END;

IF IS_ROLEMEMBER(N'db_datareader', N'NT AUTHORITY\SYSTEM') <> 1
BEGIN
    ALTER ROLE [db_datareader]
    ADD MEMBER [NT AUTHORITY\SYSTEM];
END;

IF IS_ROLEMEMBER(N'db_datawriter', N'NT AUTHORITY\SYSTEM') <> 1
BEGIN
    ALTER ROLE [db_datawriter]
    ADD MEMBER [NT AUTHORITY\SYSTEM];
END;
";

            await using var connection =
                new SqlConnection(
                    masterConnectionString);

            await connection.OpenAsync();

            await using var command =
                new SqlCommand(
                    sql,
                    connection);

            command.CommandTimeout = 60;

            await command.ExecuteNonQueryAsync();
        }
    }
}
