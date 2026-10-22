using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Tickets;
using SupportDesk.Domain.Users;

namespace SupportDesk.Infrastructure.Persistence;

public sealed class SupportDeskDbContext(DbContextOptions<SupportDeskDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDeskDbContext).Assembly);
}