using SchoolMgmt.Application.ParentAccounts;
using SchoolMgmt.Application.ParentAccounts.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Infrastructure.Tests.Auth.Fakes;
using SchoolMgmt.Infrastructure.Tests.ParentAccounts.Fakes;

namespace SchoolMgmt.Infrastructure.Tests.ParentAccounts;

public class ParentAccountServiceTests
{
    private static readonly Guid SchoolId = Guid.NewGuid();

    private static (ParentAccountService Service, FakeStudentRepository Students, FakeUserRepository Users, FakeStudentParentRepository Links)
        CreateService()
    {
        var students = new FakeStudentRepository();
        var users = new FakeUserRepository();
        var links = new FakeStudentParentRepository();
        var service = new ParentAccountService(students, users, links, new FakePasswordHasher(), new FakeUnitOfWork());
        return (service, students, users, links);
    }

    private static Student SeedStudent(FakeStudentRepository students, string? guardianEmail, string? guardianName = "Jane Doe")
    {
        var student = new Student
        {
            SchoolId = SchoolId,
            StudentCode = "2025-000001",
            FirstName = "Kid",
            LastName = "Doe",
            GuardianEmail = guardianEmail,
            GuardianName = guardianName,
        };
        students.Seed(student);
        return student;
    }

    [Fact]
    public async Task CreateParentLogin_NewAccount_CreatesParentUserAndLink()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");

        var result = await service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Passw0rd!"));

        Assert.True(result.AccountCreated);
        Assert.True(result.LinkCreated);
        Assert.Equal("parent@demoschool.test", result.Email);

        var created = await users.GetByEmailAsync("parent@demoschool.test");
        Assert.NotNull(created);
        Assert.Equal(UserRole.Parent, created!.Role);
        Assert.True(created.IsActive);
        Assert.Equal("Jane Doe", created.DisplayName);
        Assert.Equal("hash:Passw0rd!", created.PasswordHash);
        Assert.Single(links.Links);
    }

    [Fact]
    public async Task CreateParentLogin_BlankGuardianName_FallsBackToEmailAsDisplayName()
    {
        var (service, students, users, _) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test", guardianName: "  ");

        await service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Passw0rd!"));

        var created = await users.GetByEmailAsync("parent@demoschool.test");
        Assert.Equal("parent@demoschool.test", created!.DisplayName);
    }

    [Fact]
    public async Task CreateParentLogin_ExistingParent_ReusesAccountAndLeavesPasswordUntouched()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");
        var existing = new User
        {
            SchoolId = SchoolId,
            Email = "parent@demoschool.test",
            PasswordHash = "hash:original",
            DisplayName = "Existing Parent",
            Role = UserRole.Parent,
        };
        users.Seed(existing);

        var result = await service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Different1!"));

        Assert.False(result.AccountCreated);
        Assert.True(result.LinkCreated);
        Assert.Equal(existing.Id, result.ParentUserId);
        Assert.Equal("hash:original", existing.PasswordHash); // untouched
        Assert.Single(links.Links);
    }

    [Fact]
    public async Task CreateParentLogin_ExistingLink_IsIdempotentNoOp()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");
        var existing = new User
        {
            SchoolId = SchoolId,
            Email = "parent@demoschool.test",
            PasswordHash = "hash:original",
            DisplayName = "Existing Parent",
            Role = UserRole.Parent,
        };
        users.Seed(existing);
        links.Seed(new StudentParent { SchoolId = SchoolId, StudentId = student.Id, UserId = existing.Id });

        var result = await service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Whatever1!"));

        Assert.False(result.AccountCreated);
        Assert.False(result.LinkCreated);
        Assert.Single(links.Links);
    }

    [Fact]
    public async Task CreateParentLogin_EmailOwnedByNonParent_ThrowsConflict()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "teacher@demoschool.test");
        users.Seed(new User
        {
            SchoolId = SchoolId,
            Email = "teacher@demoschool.test",
            PasswordHash = "hash:x",
            DisplayName = "A Teacher",
            Role = UserRole.Teacher,
        });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Passw0rd!")));

        Assert.Empty(links.Links);
    }

    [Fact]
    public async Task CreateParentLogin_BlankGuardianEmail_ThrowsDomainException()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, guardianEmail: null);

        await Assert.ThrowsAsync<DomainException>(() =>
            service.CreateParentLoginAsync(student.Id, new CreateParentLoginRequest("Passw0rd!")));

        Assert.Empty(links.Links);
    }

    [Fact]
    public async Task CreateParentLogin_UnknownStudent_ThrowsNotFound()
    {
        var (service, _, _, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.CreateParentLoginAsync(Guid.NewGuid(), new CreateParentLoginRequest("Passw0rd!")));
    }

    [Fact]
    public async Task RemoveParentLink_ExistingLink_RemovesLinkButNotUser()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");
        var parent = new User
        {
            SchoolId = SchoolId,
            Email = "parent@demoschool.test",
            PasswordHash = "hash:x",
            DisplayName = "Parent",
            Role = UserRole.Parent,
        };
        users.Seed(parent);
        links.Seed(new StudentParent { SchoolId = SchoolId, StudentId = student.Id, UserId = parent.Id });

        await service.RemoveParentLinkAsync(student.Id, parent.Id);

        Assert.Empty(links.Links);
        Assert.NotNull(await users.GetByIdAsync(parent.Id)); // user survives
    }

    [Fact]
    public async Task RemoveParentLink_UnknownLink_ThrowsNotFound()
    {
        var (service, students, _, _) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.RemoveParentLinkAsync(student.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetParentsForStudent_UnknownStudent_ThrowsNotFound()
    {
        var (service, _, _, _) = CreateService();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.GetParentsForStudentAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetParentsForStudent_ReturnsLinkedParents()
    {
        var (service, students, users, links) = CreateService();
        var student = SeedStudent(students, "parent@demoschool.test");
        var parent = new User
        {
            SchoolId = SchoolId,
            Email = "parent@demoschool.test",
            PasswordHash = "hash:x",
            DisplayName = "Parent",
            Role = UserRole.Parent,
        };
        users.Seed(parent);
        links.Seed(new StudentParent { SchoolId = SchoolId, StudentId = student.Id, UserId = parent.Id, ParentUser = parent });

        var result = await service.GetParentsForStudentAsync(student.Id);

        Assert.Single(result);
        Assert.Equal(parent.Id, result[0].ParentUserId);
        Assert.Equal("parent@demoschool.test", result[0].Email);
    }
}
