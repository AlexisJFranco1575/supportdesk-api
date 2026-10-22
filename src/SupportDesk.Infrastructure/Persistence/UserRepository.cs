using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Auth;
using SupportDesk.Application.Common;
using SupportDesk.Domain.Users;

namespace SupportDesk.Infrastructure.Persistence;

public sealed class UserRepository(SupportDeskDbContext db) : IUserRepository
{
    // SQL Server error numbers for "duplicate key" violations (unique index / unique constraint).
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: UniqueIndexViolation or UniqueConstraintViolation })
        {
            // Two requests tried to create the same email at the same moment; the unique index
            // let only one through. Report it like any other duplicate.
            throw new ConflictException("An account with this email already exists.");
        }
    }
}