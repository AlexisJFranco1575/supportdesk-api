using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Infrastructure.Persistence;
using Testcontainers.MsSql;

namespace SupportDesk.Api.IntegrationTests;

/// <summary>
/// One disposable SQL Server container for the whole test run. The schema is created by applying
/// the real EF Core migrations, so the tests also verify that the migrations work.
/// Testcontainers' resource reaper removes the container when the test process ends.
/// </summary>
internal static class SqlServerTestDatabase
{
    private static readonly Lazy<string> Connection =
        new(Start, LazyThreadSafetyMode.ExecutionAndPublication);

    public static string ConnectionString => Connection.Value;

    private static string Start()
    {
        var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-CU14-ubuntu-22.04").Build();
        container.StartAsync().GetAwaiter().GetResult();

        var connectionString = new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = "SupportDeskTests"
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<SupportDeskDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        using var db = new SupportDeskDbContext(options);
        db.Database.Migrate();

        return connectionString;
    }
}