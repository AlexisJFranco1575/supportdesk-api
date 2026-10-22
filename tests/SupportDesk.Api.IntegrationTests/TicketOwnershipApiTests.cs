#pragma warning disable xUnit1051

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using Xunit;

namespace SupportDesk.Api.IntegrationTests;

public class TicketOwnershipApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    private async Task<(HttpClient Client, Guid UserId)> NewCustomerAsync()
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

        return (client, user.Id);
    }

    private static async Task<TicketResponse> CreateTicketAsync(HttpClient client, string title)
    {
        var response = await client.PostAsJsonAsync(
            "/api/tickets",
            new { title, description = "Something is broken.", priority = "Low" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<TicketResponse>(Json))!;
    }

    // ---------- tests ----------

    [Fact]
    public async Task A_new_ticket_records_the_authenticated_user_as_the_requester()
    {
        var (client, userId) = await NewCustomerAsync();

        var ticket = await CreateTicketAsync(client, "My ticket");

        Assert.Equal(userId, ticket.RequesterId);
    }

    [Fact]
    public async Task A_customer_cannot_read_another_customers_ticket()
    {
        var (owner, _) = await NewCustomerAsync();
        var (stranger, _) = await NewCustomerAsync();
        var ticket = await CreateTicketAsync(owner, "Private ticket");

        var asStranger = await stranger.GetAsync($"/api/tickets/{ticket.Id}");
        var asOwner = await owner.GetAsync($"/api/tickets/{ticket.Id}");

        Assert.Equal(HttpStatusCode.NotFound, asStranger.StatusCode);
        Assert.Equal(HttpStatusCode.OK, asOwner.StatusCode);
    }

    [Fact]
    public async Task A_customer_only_lists_their_own_tickets()
    {
        var (first, firstId) = await NewCustomerAsync();
        var (second, secondId) = await NewCustomerAsync();

        var firstTicket = await CreateTicketAsync(first, "First customer ticket");
        await CreateTicketAsync(first, "First customer second ticket");
        var secondTicket = await CreateTicketAsync(second, "Second customer ticket");

        var firstList = (await first.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?pageSize=100", Json))!;
        var secondList = (await second.GetFromJsonAsync<PagedResult<TicketResponse>>("/api/tickets?pageSize=100", Json))!;

        Assert.Equal(2, firstList.TotalCount);
        Assert.All(firstList.Items, t => Assert.Equal(firstId, t.RequesterId));
        Assert.Contains(firstList.Items, t => t.Id == firstTicket.Id);
        Assert.DoesNotContain(firstList.Items, t => t.Id == secondTicket.Id);

        Assert.Equal(1, secondList.TotalCount);
        Assert.All(secondList.Items, t => Assert.Equal(secondId, t.RequesterId));
    }

    [Fact]
    public async Task Staff_can_read_any_customers_ticket()
    {
        var (customer, _) = await NewCustomerAsync();
        var ticket = await CreateTicketAsync(customer, "Needs an agent");

        // The default client is authenticated as an administrator.
        var response = await factory.CreateClient().GetAsync($"/api/tickets/{ticket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}