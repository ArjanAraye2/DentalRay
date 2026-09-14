using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DentalRay.SetupHelper
{
    internal class Program
    {
        // ========================================================
        // Main
        // ========================================================
        //
        // این برنامه توسط Installer اجرا خواهد شد.
        //
        // وظایف:
        //
        // 1. اتصال به SQL Server
        // 2. صبر کردن تا SQL Server آماده شود
        // 3. اجرای DentalRay.Database.Install.sql
        // 4. آماده‌سازی دسترسی Windows Service برنامه
        //
        // ========================================================

        static async Task<int> Main(
            string[] args)
        {
            try
            {
                Console.WriteLine(
                    "DentalRay Setup Helper"
                );

                Console.WriteLine(
                    "----------------------"
                );


                // ------------------------------------------------
                // دریافت Server Name
                // ------------------------------------------------
                //
                // مثال:
                //
                // .\DENTALRAY
                //
                // ------------------------------------------------

                string serverName =
                    GetArgumentValue(
                        args,
                        "--server")
                    ??
                    @".\DENTALRAY";


                // ------------------------------------------------
                // مسیر SQL Script
                // ------------------------------------------------

                string scriptPath =
                    GetArgumentValue(
                        args,
                        "--script")
                    ??
                    Path.Combine(
                        AppContext.BaseDirectory,
                        "Database",
                        "DentalRay.Database.Install.sql"
                    );


                Console.WriteLine(
                    $"SQL Server: {serverName}"
                );

                Console.WriteLine(
                    $"SQL Script: {scriptPath}"
                );


                // ------------------------------------------------
                // بررسی وجود Script
                // ------------------------------------------------

                if (!File.Exists(
                    scriptPath))
                {
                    Console.Error.WriteLine(
                        "Database installation script was not found."
                    );

                    return 10;
                }


                // =================================================
                // Connection String اولیه
                // =================================================
                //
                // ابتدا به master وصل می‌شویم.
                //
                // Windows Authentication استفاده می‌کنیم چون
                // Installer با Administrator اجرا خواهد شد.
                //
                // =================================================

                string masterConnectionString =
                    new SqlConnectionStringBuilder
                    {
                        DataSource =
                            serverName,

                        InitialCatalog =
                            "master",

                        IntegratedSecurity =
                            true,

                        TrustServerCertificate =
                            true,

                        Encrypt =
                            true,

                        ConnectTimeout =
                            5
                    }
                    .ConnectionString;


                // =================================================
                // صبر برای آماده‌شدن SQL Server
                // =================================================

                bool sqlReady =
                    await WaitForSqlServerAsync(
                        masterConnectionString,
                        maxAttempts: 30,
                        delaySeconds: 2
                    );


                if (!sqlReady)
                {
                    Console.Error.WriteLine(
                        "SQL Server did not become ready in time."
                    );

                    return 20;
                }


                Console.WriteLine(
                    "SQL Server is ready."
                );


                // =================================================
                // خواندن Script
                // =================================================

                string sqlScript =
                    await File.ReadAllTextAsync(
                        scriptPath
                    );


                // =================================================
                // اجرای Script
                // =================================================

                await ExecuteSqlScriptAsync(
                    masterConnectionString,
                    sqlScript
                );


                Console.WriteLine(
                    "DentalRay database script completed."
                );


                // =================================================
                // آماده‌سازی دسترسی Windows Service
                // =================================================
                //
                // DentalRay به صورت Windows Service و با حساب
                // LocalSystem اجرا خواهد شد.
                //
                // بنابراین SQL Login مربوط به:
                //
                // NT AUTHORITY\SYSTEM
                //
                // را ایجاد و به دیتابیس DentalRay دسترسی می‌دهیم.
                //
                // =================================================

                await ConfigureServiceDatabaseAccessAsync(
                    masterConnectionString
                );


                Console.WriteLine(
                    "DentalRay service database access configured."
                );


                Console.WriteLine(
                    "Setup Helper completed successfully."
                );


                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    "DentalRay Setup Helper failed."
                );

                Console.Error.WriteLine(
                    ex.ToString()
                );


                return 100;
            }
        }


        // ========================================================
        // GetArgumentValue
        // ========================================================
        //
        // مثال:
        //
        // --server .\DENTALRAY
        //
        // ========================================================

        private static string? GetArgumentValue(
            string[] args,
            string argumentName)
        {
            for (
                int i = 0;
                i < args.Length;
                i++)
            {
                if (!string.Equals(
                    args[i],
                    argumentName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }


                if (i + 1 >=
                    args.Length)
                {
                    return null;
                }


                return args[i + 1];
            }


            return null;
        }


        // ========================================================
        // WaitForSqlServerAsync
        // ========================================================
        //
        // بعد از نصب SQL Express ممکن است Service چند ثانیه
        // برای آماده‌شدن زمان نیاز داشته باشد.
        //
        // بنابراین چند بار اتصال را امتحان می‌کنیم.
        //
        // ========================================================

        private static async Task<bool>
            WaitForSqlServerAsync(
                string connectionString,
                int maxAttempts,
                int delaySeconds)
        {
            for (
                int attempt = 1;
                attempt <= maxAttempts;
                attempt++)
            {
                try
                {
                    await using
                    SqlConnection connection =
                        new SqlConnection(
                            connectionString
                        );


                    await connection
                        .OpenAsync();


                    await using
                    SqlCommand command =
                        new SqlCommand(
                            "SELECT 1;",
                            connection
                        );


                    await command
                        .ExecuteScalarAsync();


                    return true;
                }
                catch
                {
                    Console.WriteLine(
                        $"Waiting for SQL Server... " +
                        $"Attempt {attempt}/{maxAttempts}"
                    );


                    await Task.Delay(
                        TimeSpan.FromSeconds(
                            delaySeconds
                        )
                    );
                }
            }


            return false;
        }


        // ========================================================
        // ExecuteSqlScriptAsync
        // ========================================================
        //
        // SQL Server خود کلمه GO را نمی‌شناسد.
        //
        // GO مربوط به ابزارهایی مانند SSMS و sqlcmd است.
        //
        // بنابراین Script را به Batchهای جدا تقسیم می‌کنیم.
        //
        // ========================================================

        private static async Task
            ExecuteSqlScriptAsync(
                string connectionString,
                string script)
        {
            string[] batches =
                Regex.Split(
                    script,
                    @"^\s*GO\s*;?\s*$",
                    RegexOptions.Multiline |
                    RegexOptions.IgnoreCase
                );


            await using
            SqlConnection connection =
                new SqlConnection(
                    connectionString
                );


            await connection.OpenAsync();


            foreach (
                string batch
                in batches)
            {
                string sql =
                    batch.Trim();


                if (string.IsNullOrWhiteSpace(
                    sql))
                {
                    continue;
                }


                await using
                SqlCommand command =
                    new SqlCommand(
                        sql,
                        connection
                    );


                // Database creation ممکن است کمی زمان ببرد.
                command.CommandTimeout =
                    120;


                await command
                    .ExecuteNonQueryAsync();
            }
        }


        // ========================================================
        // ConfigureServiceDatabaseAccessAsync
        // ========================================================
        //
        // Windows Service فعلی DentalRay تحت LocalSystem اجرا
        // خواهد شد.
        //
        // بنابراین:
        //
        // Login:
        // NT AUTHORITY\SYSTEM
        //
        // را روی SQL Server ایجاد می‌کنیم.
        //
        // در Database DentalRay نیز User متناظر ساخته می‌شود.
        //
        // سپس فقط دسترسی‌های موردنیاز برنامه داده می‌شود:
        //
        // db_datareader
        // db_datawriter
        //
        // ========================================================

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

IF IS_ROLEMEMBER(
    N'db_datareader',
    N'NT AUTHORITY\SYSTEM'
) <> 1
BEGIN
    ALTER ROLE [db_datareader]
    ADD MEMBER [NT AUTHORITY\SYSTEM];
END;

IF IS_ROLEMEMBER(
    N'db_datawriter',
    N'NT AUTHORITY\SYSTEM'
) <> 1
BEGIN
    ALTER ROLE [db_datawriter]
    ADD MEMBER [NT AUTHORITY\SYSTEM];
END;
";


            await using
            SqlConnection connection =
                new SqlConnection(
                    masterConnectionString
                );


            await connection.OpenAsync();


            await using
            SqlCommand command =
                new SqlCommand(
                    sql,
                    connection
                );


            command.CommandTimeout =
                60;


            await command
                .ExecuteNonQueryAsync();
        }
    }
}