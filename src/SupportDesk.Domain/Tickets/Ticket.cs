namespace SupportDesk.Domain.Tickets;

/// <summary>
/// A support ticket. All state changes go through methods that enforce the workflow:
/// Open -> InProgress -> Resolved -> Closed, with Reopen from Resolved or Closed.
/// </summary>
public sealed class Ticket
{
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 4000;

    // Needed by the persistence layer (EF Core) later on.
    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public TicketStatus Status { get; private set; }

    public TicketPriority Priority { get; private set; }

    public Guid? AssigneeId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public static Ticket Create(
        string? title,
        string? description,
        TicketPriority priority,
        DateTimeOffset now)
    {
        EnsureValidPriority(priority);

        return new Ticket
        {
            Id = Guid.CreateVersion7(),
            Title = NormalizeTitle(title),
            Description = NormalizeDescription(description),
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void StartProgress(Guid assigneeId, DateTimeOffset now)
    {
        if (assigneeId == Guid.Empty)
        {
            throw new DomainException("An assignee is required to start working on a ticket.");
        }

        EnsureStatus(TicketStatus.Open, "start progress on");

        AssigneeId = assigneeId;
        Status = TicketStatus.InProgress;
        UpdatedAt = now;
    }

    public void Resolve(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.InProgress, "resolve");

        Status = TicketStatus.Resolved;
        ResolvedAt = now;
        UpdatedAt = now;
    }

    public void Close(DateTimeOffset now)
    {
        EnsureStatus(TicketStatus.Resolved, "close");

        Status = TicketStatus.Closed;
        UpdatedAt = now;
    }

    public void Reopen(DateTimeOffset now)
    {
        if (Status is not (TicketStatus.Resolved or TicketStatus.Closed))
        {
            throw new DomainException($"Cannot reopen a ticket that is {Status}.");
        }

        Status = TicketStatus.Open;
        AssigneeId = null;
        ResolvedAt = null;
        UpdatedAt = now;
    }

    public void ChangePriority(TicketPriority priority, DateTimeOffset now)
    {
        if (Status == TicketStatus.Closed)
        {
            throw new DomainException("Cannot change the priority of a closed ticket.");
        }

        EnsureValidPriority(priority);

        Priority = priority;
        UpdatedAt = now;
    }

    private void EnsureStatus(TicketStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException($"Cannot {action} a ticket that is {Status}.");
        }
    }

    private static void EnsureValidPriority(TicketPriority priority)
    {
        if (!Enum.IsDefined(priority))
        {
            throw new DomainException("The priority is not valid.");
        }
    }

    private static string NormalizeTitle(string? title)
    {
        var value = title?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("The title is required.");
        }

        if (value.Length > TitleMaxLength)
        {
            throw new DomainException($"The title cannot exceed {TitleMaxLength} characters.");
        }

        return value;
    }

    private static string NormalizeDescription(string? description)
    {
        var value = description?.Trim();

        if (string.IsNullOrEmpty(value))
        {
            throw new DomainException("The description is required.");
        }

        if (value.Length > DescriptionMaxLength)
        {
            throw new DomainException($"The description cannot exceed {DescriptionMaxLength} characters.");
        }

        return value;
    }
}