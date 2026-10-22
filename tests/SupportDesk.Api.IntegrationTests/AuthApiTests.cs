#pragma warning disable xUnit1051

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportDesk.Application.Auth;
using SupportDesk.Domain.Users;
using Xunit;

namespace SupportDesk.Api.IntegrationTests;

public class AuthApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private HttpClient WithToken(string token)
    {
        var client = Anonymous();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static async Task<UserResponse> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new { email, fullName = "Test User", password = StrongPassword });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<UserResponse>(Json))!;
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password = StrongPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var login = (await response.Content.ReadFromJsonAsync<LoginResponse>(Json))!;
        return login.AccessToken;
    }

    private static async Task<Guid> CreateTicketAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tickets",
            new { title = "Cannot log in", description = "The login page shows an error.", priority = "Low" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    // ---------- tests ----------

    [Fact]
    public async Task Tickets_require_authentication()
    {
        var response = await Anonymous().GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_garbage_token_is_rejected()
    {
        var response = await WithToken("this-is-not-a-jwt").GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_creates_a_customer_and_never_returns_the_password()
    {
        var email = NewEmail();

        var response = await Anonymous().PostAsJsonAsync(
            "/api/auth/register",
            new { email, fullName = "Ana Perez", password = StrongPassword });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(StrongPassword, raw);
        Assert.DoesNotContain("passwordHash", raw, StringComparison.OrdinalIgnoreCase);

        var user = JsonSerializer.Deserialize<UserResponse>(raw, Json)!;
        Assert.Equal(email, user.Email);
        Assert.Equal(UserRole.Customer, user.Role);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_409()
    {
        var email = NewEmail();
        await RegisterAsync(Anonymous(), email);

        var response = await Anonymous().PostAsJsonAsync(
            "/api/auth/register",
            new { email = email.ToUpperInvariant(), fullName = "Someone Else", password = StrongPassword });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_reports_every_invalid_field_with_a_400()
    {
        var response = await Anonymous().PostAsJsonAsync(
            "/api/auth/register",
            new { email = "nope", fullName = "", password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var errors = problem.GetProperty("errors");

        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("fullName", out _));
        Assert.True(errors.TryGetProperty("password", out _));
    }

    [Fact]
    public async Task Login_returns_a_bearer_token_that_opens_protected_endpoints()
    {
        var email = NewEmail();
        await RegisterAsync(Anonymous(), email);

        var token = await LoginAsync(Anonymous(), email);

        var response = await WithToken(token).GetAsync("/api/tickets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_password_and_an_unknown_email_get_the_same_401()
    {
        var email = NewEmail();
        await RegisterAsync(Anonymous(), email);

        var wrongPassword = await Anonymous().PostAsJsonAsync(
            "/api/auth/login", new { email, password = "definitely-wrong-password" });
        var unknownEmail = await Anonymous().PostAsJsonAsync(
            "/api/auth/login", new { email = NewEmail(), password = StrongPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var first = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
        var second = await unknownEmail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("detail").GetString(), second.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Me_returns_the_identity_carried_by_the_token()
    {
        var email = NewEmail();
        await RegisterAsync(Anonymous(), email);
        var token = await LoginAsync(Anonymous(), email);

        var me = await WithToken(token).GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal(email, me.GetProperty("email").GetString());
        Assert.Equal("Customer", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task A_customer_can_open_a_ticket_but_cannot_work_on_it()
    {
        var email = NewEmail();
        await RegisterAsync(Anonymous(), email);
        var customer = WithToken(await LoginAsync(Anonymous(), email));

        var ticketId = await CreateTicketAsync(customer);

        var start = await customer.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/start", new { assigneeId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    [Fact]
    public async Task Only_administrators_can_change_roles()
    {
        var email = NewEmail();
        var user = await RegisterAsync(Anonymous(), email);
        var customer = WithToken(await LoginAsync(Anonymous(), email));

        var response = await customer.PutAsJsonAsync($"/api/users/{user.Id}/role", new { role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task An_administrator_can_promote_a_customer_who_then_works_on_tickets()
    {
        var email = NewEmail();
        var user = await RegisterAsync(Anonymous(), email);

        // The default client is authenticated as an administrator.
        var promote = await factory.CreateClient().PutAsJsonAsync(
            $"/api/users/{user.Id}/role", new { role = "Agent" });
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);

        // The role travels inside the token, so the user signs in again to receive it.
        var agent = WithToken(await LoginAsync(Anonymous(), email));
        var ticketId = await CreateTicketAsync(agent);

        var start = await agent.PostAsJsonAsync(
            $"/api/tickets/{ticketId}/start", new { assigneeId = user.Id });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
    }

    [Fact]
    public async Task The_seeded_administrator_can_sign_in()
    {
        var token = await LoginAsync(Anonymous(), ApiFactory.AdminEmail, ApiFactory.AdminPassword);

        var me = await WithToken(token).GetFromJsonAsync<JsonElement>("/api/auth/me");

        Assert.Equal("Admin", me.GetProperty("role").GetString());
    }
}