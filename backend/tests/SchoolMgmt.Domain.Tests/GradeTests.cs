using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Domain.Tests;

public class GradeTests
{
    [Fact]
    public void EnsureNoSections_DoesNotThrow_WhenGradeHasNoSections()
    {
        var grade = new Grade { Name = "Grade 5", DisplayOrder = 5 };

        var ex = Record.Exception(() => grade.EnsureNoSections());

        Assert.Null(ex);
    }

    [Fact]
    public void EnsureNoSections_ThrowsDomainException_WhenGradeHasSections()
    {
        var grade = new Grade { Name = "Grade 5", DisplayOrder = 5 };
        // Directly test via reflection to inject a section into the backing field
        var sectionsField = typeof(Grade)
            .GetField("_sections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var sections = (List<Section>)sectionsField.GetValue(grade)!;
        sections.Add(new Section { Name = "A", GradeId = grade.Id });

        Assert.Throws<DomainException>(() => grade.EnsureNoSections());
    }
}
