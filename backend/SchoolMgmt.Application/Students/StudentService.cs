using Microsoft.Extensions.Options;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.Students;

public class StudentService(IStudentRepository repository, IUnitOfWork unitOfWork, IOptions<StudentOptions> options)
{
    private readonly int _maxRetries = options.Value.StudentCodeMaxRetries;

    public async Task<StudentDto> CreateStudentAsync(CreateStudentRequest request, CancellationToken ct = default)
    {
        var student = new Student
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            DateOfBirth = request.DateOfBirth,
            Gender = Enum.Parse<Gender>(request.Gender, ignoreCase: true),
            EnrollmentDate = request.EnrollmentDate,
            EnrollmentStatus = EnrollmentStatus.Active,
            GuardianName = request.GuardianName,
            GuardianPhone = request.GuardianPhone,
            GuardianEmail = request.GuardianEmail,
        };

        for (var attempt = 0; attempt < _maxRetries; attempt++)
        {
            student.StudentCode = await repository.GetNextStudentCodeAsync(request.EnrollmentDate.Year, ct);
            await repository.AddAsync(student, ct);

            try
            {
                await unitOfWork.SaveChangesAsync(ct);
                return ToDto(student);
            }
            catch (ConflictException) when (attempt < _maxRetries - 1)
            {
                unitOfWork.Detach(student);
            }
        }

        throw new DomainException("Unable to assign a student code. Please try again.");
    }

    public async Task<PagedResult<StudentSummaryDto>> GetStudentsAsync(
        EnrollmentStatus? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(status, search, page, pageSize, ct);
        return new PagedResult<StudentSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
    }

    public async Task<StudentDto> GetStudentByIdAsync(Guid id, CancellationToken ct = default)
    {
        var student = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Student not found.");
        return ToDto(student);
    }

    public async Task<StudentDto> UpdateStudentAsync(Guid id, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var student = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Student not found.");

        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = Enum.Parse<Gender>(request.Gender, ignoreCase: true);
        student.EnrollmentDate = request.EnrollmentDate;
        student.EnrollmentStatus = Enum.Parse<EnrollmentStatus>(request.EnrollmentStatus, ignoreCase: true);
        student.GuardianName = request.GuardianName;
        student.GuardianPhone = request.GuardianPhone;
        student.GuardianEmail = request.GuardianEmail;

        repository.Update(student);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(student);
    }

    private static StudentSummaryDto ToSummaryDto(Student s) => new(
        s.Id,
        s.StudentCode,
        s.FirstName,
        s.LastName,
        s.DateOfBirth,
        s.Gender.ToString(),
        s.EnrollmentDate,
        s.EnrollmentStatus.ToString()
    );

    private static StudentDto ToDto(Student s) => new(
        s.Id,
        s.StudentCode,
        s.FirstName,
        s.LastName,
        s.DateOfBirth,
        s.Gender.ToString(),
        s.EnrollmentDate,
        s.EnrollmentStatus.ToString(),
        s.GuardianName,
        s.GuardianPhone,
        s.GuardianEmail,
        s.CreatedAt,
        s.UpdatedAt
    );
}
