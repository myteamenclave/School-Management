using SchoolMgmt.Application.Gradebook.Dtos;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.Gradebook;

public class GradeScaleService(IGradeScaleBandRepository bandRepo, IUnitOfWork unitOfWork)
{
    public async Task<List<GradeScaleBandDto>> GetAllAsync(CancellationToken ct = default) =>
        (await bandRepo.GetAllOrderedAsync(ct))
            .Select(b => new GradeScaleBandDto(b.Id, b.Letter, b.MinScore, b.MaxScore)).ToList();

    public async Task<GradeScaleBandDto> CreateAsync(UpsertGradeScaleBandRequest req, CancellationToken ct = default)
    {
        var band = new GradeScaleBand { Letter = req.Letter, MinScore = req.MinScore, MaxScore = req.MaxScore };
        await bandRepo.AddAsync(band, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new GradeScaleBandDto(band.Id, band.Letter, band.MinScore, band.MaxScore);
    }

    public async Task<GradeScaleBandDto> UpdateAsync(Guid id, UpsertGradeScaleBandRequest req, CancellationToken ct = default)
    {
        var band = await bandRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Grade scale band not found.");
        band.Letter = req.Letter;
        band.MinScore = req.MinScore;
        band.MaxScore = req.MaxScore;
        bandRepo.Update(band);
        await unitOfWork.SaveChangesAsync(ct);
        return new GradeScaleBandDto(band.Id, band.Letter, band.MinScore, band.MaxScore);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var band = await bandRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Grade scale band not found.");
        bandRepo.Remove(band);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
