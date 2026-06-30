using Microsoft.AspNetCore.Identity;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Infrastructure.Auth;

// Wraps ASP.NET Core Identity's standalone PasswordHasher<TUser> — not full
// Identity (no UserManager/SignInManager/IdentityDbContext). See
// specs/02-implement-auth.md Tech Stack.
internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password) =>
        _hasher.HashPassword(default!, password);

    public bool VerifyPassword(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(default!, passwordHash, providedPassword) != PasswordVerificationResult.Failed;
}
