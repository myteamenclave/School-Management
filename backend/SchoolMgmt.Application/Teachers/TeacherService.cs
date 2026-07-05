using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Application.Teachers.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Teachers;

public class TeacherService(
    ITeacherRepository repository,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IOptions<TeacherOptions> options)
{
    private readonly int _maxRetries = options.Value.TeacherCodeMaxRetries;

    public async Task<TeacherDto> CreateTeacherAsync(CreateTeacherRequest request, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var user = new User
            {
                Email = request.Email,
                PasswordHash = passwordHasher.HashPassword(request.Password),
                DisplayName = $"{request.FirstName} {request.LastName}",
                Role = UserRole.Teacher,
                IsActive = true,
            };
            await userRepository.AddAsync(user, ct);
            await unitOfWork.SaveChangesAsync(ct); // flush to get user.Id for the FK

            var teacher = new Teacher
            {
                UserId = user.Id,
                TeacherCode = string.Empty,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Phone = request.Phone,
                JoiningDate = request.JoiningDate,
                IsActive = true,
            };

            for (var attempt = 0; attempt < _maxRetries; attempt++)
            {
                teacher.TeacherCode = await repository.GetNextTeacherCodeAsync(request.JoiningDate.Year, ct);
                await repository.AddAsync(teacher, ct);

                try
                {
                    await unitOfWork.SaveChangesAsync(ct);
                    break;
                }
                catch (ConflictException) when (attempt < _maxRetries - 1)
                {
                    unitOfWork.Detach(teacher);
                }
            }

            if (string.IsNullOrEmpty(teacher.TeacherCode))
                throw new DomainException("Unable to assign a teacher code. Please try again.");

            await unitOfWork.CommitAsync(ct);
            teacher.User = user;
            return ToDto(teacher);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<PagedResult<TeacherSummaryDto>> GetTeachersAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(isActive, search, page, pageSize, ct);
        return new PagedResult<TeacherSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
    }

    public async Task<TeacherDto> GetTeacherByIdAsync(Guid id, CancellationToken ct = default)
    {
        var teacher = await repository.GetByIdWithUserAsync(id, ct)
            ?? throw new NotFoundException("Teacher not found.");
        return ToDto(teacher);
    }

    public async Task<TeacherDto> UpdateTeacherAsync(Guid id, UpdateTeacherRequest request, CancellationToken ct = default)
    {
        var teacher = await repository.GetByIdWithUserAsync(id, ct)
            ?? throw new NotFoundException("Teacher not found.");

        teacher.FirstName = request.FirstName;
        teacher.LastName = request.LastName;
        teacher.Phone = request.Phone;
        teacher.JoiningDate = request.JoiningDate;

        if (teacher.IsActive != request.IsActive)
        {
            teacher.IsActive = request.IsActive;
            teacher.User.IsActive = request.IsActive;
            userRepository.Update(teacher.User);
        }

        repository.Update(teacher);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(teacher);
    }

    private static TeacherSummaryDto ToSummaryDto(Teacher t) => new(
        t.Id,
        t.TeacherCode,
        t.FirstName,
        t.LastName,
        t.Phone,
        t.JoiningDate,
        t.IsActive,
        t.User.Email
    );

    private static TeacherDto ToDto(Teacher t) => new(
        t.Id,
        t.TeacherCode,
        t.FirstName,
        t.LastName,
        t.Phone,
        t.JoiningDate,
        t.IsActive,
        t.User.Email,
        t.UserId,
        t.CreatedAt,
        t.UpdatedAt
    );
}
