using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Gradebook;

namespace SchoolMgmt.Domain.Entities;

public class SubjectTermGrade : BaseEntity, ITenantScoped
{
    public Guid SchoolId { get; set; }

    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;

    public Guid SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;

    // Provenance only — which section context the grade was entered under.
    // NOT part of the unique key. Section transfers do not fragment the grade.
    public Guid SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public Guid AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;

    public Guid SemesterId { get; set; }
    public Semester Semester { get; set; } = null!;

    public decimal? MidtermScore { get; private set; }
    public decimal? FinalScore { get; private set; }
    public decimal? CourseworkScore { get; private set; }

    // Auto-computed. Null until ALL three components are present (idea doc 12 — null-until-complete).
    public decimal? TermScore { get; private set; }
    public string? LetterGrade { get; private set; }

    public string? Notes { get; set; }

    public Guid EnteredByUserId { get; set; }
    public User EnteredByUser { get; set; } = null!;

    // Sets components and recomputes TermScore. LetterGrade is applied separately
    // by the service (needs the school's GradeScale bands). See ApplyLetter.
    public void SetScores(decimal? midterm, decimal? final, decimal? coursework)
    {
        MidtermScore = midterm;
        FinalScore = final;
        CourseworkScore = coursework;

        TermScore = (midterm.HasValue && final.HasValue && coursework.HasValue)
            ? Math.Round(
                midterm.Value * GradeWeights.Midterm
                + final.Value * GradeWeights.Final
                + coursework.Value * GradeWeights.Coursework, 2)
            : null;
        LetterGrade = null; // reset — service re-applies from TermScore
    }

    public void ApplyLetter(string? letter) => LetterGrade = letter;
}
