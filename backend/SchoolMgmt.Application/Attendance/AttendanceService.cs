using SchoolMgmt.Application.Attendance.Dtos;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.Attendance;

public class AttendanceService(
    IAttendanceRepository attendanceRepo,
    IStudentSectionEnrollmentRepository enrollmentRepo,
    ITeacherSectionSubjectRepository assignmentRepo,
    ITeacherRepository teacherRepo,
    IGradeRepository gradeRepo,
    IUnitOfWork unitOfWork)
{
    public async Task<SectionAttendanceRosterDto> GetSectionRosterAsync(
        Guid sectionId, DateOnly date, Guid academicYearId, CancellationToken ct = default)
    {
        var section = await gradeRepo.GetSectionByIdAsync(sectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        var enrollments = await enrollmentRepo.GetBySectionAndYearAsync(sectionId, academicYearId, ct);
        var existing = await attendanceRepo.GetBySectionAndDateAsync(sectionId, date, ct);
        var existingByStudent = existing.ToDictionary(r => r.StudentId);

        var entries = enrollments.Select(e =>
        {
            existingByStudent.TryGetValue(e.StudentId, out var rec);
            return new AttendanceRosterEntryDto(
                e.StudentId,
                $"{e.Student.FirstName} {e.Student.LastName}",
                e.Student.StudentCode,
                rec?.Status.ToString(),
                rec?.Notes
            );
        }).ToList();

        return new SectionAttendanceRosterDto(sectionId, section.Name, date, entries);
    }

    public async Task<BulkUpsertAttendanceResult> BulkUpsertAsync(
        BulkUpsertAttendanceRequest request, Guid markedByUserId, CancellationToken ct = default)
    {
        _ = await gradeRepo.GetSectionByIdAsync(request.SectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        var teacher = await teacherRepo.GetByUserIdAsync(markedByUserId, ct)
            ?? throw new NotFoundException("Teacher profile not found.");

        var assignments = await assignmentRepo.GetByTeacherAndYearAsync(teacher.Id, request.AcademicYearId, ct);
        if (!assignments.Any(a => a.SectionId == request.SectionId))
            throw new DomainException("You are not assigned to this section for the selected year.");

        var upserted = 0;
        foreach (var entry in request.Entries)
        {
            if (!Enum.TryParse<AttendanceStatus>(entry.Status, out var status))
                continue;

            var existing = await attendanceRepo.GetByStudentSectionAndDateAsync(
                entry.StudentId, request.SectionId, request.Date, ct);

            if (existing is not null)
            {
                existing.Status = status;
                existing.Notes = entry.Notes;
                existing.MarkedByUserId = markedByUserId;
                attendanceRepo.Update(existing);
            }
            else
            {
                var record = new AttendanceRecord
                {
                    StudentId = entry.StudentId,
                    SectionId = request.SectionId,
                    AcademicYearId = request.AcademicYearId,
                    Date = request.Date,
                    Status = status,
                    Notes = entry.Notes,
                    MarkedByUserId = markedByUserId,
                };
                await attendanceRepo.AddAsync(record, ct);
            }
            upserted++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BulkUpsertAttendanceResult(upserted);
    }

    public async Task<List<AttendanceHistoryEntryDto>> GetStudentHistoryAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var records = await attendanceRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        return records.Select(r => new AttendanceHistoryEntryDto(
            r.Id,
            r.SectionId,
            r.Section.Name,
            r.Date,
            r.Status.ToString(),
            r.Notes
        )).ToList();
    }
}
