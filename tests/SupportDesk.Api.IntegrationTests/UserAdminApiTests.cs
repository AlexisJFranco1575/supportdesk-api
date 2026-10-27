#pragma warning disable xUnit1051

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Api.IntegrationTests;

public class UserAdminApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string StrongPassword = "Str0ng_Passw0rd!";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---------- helpers ----------

    private HttpClient Anonymous()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = null;
        return client;
    }

    /// <summary>Registers a brand-new customer, signs in and returns a client carrying their token.</summary>
    private async Task<(HttpClient Client, UserResponse User)> NewCustomerAsync()
    {
        var email = $"customer-{Guid.NewGuid():N}@example.com";

        var register = await Anonymous().PostAsJsonAsync(
            "/api/auth/register",
            new { email, fullName = "Test Customer", password = StrongPassword });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = (await register.Content.ReadFromJsonAsync<UserResponse>(Json))!;

        var login = await Anonymous().PostAsJsonAsync("/api/auth/login", new { email, password = StrongPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<LoginResponse>(Json))!.AccessToken;

        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return (client, user);
    }

    /// <summary>Reads the "sub" claim (the user id) from the bearer token a client is using.</summary>
    private static Guid SubjectOf(HttpClient client)
    {
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - (payload.Length % 4)) % 4), '=');

        using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
        return document.RootElement.GetProperty("sub").GetGuid();
    }

    // ---------- tests ----------

    [Fact]
    public async Task Anonymous_visitors_cannot_list_users()
    {
        var response = await Anonymous().GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Customers_cannot_list_users()
    {
        var (customer, _) = await NewCustomerAsync();

        var response = await customer.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admins_can_list_users_page_by_page_without_password_data()
    {
        await NewCustomerAsync();
        await NewCustomerAsync();
        await NewCustomerAsync();

        // The default client is authenticated as an administrator.
        var response = await factory.CreateClient().GetAsync("/api/users?page=1&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);

        var page = JsonSerializer.Deserialize<PagedResult<UserResponse>>(raw, Json)!;
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.PageSize);
        Assert.True(page.TotalCount >= 4);
    }

    [Fact]
    public async Task Admins_can_promote_a_customer_to_agent()
    {
        var (_, customer) = await NewCustomerAsync();

        var response = await factory.CreateClient().PutAsJsonAsync(
            $"/api/users/{customer.Id}/role",
            new { role = "Agent" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await response.Content.ReadFromJsonAsync<UserResponse>(Json))!;
        Assert.Equal(UserRole.Agent, updated.Role);
    }

    [Fact]
    public async Task Customers_cannot_change_roles()
    {
        var (customer, user) = await NewCustomerAsync();

        var response = await customer.PutAsJsonAsync($"/api/users/{user.Id}/role", new { role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_admin_cannot_change_their_own_role()
    {
        var admin = factory.CreateClient();
        var adminId = SubjectOf(admin);

        var response = await admin.PutAsJsonAsync($"/api/users/{adminId}/role", new { role = "Customer" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
