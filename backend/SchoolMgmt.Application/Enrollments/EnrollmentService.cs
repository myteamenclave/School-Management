using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Enrollments.Dtos;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Enrollments;

public class EnrollmentService(
    IStudentSectionEnrollmentRepository enrollmentRepository,
    IStudentRepository studentRepository,
    IGradeRepository gradeRepository,
    IAcademicYearRepository academicYearRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<List<EnrollmentDto>> GetBySectionAndYearAsync(
        Guid sectionId, Guid academicYearId, CancellationToken ct = default)
    {
        _ = await gradeRepository.GetSectionByIdAsync(sectionId, ct)
            ?? throw new NotFoundException("Section not found.");
        var enrollments = await enrollmentRepository.GetBySectionAndYearAsync(sectionId, academicYearId, ct);
        return enrollments.Select(ToDto).ToList();
    }

    public async Task<EnrollmentDto> CreateAsync(
        Guid sectionId, CreateEnrollmentRequest request, CancellationToken ct = default)
    {
        _ = await gradeRepository.GetSectionByIdAsync(sectionId, ct)
            ?? throw new NotFoundException("Section not found.");
        _ = await studentRepository.GetByIdAsync(request.StudentId, ct)
            ?? throw new NotFoundException("Student not found.");
        _ = await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");

        var existing = await enrollmentRepository.GetByStudentAndYearAsync(request.StudentId, request.AcademicYearId, ct);
        if (existing != null)
            throw new ConflictException("Student is already enrolled for this academic year.");

        var enrollment = new StudentSectionEnrollment
        {
            StudentId = request.StudentId,
            SectionId = sectionId,
            AcademicYearId = request.AcademicYearId,
        };
        await enrollmentRepository.AddAsync(enrollment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await enrollmentRepository.GetByIdWithDetailsAsync(enrollment.Id, ct);
        return ToDto(loaded!);
    }

    public async Task<EnrollmentDto> TransferAsync(
        Guid enrollmentId, TransferEnrollmentRequest request, CancellationToken ct = default)
    {
        var enrollment = await enrollmentRepository.GetByIdWithDetailsAsync(enrollmentId, ct)
            ?? throw new NotFoundException("Enrollment not found.");
        _ = await gradeRepository.GetSectionByIdAsync(request.SectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        enrollment.SectionId = request.SectionId;
        enrollmentRepository.Update(enrollment);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await enrollmentRepository.GetByIdWithDetailsAsync(enrollment.Id, ct);
        return ToDto(loaded!);
    }

    public async Task DeleteAsync(Guid enrollmentId, CancellationToken ct = default)
    {
        var enrollment = await enrollmentRepository.GetByIdAsync(enrollmentId, ct)
            ?? throw new NotFoundException("Enrollment not found.");
        enrollmentRepository.Remove(enrollment);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static EnrollmentDto ToDto(StudentSectionEnrollment e) => new(
        e.Id,
        e.StudentId,
        e.Student.StudentCode,
        e.Student.FirstName,
        e.Student.LastName,
        e.SectionId,
        e.Section.Name,
        e.Section.GradeId,
        e.Section.Grade.Name,
        e.AcademicYearId,
        e.AcademicYear.Name,
        e.CreatedAt,
        e.UpdatedAt
    );
}
