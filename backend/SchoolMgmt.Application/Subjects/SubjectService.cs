using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Application.Subjects.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Subjects;

public class SubjectService(ISubjectRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectRequest request, CancellationToken ct = default)
    {
        var subject = new Subject
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            IsActive = true,
        };

        await repository.AddAsync(subject, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(subject);
    }

    public async Task<PagedResult<SubjectSummaryDto>> GetSubjectsAsync(
        bool? isActive, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(isActive, search, page, pageSize, ct);
        return new PagedResult<SubjectSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
    }

    public async Task<SubjectDto> GetSubjectByIdAsync(Guid id, CancellationToken ct = default)
    {
        var subject = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Subject not found.");
        return ToDto(subject);
    }

    public async Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectRequest request, CancellationToken ct = default)
    {
        var subject = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Subject not found.");

        subject.Name = request.Name;
        subject.Description = request.Description;
        subject.IsActive = request.IsActive;

        repository.Update(subject);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(subject);
    }

    private static SubjectSummaryDto ToSummaryDto(Subject s) => new(
        s.Id, s.Name, s.Code, s.Description, s.IsActive, s.CreatedAt);

    private static SubjectDto ToDto(Subject s) => new(
        s.Id, s.Name, s.Code, s.Description, s.IsActive, s.CreatedAt, s.UpdatedAt);
}
