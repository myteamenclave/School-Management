using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Domain.Tests;

public class SubjectTermGradeTests
{
    [Fact]
    public void SetScores_ComputesWeightedTermScore_WhenAllComponentsPresent()
    {
        var grade = new SubjectTermGrade();

        // 85*0.30 + 90*0.40 + 80*0.30 = 25.5 + 36 + 24 = 85.5
        grade.SetScores(midterm: 85m, final: 90m, coursework: 80m);

        Assert.Equal(85.5m, grade.TermScore);
    }

    [Fact]
    public void SetScores_RoundsTermScoreToTwoDecimals()
    {
        var grade = new SubjectTermGrade();

        // 83.333*0.30 + 77.777*0.40 + 91.111*0.30 = 24.9999 + 31.1108 + 27.3333 = 83.444
        grade.SetScores(midterm: 83.333m, final: 77.777m, coursework: 91.111m);

        Assert.Equal(83.44m, grade.TermScore);
    }

    [Theory]
    [InlineData(null, 90, 80)]
    [InlineData(85, null, 80)]
    [InlineData(85, 90, null)]
    [InlineData(null, null, null)]
    public void SetScores_LeavesTermScoreNull_WhenAnyComponentMissing(
        int? midterm, int? final, int? coursework)
    {
        var grade = new SubjectTermGrade();

        grade.SetScores(
            midterm is null ? null : (decimal)midterm.Value,
            final is null ? null : (decimal)final.Value,
            coursework is null ? null : (decimal)coursework.Value);

        Assert.Null(grade.TermScore);
    }

    [Fact]
    public void SetScores_ResetsLetterGrade_SoServiceMustReapply()
    {
        var grade = new SubjectTermGrade();
        grade.ApplyLetter("A");

        grade.SetScores(midterm: 85m, final: 90m, coursework: 80m);

        Assert.Null(grade.LetterGrade);
    }

    [Fact]
    public void ApplyLetter_SetsLetterGrade()
    {
        var grade = new SubjectTermGrade();

        grade.ApplyLetter("B");

        Assert.Equal("B", grade.LetterGrade);
    }
}
