using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Auth;
using SupportDesk.Domain.Users;

namespace SupportDesk.Api.IntegrationTests;

/// <summary>
/// Hosts the real API in memory, pointed at the disposable SQL Server.
/// Clients are created already authenticated as an administrator; tests that need another
/// identity (or none) replace the Authorization header.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@supportdesk.test";
    public const string AdminPassword = "Admin_Passw0rd!2026";

    public ApiFactory()
    {
        // Environment variables take precedence over appsettings and user-secrets, and they are
        // read when Program starts, which makes these overrides reliable.
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__SupportDesk",
            SqlServerTestDatabase.ConnectionString);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", "integration-tests-signing-key-0123456789-abcdef");
        Environment.SetEnvironmentVariable("Seed__AdminEmail", AdminEmail);
        Environment.SetEnvironmentVariable("Seed__AdminPassword", AdminPassword);
    }

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);

        // Mint an administrator token with the real token service (no HTTP round trip needed).
        using var scope = Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();

        var admin = User.Create(
            "token-admin@supportdesk.test",
            "Token Admin",
            "not-a-real-hash",
            UserRole.Admin,
            DateTimeOffset.UtcNow);

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.CreateAccessToken(admin).Value);
    }
}