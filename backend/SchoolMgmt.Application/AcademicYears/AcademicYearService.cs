using SchoolMgmt.Application.AcademicYears.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.AcademicYears;

public class AcademicYearService(IAcademicYearRepository repository, IUnitOfWork unitOfWork)
{
    public async Task<AcademicYearDto> CreateAcademicYearAsync(CreateAcademicYearRequest request, CancellationToken ct = default)
    {
        if (await repository.NameExistsAsync(request.Name, ct))
            throw new ConflictException($"An academic year named '{request.Name}' already exists.");

        var year = AcademicYear.Create(request.Name, request.StartDate, request.EndDate);
        await repository.AddAsync(year, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ToDto(year);
    }

    public async Task<List<AcademicYearDto>> GetAllAcademicYearsAsync(CancellationToken ct = default)
    {
        var years = await repository.GetAllWithSemestersAsync(ct);
        return years.Select(ToDto).ToList();
    }

    public async Task<AcademicYearDto> GetAcademicYearByIdAsync(Guid id, CancellationToken ct = default)
    {
        var year = await repository.GetWithSemestersAsync(id, ct)
            ?? throw new NotFoundException($"Academic year {id} not found.");
        return ToDto(year);
    }

    public async Task<SemesterDto> UpdateSemesterAsync(Guid semesterId, UpdateSemesterRequest request, CancellationToken ct = default)
    {
        var semester = await repository.GetSemesterByIdAsync(semesterId, ct)
            ?? throw new NotFoundException($"Semester {semesterId} not found.");

        var year = await repository.GetByIdAsync(semester.AcademicYearId, ct)
            ?? throw new NotFoundException($"Academic year {semester.AcademicYearId} not found.");
        year.EnsureNotArchived();

        semester.Name = request.Name;
        semester.StartDate = request.StartDate;
        semester.EndDate = request.EndDate;

        await unitOfWork.SaveChangesAsync(ct);
        return ToSemesterDto(semester);
    }

    public async Task SetCurrentYearAsync(Guid yearId, CancellationToken ct = default)
    {
        var year = await repository.GetWithSemestersAsync(yearId, ct)
            ?? throw new NotFoundException($"Academic year {yearId} not found.");

        if (year.Status == AcademicYearStatus.Archived)
            throw new DomainException("Cannot set an archived academic year as current.");

        await unitOfWork.BeginTransactionAsync(ct);

        var previousYear = await repository.GetCurrentAsync(ct);
        if (previousYear is not null && previousYear.Id != yearId)
            previousYear.SetCurrent(false);

        var previousSemester = await repository.GetCurrentSemesterAsync(ct);
        if (previousSemester is not null && previousSemester.AcademicYearId != yearId)
            previousSemester.SetCurrent(false);

        year.SetCurrent(true);

        var semester1 = year.Semesters.First(s => s.Name == "Semester 1");
        semester1.SetCurrent(true);

        await unitOfWork.SaveChangesAsync(ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task SetCurrentSemesterAsync(Guid semesterId, CancellationToken ct = default)
    {
        var semester = await repository.GetSemesterByIdAsync(semesterId, ct)
            ?? throw new NotFoundException($"Semester {semesterId} not found.");

        var currentYear = await repository.GetCurrentAsync(ct)
            ?? throw new DomainException("No current academic year is set.");

        if (semester.AcademicYearId != currentYear.Id)
            throw new DomainException("Semester does not belong to the current academic year.");

        await unitOfWork.BeginTransactionAsync(ct);

        var previousSemester = await repository.GetCurrentSemesterAsync(ct);
        if (previousSemester is not null)
            previousSemester.SetCurrent(false);

        semester.SetCurrent(true);

        await unitOfWork.SaveChangesAsync(ct);
        await unitOfWork.CommitAsync(ct);
    }

    public async Task ArchiveAcademicYearAsync(Guid yearId, CancellationToken ct = default)
    {
        var year = await repository.GetByIdAsync(yearId, ct)
            ?? throw new NotFoundException($"Academic year {yearId} not found.");

        year.Archive();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static AcademicYearDto ToDto(AcademicYear year) => new(
        year.Id,
        year.Name,
        year.StartDate,
        year.EndDate,
        year.Status.ToString(),
        year.IsCurrent,
        year.Semesters.Select(ToSemesterDto).ToList());

    private static SemesterDto ToSemesterDto(Semester semester) => new(
        semester.Id,
        semester.AcademicYearId,
        semester.Name,
        semester.StartDate,
        semester.EndDate,
        semester.IsCurrent);
}
