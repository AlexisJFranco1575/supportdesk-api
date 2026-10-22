#pragma warning disable xUnit1051

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets;
using SupportDesk.Domain.Tickets;
using Xunit;

namespace SupportDesk.Api.IntegrationTests;

public class TicketsApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly Guid Agent = Guid.Parse("11111111-1111-4111-8111-111111111111");

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _client = factory.CreateClient();

    // ---------- helpers ----------

    private async Task<TicketResponse> CreateTicketAsync(string title = "Printer is down", string priority = "High")
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new { title, description = "Something is broken.", priority });

        return await ReadAsync<TicketResponse>(response);
    }

    private Task<HttpResponseMessage> PostActionAsync(Guid id, string action, object? body = null)
    {
        var url = $"/api/tickets/{id}/{action}";

        return body is null
            ? _client.PostAsync(url, null)
            : _client.PostAsJsonAsync(url, body);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        Assert.True(
            response.IsSuccessStatusCode,
            $"Unexpected status {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<TicketResponse> MoveToClosedAsync(Guid id)
    {
        await ReadAsync<TicketResponse>(await PostActionAsync(id, "start", new { assigneeId = Agent }));
        await ReadAsync<TicketResponse>(await PostActionAsync(id, "resolve"));
        return await ReadAsync<TicketResponse>(await PostActionAsync(id, "close"));
    }

    // ---------- tests ----------

    [Fact]
    public async Task Liveness_and_readiness_probes_report_healthy()
    {
        var live = await _client.GetAsync("/health/live");
        var ready = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    [Fact]
    public async Task Create_returns_201_with_a_location_and_the_ticket_can_be_read_back()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new { title = "  Printer is down ", description = "3rd floor", priority = "High" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = (await response.Content.ReadFromJsonAsync<TicketResponse>(Json))!;
        Assert.Equal("Printer is down", created.Title);
        Assert.Equal(TicketStatus.Open, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.EndsWith($"/api/tickets/{created.Id}", response.Headers.Location!.ToString());

        var loaded = await ReadAsync<TicketResponse>(await _client.GetAsync($"/api/tickets/{created.Id}"));
        Assert.Equal(created.Id, loaded.Id);
        Assert.Equal(created.Title, loaded.Title);
        Assert.Equal(created.CreatedAt, loaded.CreatedAt);
    }

    [Fact]
    public async Task Creating_a_ticket_without_a_title_returns_422()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new { title = "  ", description = "Something", priority = "Low" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Business rule violated", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_unknown_priority_value_returns_400()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tickets",
            new { title = "A title", description = "A description", priority = "Banana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_ticket_returns_404()
    {
        var response = await _client.GetAsync($"/api/tickets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Resource not found", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A_ticket_moves_through_the_workflow_and_the_state_is_persisted()
    {
        var ticket = await CreateTicketAsync();

        var started = await ReadAsync<TicketResponse>(
            await PostActionAsync(ticket.Id, "start", new { assigneeId = Agent }));
        Assert.Equal(TicketStatus.InProgress, started.Status);
        Assert.Equal(Agent, started.AssigneeId);

        var resolved = await ReadAsync<TicketResponse>(await PostActionAsync(ticket.Id, "resolve"));
        Assert.Equal(TicketStatus.Resolved, resolved.Status);
        Assert.NotNull(resolved.ResolvedAt);

        var closed = await ReadAsync<TicketResponse>(await PostActionAsync(ticket.Id, "close"));
        Assert.Equal(TicketStatus.Closed, closed.Status);

        var loaded = await ReadAsync<TicketResponse>(await _client.GetAsync($"/api/tickets/{ticket.Id}"));
        Assert.Equal(TicketStatus.Closed, loaded.Status);
        Assert.Equal(Agent, loaded.AssigneeId);
        Assert.NotNull(loaded.ResolvedAt);
    }

    [Fact]
    public async Task An_invalid_transition_returns_422_and_leaves_the_ticket_untouched()
    {
        var ticket = await CreateTicketAsync();

        var response = await PostActionAsync(ticket.Id, "resolve");

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var loaded = await ReadAsync<TicketResponse>(await _client.GetAsync($"/api/tickets/{ticket.Id}"));
        Assert.Equal(TicketStatus.Open, loaded.Status);
    }

    [Fact]
    public async Task Reopening_a_resolved_ticket_clears_the_assignee_and_resolution_date()
    {
        var ticket = await CreateTicketAsync();
        await ReadAsync<TicketResponse>(await PostActionAsync(ticket.Id, "start", new { assigneeId = Agent }));
        await ReadAsync<TicketResponse>(await PostActionAsync(ticket.Id, "resolve"));

        var reopened = await ReadAsync<TicketResponse>(await PostActionAsync(ticket.Id, "reopen"));

        Assert.Equal(TicketStatus.Open, reopened.Status);
        Assert.Null(reopened.AssigneeId);
        Assert.Null(reopened.ResolvedAt);
    }

    [Fact]
    public async Task Priority_can_be_changed_until_the_ticket_is_closed()
    {
        var ticket = await CreateTicketAsync(priority: "Low");

        var changed = await ReadAsync<TicketResponse>(await PatchPriorityAsync(ticket.Id, "Critical"));
        Assert.Equal(TicketPriority.Critical, changed.Priority);

        await MoveToClosedAsync(ticket.Id);

        var refused = await PatchPriorityAsync(ticket.Id, "Low");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, refused.StatusCode);
    }

    [Fact]
    public async Task List_paginates_and_returns_the_newest_tickets_first()
    {
        await CreateTicketAsync("Older ticket");
        await CreateTicketAsync("Middle ticket");
        var newest = await CreateTicketAsync("Newest ticket");

        var page = await ReadAsync<PagedResult<TicketResponse>>(
            await _client.GetAsync("/api/tickets?page=1&pageSize=2"));

        Assert.Equal(2, page.Items.Count);
        Assert.Equal(newest.Id, page.Items[0].Id);
        Assert.True(page.TotalCount >= 3);
        Assert.True(page.TotalPages >= 2);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var ticket = await CreateTicketAsync();
        await MoveToClosedAsync(ticket.Id);

        var page = await ReadAsync<PagedResult<TicketResponse>>(
            await _client.GetAsync("/api/tickets?status=Closed&pageSize=100"));

        Assert.Contains(page.Items, t => t.Id == ticket.Id);
        Assert.All(page.Items, t => Assert.Equal(TicketStatus.Closed, t.Status));
    }

    [Fact]
    public async Task An_invalid_status_filter_returns_400()
    {
        var response = await _client.GetAsync("/api/tickets?status=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_huge_page_size_is_clamped()
    {
        var page = await ReadAsync<PagedResult<TicketResponse>>(
            await _client.GetAsync("/api/tickets?pageSize=1000"));

        Assert.Equal(TicketService.MaxPageSize, page.PageSize);
    }

    private Task<HttpResponseMessage> PatchPriorityAsync(Guid id, string priority)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/tickets/{id}/priority")
        {
            Content = JsonContent.Create(new { priority })
        };

        return _client.SendAsync(request);
    }
}