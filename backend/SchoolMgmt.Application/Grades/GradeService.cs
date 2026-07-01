using SchoolMgmt.Application.Grades.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Grades;

public class GradeService(IGradeRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<GradeDto> CreateGradeAsync(CreateGradeRequest request, CancellationToken ct = default)
    {
        if (await repository.GradeNameExistsAsync(request.Name, ct))
            throw new ConflictException($"A grade named '{request.Name}' already exists.");

        var grade = new Grade { Name = request.Name, DisplayOrder = request.DisplayOrder };
        await repository.AddAsync(grade, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(grade);
    }

    public async Task<List<GradeDto>> GetAllGradesAsync(CancellationToken ct = default)
    {
        var grades = await repository.GetAllWithSectionsAsync(ct);
        return grades.Select(ToDto).ToList();
    }

    public async Task<GradeDto> GetGradeByIdAsync(Guid id, CancellationToken ct = default)
    {
        var grade = await repository.GetWithSectionsAsync(id, ct)
            ?? throw new NotFoundException("Grade not found.");
        return ToDto(grade);
    }

    public async Task<GradeDto> UpdateGradeAsync(Guid id, UpdateGradeRequest request, CancellationToken ct = default)
    {
        var grade = await repository.GetWithSectionsAsync(id, ct)
            ?? throw new NotFoundException("Grade not found.");

        if (request.Name != grade.Name && await repository.GradeNameExistsAsync(request.Name, ct))
            throw new ConflictException($"A grade named '{request.Name}' already exists.");

        grade.Name = request.Name;
        grade.DisplayOrder = request.DisplayOrder;
        repository.Update(grade);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(grade);
    }

    public async Task DeleteGradeAsync(Guid id, CancellationToken ct = default)
    {
        var grade = await repository.GetWithSectionsAsync(id, ct)
            ?? throw new NotFoundException("Grade not found.");

        grade.EnsureNoSections();
        repository.Remove(grade);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<SectionDto> AddSectionAsync(Guid gradeId, CreateSectionRequest request, CancellationToken ct = default)
    {
        var grade = await repository.GetByIdAsync(gradeId, ct)
            ?? throw new NotFoundException("Grade not found.");

        if (await repository.SectionNameExistsInGradeAsync(gradeId, request.Name, ct))
            throw new ConflictException($"A section named '{request.Name}' already exists in this grade.");

        var section = new Section { GradeId = gradeId, Name = request.Name };
        await repository.AddSectionAsync(section, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(section);
    }

    public async Task<SectionDto> UpdateSectionAsync(Guid gradeId, Guid sectionId, UpdateSectionRequest request, CancellationToken ct = default)
    {
        var section = await repository.GetSectionAsync(gradeId, sectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        if (request.Name != section.Name && await repository.SectionNameExistsInGradeAsync(gradeId, request.Name, ct))
            throw new ConflictException($"A section named '{request.Name}' already exists in this grade.");

        section.Name = request.Name;
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(section);
    }

    public async Task DeleteSectionAsync(Guid gradeId, Guid sectionId, CancellationToken ct = default)
    {
        var section = await repository.GetSectionAsync(gradeId, sectionId, ct)
            ?? throw new NotFoundException("Section not found.");

        repository.RemoveSection(section);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static SectionDto ToDto(Section section) =>
        new(section.Id, section.GradeId, section.Name);

    private static GradeDto ToDto(Grade grade) =>
        new(grade.Id, grade.Name, grade.DisplayOrder, grade.Sections.Select(ToDto).ToList());
}
