using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.TeacherAssignments.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.TeacherAssignments;

public class TeacherAssignmentService(
    ITeacherSectionSubjectRepository assignmentRepository,
    ITeacherRepository teacherRepository,
    ISubjectRepository subjectRepository,
    IGradeRepository gradeRepository,
    IAcademicYearRepository academicYearRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<List<TeacherAssignmentDto>> GetByTeacherAndYearAsync(
        Guid teacherId, Guid academicYearId, CancellationToken ct = default)
    {
        _ = await teacherRepository.GetByIdAsync(teacherId, ct)
            ?? throw new NotFoundException("Teacher not found.");
        var assignments = await assignmentRepository.GetByTeacherAndYearAsync(teacherId, academicYearId, ct);
        return assignments.Select(ToDto).ToList();
    }

    public async Task<TeacherAssignmentDto> CreateAsync(
        Guid teacherId, CreateTeacherAssignmentRequest request, CancellationToken ct = default)
    {
        _ = await teacherRepository.GetByIdAsync(teacherId, ct)
            ?? throw new NotFoundException("Teacher not found.");
        _ = await subjectRepository.GetByIdAsync(request.SubjectId, ct)
            ?? throw new NotFoundException("Subject not found.");
        _ = await gradeRepository.GetSectionByIdAsync(request.SectionId, ct)
            ?? throw new NotFoundException("Section not found.");
        _ = await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");

        var existing = await assignmentRepository.GetBySubjectSectionAndYearAsync(
            request.SubjectId, request.SectionId, request.AcademicYearId, ct);
        if (existing != null)
            throw new ConflictException(
                "A teacher is already assigned to this subject in this section for this academic year.");

        var assignment = new TeacherSectionSubject
        {
            TeacherId = teacherId,
            SubjectId = request.SubjectId,
            SectionId = request.SectionId,
            AcademicYearId = request.AcademicYearId,
        };
        await assignmentRepository.AddAsync(assignment, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var loaded = await assignmentRepository.GetByTeacherAndYearAsync(teacherId, request.AcademicYearId, ct);
        return ToDto(loaded.Single(a => a.Id == assignment.Id));
    }

    public async Task DeleteAsync(Guid teacherId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await assignmentRepository.GetByIdAsync(assignmentId, ct)
            ?? throw new NotFoundException("Teacher assignment not found.");
        if (assignment.TeacherId != teacherId)
            throw new NotFoundException("Teacher assignment not found.");
        assignmentRepository.Remove(assignment);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static TeacherAssignmentDto ToDto(TeacherSectionSubject a) => new(
        a.Id,
        a.TeacherId,
        a.SubjectId,
        a.Subject.Name,
        a.Subject.Code,
        a.SectionId,
        a.Section.Name,
        a.Section.GradeId,
        a.Section.Grade.Name,
        a.AcademicYearId,
        a.AcademicYear.Name,
        a.CreatedAt
    );
}
