using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Tickets;

namespace SupportDesk.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(Ticket.TitleMaxLength);

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(Ticket.DescriptionMaxLength);

        // Stored as readable text instead of magic numbers.
        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(t => t.RequesterId).IsRequired();

        builder.HasIndex(t => new { t.Status, t.CreatedAt });
        builder.HasIndex(t => t.AssigneeId);

        // Customers list "my tickets" newest first, so this index serves their main query.
        builder.HasIndex(t => new { t.RequesterId, t.CreatedAt });
    }
}