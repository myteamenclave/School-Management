using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.FeeTemplates.Dtos;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;

namespace SchoolMgmt.Application.FeeTemplates;

public class FeeTemplateService(
    IFeeTemplateRepository repository,
    IAcademicYearRepository academicYearRepository,
    IGradeRepository gradeRepository,
    IUnitOfWork unitOfWork,
    IRepository<FeeLineItem> lineItemRepository,
    IRepository<FeeInstallment> installmentRepository,
    IRepository<DiscountRule> discountRuleRepository)
{
    public async Task<FeeTemplateDto> CreateAsync(CreateFeeTemplateRequest request, CancellationToken ct = default)
    {
        _ = await academicYearRepository.GetByIdAsync(request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");
        _ = await gradeRepository.GetByIdAsync(request.GradeId, ct)
            ?? throw new NotFoundException("Grade not found.");

        var template = new FeeTemplate
        {
            AcademicYearId = request.AcademicYearId,
            GradeId = request.GradeId,
            Name = request.Name,
            IsActive = true,
        };

        await repository.AddAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var created = await repository.GetByIdWithChildrenAsync(template.Id, ct);
        return ToDetailDto(created!);
    }

    public async Task<PagedResult<FeeTemplateSummaryDto>> GetTemplatesAsync(
        Guid? academicYearId, Guid? gradeId, bool? isActive,
        int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await repository.GetPagedAsync(academicYearId, gradeId, isActive, page, pageSize, ct);
        return new PagedResult<FeeTemplateSummaryDto>(items.Select(ToSummaryDto).ToList(), total, page, pageSize);
    }

    public async Task<FeeTemplateDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await repository.GetByIdWithChildrenAsync(id, ct)
            ?? throw new NotFoundException("Fee template not found.");
        return ToDetailDto(template);
    }

    public async Task<FeeTemplateDto> UpdateHeaderAsync(Guid id, UpdateFeeTemplateRequest request, CancellationToken ct = default)
    {
        var template = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Fee template not found.");

        template.Name = request.Name;
        template.IsActive = request.IsActive;

        repository.Update(template);
        await unitOfWork.SaveChangesAsync(ct);

        var updated = await repository.GetByIdWithChildrenAsync(id, ct);
        return ToDetailDto(updated!);
    }

    public async Task<FeeTemplateDto> ReplaceLineItemsAsync(Guid id, ReplaceLineItemsRequest request, CancellationToken ct = default)
    {
        var template = await repository.GetByIdWithChildrenAsync(id, ct)
            ?? throw new NotFoundException("Fee template not found.");

        var existingById = template.LineItems.ToDictionary(li => li.Id);
        var requestedIds = request.Items
            .Where(i => i.Id.HasValue)
            .Select(i => i.Id!.Value)
            .ToHashSet();

        var idsToDelete = existingById.Keys.Where(k => !requestedIds.Contains(k)).ToHashSet();

        // Null out FK on any DiscountRules that target a line item being removed
        foreach (var rule in template.DiscountRules)
        {
            if (rule.FeeLineItemId.HasValue && idsToDelete.Contains(rule.FeeLineItemId.Value))
            {
                rule.FeeLineItemId = null;
                rule.FeeLineItem = null;
                discountRuleRepository.Update(rule);
            }
        }

        // Delete line items not present in the request
        foreach (var key in idsToDelete)
            lineItemRepository.Remove(existingById[key]);

        // Update or create
        foreach (var input in request.Items)
        {
            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out var existing))
            {
                existing.Name = input.Name;
                existing.Amount = input.Amount;
                existing.DisplayOrder = input.DisplayOrder;
                lineItemRepository.Update(existing);
            }
            else
            {
                var newItem = new FeeLineItem
                {
                    FeeTemplateId = id,
                    Name = input.Name,
                    Amount = input.Amount,
                    DisplayOrder = input.DisplayOrder,
                };
                await lineItemRepository.AddAsync(newItem, ct);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        var reloaded = await repository.GetByIdWithChildrenAsync(id, ct);
        return ToDetailDto(reloaded!);
    }

    public async Task<FeeTemplateDto> ReplaceInstallmentsAsync(Guid id, ReplaceInstallmentsRequest request, CancellationToken ct = default)
    {
        var template = await repository.GetByIdWithChildrenAsync(id, ct)
            ?? throw new NotFoundException("Fee template not found.");

        if (request.Items.Count > 0)
        {
            var sum = request.Items.Sum(i => i.Percentage);
            if (Math.Abs(sum - 100m) > 0.01m)
                throw new DomainException("Installment percentages must sum to 100%.");
        }

        foreach (var existing in template.Installments)
            installmentRepository.Remove(existing);

        foreach (var input in request.Items)
        {
            await installmentRepository.AddAsync(new FeeInstallment
            {
                FeeTemplateId = id,
                Name = input.Name,
                Percentage = input.Percentage,
                DisplayOrder = input.DisplayOrder,
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var reloaded = await repository.GetByIdWithChildrenAsync(id, ct);
        return ToDetailDto(reloaded!);
    }

    public async Task<FeeTemplateDto> ReplaceDiscountRulesAsync(Guid id, ReplaceDiscountRulesRequest request, CancellationToken ct = default)
    {
        var template = await repository.GetByIdWithChildrenAsync(id, ct)
            ?? throw new NotFoundException("Fee template not found.");

        var validLineItemIds = template.LineItems.Select(li => li.Id).ToHashSet();
        foreach (var input in request.Items.Where(i => i.FeeLineItemId.HasValue))
        {
            if (!validLineItemIds.Contains(input.FeeLineItemId!.Value))
                throw new DomainException($"FeeLineItemId {input.FeeLineItemId} does not belong to this template.");
        }

        foreach (var existing in template.DiscountRules)
            discountRuleRepository.Remove(existing);

        foreach (var input in request.Items)
        {
            await discountRuleRepository.AddAsync(new DiscountRule
            {
                FeeTemplateId = id,
                Name = input.Name,
                RuleType = input.RuleType,
                Value = input.Value,
                FeeLineItemId = input.FeeLineItemId,
            }, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var reloaded = await repository.GetByIdWithChildrenAsync(id, ct);
        return ToDetailDto(reloaded!);
    }

    private static FeeTemplateSummaryDto ToSummaryDto(FeeTemplate t) => new(
        t.Id,
        t.Name,
        t.AcademicYearId,
        t.AcademicYear.Name,
        t.GradeId,
        t.Grade.Name,
        t.LineItems.Sum(li => li.Amount),
        t.LineItems.Count,
        t.IsActive,
        t.CreatedAt);

    private static FeeTemplateDto ToDetailDto(FeeTemplate t) => new(
        t.Id,
        t.Name,
        t.AcademicYearId,
        t.AcademicYear.Name,
        t.GradeId,
        t.Grade.Name,
        t.LineItems.Sum(li => li.Amount),
        t.IsActive,
        t.CreatedAt,
        t.UpdatedAt,
        t.LineItems.Select(li => new FeeLineItemDto(li.Id, li.Name, li.Amount, li.DisplayOrder)).ToList(),
        t.Installments.Select(i => new FeeInstallmentDto(i.Id, i.Name, i.Percentage, i.DisplayOrder)).ToList(),
        t.DiscountRules.Select(dr => new DiscountRuleDto(
            dr.Id, dr.Name, dr.RuleType, dr.Value,
            dr.FeeLineItemId, dr.FeeLineItem?.Name)).ToList());
}
