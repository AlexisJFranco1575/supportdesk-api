using Microsoft.AspNetCore.Mvc.Testing;

namespace SupportDesk.Api.IntegrationTests;

/// <summary>Hosts the real API in memory, pointed at the disposable SQL Server.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory()
    {
        // Environment variables take precedence over appsettings and user-secrets, and they are
        // read when Program starts, which makes this override reliable.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SupportDesk",
            SqlServerTestDatabase.ConnectionString);
    }
}