using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.ParentAccounts.Dtos;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.ParentAccounts;

public class ParentAccountService(
    IStudentRepository students,
    IUserRepository users,
    IStudentParentRepository links,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork)
{
    public async Task<ParentLoginResultDto> CreateParentLoginAsync(
        Guid studentId, CreateParentLoginRequest request, CancellationToken ct = default)
    {
        var student = await students.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException("Student not found.");

        var email = student.GuardianEmail?.Trim();
        if (string.IsNullOrEmpty(email))
            throw new DomainException("Student has no guardian email. Add one before creating a parent login.");

        var existing = await users.FindByEmailInTenantAsync(email, ct);
        var accountCreated = false;
        User parent;
        if (existing is not null)
        {
            if (existing.Role != UserRole.Parent)
                throw new ConflictException(
                    $"The email '{email}' already belongs to a non-parent account and cannot be used for a parent login.");

            // Reuse the existing parent account — leave its password untouched.
            parent = existing;
        }
        else
        {
            parent = new User
            {
                Email = email,
                PasswordHash = passwordHasher.HashPassword(request.TemporaryPassword),
                DisplayName = string.IsNullOrWhiteSpace(student.GuardianName) ? email : student.GuardianName!,
                Role = UserRole.Parent,
                IsActive = true,
            };
            await users.AddAsync(parent, ct);
            accountCreated = true;
        }

        var linkCreated = false;
        var existingLink = await links.GetLinkAsync(studentId, parent.Id, ct);
        if (existingLink is null)
        {
            await links.AddAsync(new StudentParent { StudentId = studentId, UserId = parent.Id }, ct);
            linkCreated = true;
        }

        if (accountCreated || linkCreated)
            await unitOfWork.SaveChangesAsync(ct);

        return new ParentLoginResultDto(parent.Id, parent.Email, parent.DisplayName, accountCreated, linkCreated);
    }

    public async Task<List<ParentAccountDto>> GetParentsForStudentAsync(Guid studentId, CancellationToken ct = default)
    {
        _ = await students.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException("Student not found.");

        var list = await links.GetByStudentIdAsync(studentId, ct);
        return list
            .Select(l => new ParentAccountDto(
                l.ParentUser.Id, l.ParentUser.Email, l.ParentUser.DisplayName, l.ParentUser.CreatedAt))
            .ToList();
    }

    public async Task RemoveParentLinkAsync(Guid studentId, Guid parentUserId, CancellationToken ct = default)
    {
        var link = await links.GetLinkAsync(studentId, parentUserId, ct)
            ?? throw new NotFoundException("Parent link not found for this student.");

        links.Remove(link);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
