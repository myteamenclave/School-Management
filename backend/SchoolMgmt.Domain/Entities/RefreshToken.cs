using SchoolMgmt.Domain.Common;

namespace SchoolMgmt.Domain.Entities;

public class RefreshToken : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!; // eager-loaded by GetByTokenHashAsync — avoids a second tenant-bypassing lookup in AuthService
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of the raw token — never store the raw value
    public Guid SessionId { get; set; } // groups every token issued from one login, for family revocation
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? ReplacedByTokenId { get; set; }
}
