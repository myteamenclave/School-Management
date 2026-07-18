using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Gradebook.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Subjects;
using SchoolMgmt.Application.Teachers;
using SchoolMgmt.Application.TeacherAssignments;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Gradebook;

public class GradebookService(
    ISubjectTermGradeRepository gradeRepo,
    IGradeScaleBandRepository bandRepo,
    IStudentSectionEnrollmentRepository enrollmentRepo,
    ITeacherSectionSubjectRepository assignmentRepo,
    ITeacherRepository teacherRepo,
    IAcademicYearRepository yearRepo,
    IGradeRepository gradeLevelRepo,
    ISubjectRepository subjectRepo,
    IUnitOfWork unitOfWork)
{
    // GET roster — enrolled students for section+year with their grade (by student+subject+semester).
    // Teacher and Admin. Students come from CURRENT enrollment; grades join by student identity,
    // so a transferred student's grade (entered under a different section) still shows here.
    public async Task<SubjectGradeRosterDto> GetSubjectRosterAsync(
        Guid sectionId, Guid subjectId, Guid semesterId, CancellationToken ct = default)
    {
        var section = await gradeLevelRepo.GetSectionByIdAsync(sectionId, ct)
            ?? throw new NotFoundException("Section not found.");
        var subject = await subjectRepo.GetByIdAsync(subjectId, ct)
            ?? throw new NotFoundException("Subject not found.");
        var semester = await yearRepo.GetSemesterByIdAsync(semesterId, ct)
            ?? throw new NotFoundException("Semester not found.");

        var enrollments = await enrollmentRepo.GetBySectionAndYearAsync(sectionId, semester.AcademicYearId, ct);
        var grades = await gradeRepo.GetBySubjectAndSemesterAsync(subjectId, semesterId, ct);
        var gradeByStudent = grades.ToDictionary(g => g.StudentId);

        var entries = enrollments.Select(e =>
        {
            gradeByStudent.TryGetValue(e.StudentId, out var g);
            return new GradeRosterEntryDto(
                e.StudentId,
                $"{e.Student.FirstName} {e.Student.LastName}",
                e.Student.StudentCode,
                g?.MidtermScore, g?.FinalScore, g?.CourseworkScore,
                g?.TermScore, g?.LetterGrade, g?.Notes);
        }).ToList();

        return new SubjectGradeRosterDto(
            sectionId, section.Name, subjectId, subject.Name, semesterId, semester.Name, entries);
    }

    // PUT bulk — Teacher only. Validates the caller owns the (subject, section, year) slot
    // and the year is not archived.
    public async Task<BulkUpsertGradesResult> BulkUpsertAsync(
        BulkUpsertGradesRequest request, Guid enteredByUserId, CancellationToken ct = default)
    {
        var semester = await yearRepo.GetSemesterByIdAsync(request.SemesterId, ct)
            ?? throw new NotFoundException("Semester not found.");
        var year = await yearRepo.GetByIdAsync(semester.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");
        year.EnsureNotArchived();

        var teacher = await teacherRepo.GetByUserIdAsync(enteredByUserId, ct)
            ?? throw new NotFoundException("Teacher profile not found.");
        var assignment = await assignmentRepo.GetBySubjectSectionAndYearAsync(
            request.SubjectId, request.SectionId, semester.AcademicYearId, ct);
        if (assignment is null || assignment.TeacherId != teacher.Id)
            throw new DomainException("You are not assigned to teach this subject in this section for the selected year.");

        var bands = await bandRepo.GetAllOrderedAsync(ct);

        var upserted = 0;
        foreach (var entry in request.Entries)
        {
            var existing = await gradeRepo.GetByStudentSubjectSemesterAsync(
                entry.StudentId, request.SubjectId, request.SemesterId, ct);

            if (existing is not null)
            {
                existing.SetScores(entry.Midterm, entry.Final, entry.Coursework);
                existing.ApplyLetter(LetterResolver.Resolve(bands, existing.TermScore));
                existing.Notes = entry.Notes;
                existing.SectionId = request.SectionId;       // refresh provenance to current section
                existing.EnteredByUserId = enteredByUserId;
                gradeRepo.Update(existing);
            }
            else
            {
                var grade = new SubjectTermGrade
                {
                    StudentId = entry.StudentId,
                    SubjectId = request.SubjectId,
                    SectionId = request.SectionId,
                    AcademicYearId = semester.AcademicYearId,
                    SemesterId = request.SemesterId,
                    Notes = entry.Notes,
                    EnteredByUserId = enteredByUserId,
                };
                grade.SetScores(entry.Midterm, entry.Final, entry.Coursework);
                grade.ApplyLetter(LetterResolver.Resolve(bands, grade.TermScore));
                await gradeRepo.AddAsync(grade, ct);
            }
            upserted++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BulkUpsertGradesResult(upserted);
    }

    // GET student grades for a year — Teacher and Admin.
    public async Task<List<StudentGradeDto>> GetStudentGradesAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var grades = await gradeRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        return grades.Select(g => new StudentGradeDto(
            g.Id, g.SubjectId, g.Subject.Name, g.SemesterId, g.Semester.Name,
            g.MidtermScore, g.FinalScore, g.CourseworkScore, g.TermScore, g.LetterGrade, g.Notes)).ToList();
    }
}
