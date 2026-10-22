using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportDesk.Domain.Users;

namespace SupportDesk.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(User.EmailMaxLength);

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(User.FullNameMaxLength);

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasMaxLength(User.PasswordHashMaxLength);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
    }
}