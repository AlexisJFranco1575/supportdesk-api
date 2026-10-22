using Microsoft.EntityFrameworkCore;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Infrastructure.Persistence;

public sealed class SupportDeskDbContext(DbContextOptions<SupportDeskDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDeskDbContext).Assembly);
}