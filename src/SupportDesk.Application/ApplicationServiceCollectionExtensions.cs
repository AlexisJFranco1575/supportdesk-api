using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Tickets;

namespace SupportDesk.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}