#pragma warning disable xUnit1051

using SupportDesk.Application.Auth;
using Xunit;

namespace SupportDesk.Application.Tests.Auth;

public class AuthServiceListingTests
{
    private readonly AuthService _service = new(
        new FakeUserRepository(),
        new FakePasswordHasher(),
        new FakeTokenService(),
        TimeProvider.System);

    private Task RegisterAsync(string email) =>
        _service.RegisterAsync(
            new RegisterRequest(email, "Test User", "Str0ng_Passw0rd!"),
            CancellationToken.None);

    [Fact]
    public async Task Lists_users_page_by_page()
    {
        await RegisterAsync("one@example.com");
        await RegisterAsync("two@example.com");
        await RegisterAsync("three@example.com");

        var first = await _service.ListUsersAsync(1, 2, CancellationToken.None);
        var second = await _service.ListUsersAsync(2, 2, CancellationToken.None);

        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Equal(3, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
    }

    [Fact]
    public async Task Clamps_out_of_range_paging_values()
    {
        await RegisterAsync("one@example.com");

        var result = await _service.ListUsersAsync(0, 5000, CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Equal(AuthService.MaxPageSize, result.PageSize);
    }

    [Fact]
    public void The_listing_never_exposes_password_data()
    {
        var properties = typeof(UserResponse).GetProperties().Select(p => p.Name).ToList();

        Assert.DoesNotContain(properties, name => name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }
}
