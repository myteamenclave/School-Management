namespace SchoolMgmt.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Called only from AppDbContext.SaveChangesAsync.
    internal void SetCreated(DateTimeOffset now) => CreatedAt = now;
    internal void SetUpdated(DateTimeOffset now) => UpdatedAt = now;
}
