using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Domain.Tests;

public class AcademicYearTests
{
    [Fact]
    public void Create_ProducesExactlyTwoSemesters()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));

        Assert.Equal(2, year.Semesters.Count);
    }

    [Fact]
    public void Create_NamesSemestersCorrectly()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));

        Assert.Equal("Semester 1", year.Semesters[0].Name);
        Assert.Equal("Semester 2", year.Semesters[1].Name);
    }

    [Fact]
    public void EnsureNotArchived_DoesNotThrow_WhenActive()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));

        var ex = Record.Exception(() => year.EnsureNotArchived());

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureNotArchived_ThrowsDomainException_WhenArchived()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));
        year.Archive();

        Assert.Throws<DomainException>(() => year.EnsureNotArchived());
    }

    [Fact]
    public void Archive_SetsStatusToArchived_WhenNotCurrent()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));

        year.Archive();

        Assert.Equal(AcademicYearStatus.Archived, year.Status);
    }

    [Fact]
    public void Archive_ThrowsDomainException_WhenIsCurrent()
    {
        var year = AcademicYear.Create("2024-2025", new DateOnly(2024, 9, 1), new DateOnly(2025, 6, 30));
        year.SetCurrent(true);

        Assert.Throws<DomainException>(() => year.Archive());
    }
}
