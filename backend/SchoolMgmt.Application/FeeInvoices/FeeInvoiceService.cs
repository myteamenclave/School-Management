using Microsoft.Extensions.Options;
using SchoolMgmt.Application.AcademicYears;
using SchoolMgmt.Application.Enrollments;
using SchoolMgmt.Application.FeeInvoices.Dtos;
using SchoolMgmt.Application.FeeTemplates;
using SchoolMgmt.Application.Grades;
using SchoolMgmt.Application.Interfaces;
using SchoolMgmt.Application.Students;
using SchoolMgmt.Application.Students.Dtos;
using SchoolMgmt.Domain.Common;
using SchoolMgmt.Domain.Entities;
using SchoolMgmt.Domain.Enums;

namespace SchoolMgmt.Application.FeeInvoices;

public class FeeInvoiceService(
    IStudentFeeAssignmentRepository assignmentRepo,
    IStudentDiscountAssignmentRepository discountAssignmentRepo,
    IFeeInvoiceRepository invoiceRepo,
    IRepository<FeeInvoiceLineItem> lineItemRepo,
    IRepository<FeeInvoiceInstallment> installmentRepo,
    IFeeTemplateRepository templateRepo,
    IAcademicYearRepository yearRepo,
    IGradeRepository gradeRepo,
    IStudentRepository studentRepo,
    IStudentSectionEnrollmentRepository enrollmentRepo,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    IOptions<InvoiceOptions> options)
{
    private readonly int _maxRetries = options.Value.InvoiceCodeMaxRetries;

    public async Task<BroadcastAssignmentResult> BroadcastAssignmentAsync(
        Guid templateId, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException("Fee template not found.");

        var existingAssignments = await assignmentRepo.GetByGradeAndYearAsync(
            template.GradeId, template.AcademicYearId, ct);
        var assignedStudentIds = existingAssignments.Select(a => a.StudentId).ToHashSet();

        var enrollments = await enrollmentRepo.GetByGradeAndYearAsync(
            template.GradeId, template.AcademicYearId, ct);

        var assigned = 0;
        var skipped = 0;

        foreach (var enrollment in enrollments)
        {
            if (assignedStudentIds.Contains(enrollment.StudentId))
            {
                skipped++;
                continue;
            }

            await assignmentRepo.AddAsync(new StudentFeeAssignment
            {
                StudentId = enrollment.StudentId,
                FeeTemplateId = templateId,
                AcademicYearId = template.AcademicYearId,
            }, ct);
            assigned++;
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new BroadcastAssignmentResult(assigned, skipped);
    }

    public async Task<StudentFeeAssignmentDto?> GetStudentAssignmentAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var assignment = await assignmentRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        if (assignment is null) return null;

        var student = await studentRepo.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException("Student not found.");
        var template = await templateRepo.GetByIdAsync(assignment.FeeTemplateId, ct)
            ?? throw new NotFoundException("Fee template not found.");
        var year = await yearRepo.GetByIdAsync(academicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");

        return ToAssignmentDto(assignment, student, template, year);
    }

    public async Task<StudentFeeAssignmentDto> SetStudentAssignmentAsync(
        Guid studentId, SetStudentAssignmentRequest request, CancellationToken ct = default)
    {
        _ = await studentRepo.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException("Student not found.");
        var template = await templateRepo.GetByIdAsync(request.FeeTemplateId, ct)
            ?? throw new NotFoundException("Fee template not found.");
        var year = await yearRepo.GetByIdAsync(request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");
        var student = await studentRepo.GetByIdAsync(studentId, ct)!;

        var existing = await assignmentRepo.GetByStudentAndYearAsync(studentId, request.AcademicYearId, ct);
        StudentFeeAssignment assignment;

        if (existing is not null)
        {
            existing.FeeTemplateId = request.FeeTemplateId;
            assignmentRepo.Update(existing);
            assignment = existing;
        }
        else
        {
            assignment = new StudentFeeAssignment
            {
                StudentId = studentId,
                FeeTemplateId = request.FeeTemplateId,
                AcademicYearId = request.AcademicYearId,
            };
            await assignmentRepo.AddAsync(assignment, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return ToAssignmentDto(assignment, student!, template, year);
    }

    public async Task RemoveStudentAssignmentAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var assignment = await assignmentRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct)
            ?? throw new NotFoundException("Fee assignment not found.");
        assignmentRepo.Remove(assignment);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<List<StudentDiscountAssignmentDto>> GetStudentDiscountsAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var discounts = await discountAssignmentRepo.GetByStudentAndYearAsync(studentId, academicYearId, ct);
        return discounts.Select(ToDiscountDto).ToList();
    }

    public async Task<StudentDiscountAssignmentDto> AddStudentDiscountAsync(
        Guid studentId, AddStudentDiscountRequest request, CancellationToken ct = default)
    {
        _ = await studentRepo.GetByIdAsync(studentId, ct)
            ?? throw new NotFoundException("Student not found.");

        var existing = await discountAssignmentRepo.GetByStudentRuleAndYearAsync(
            studentId, request.DiscountRuleId, request.AcademicYearId, ct);
        if (existing is not null)
            throw new ConflictException("This discount rule is already assigned to the student for this year.");

        var discount = new StudentDiscountAssignment
        {
            StudentId = studentId,
            DiscountRuleId = request.DiscountRuleId,
            AcademicYearId = request.AcademicYearId,
        };
        await discountAssignmentRepo.AddAsync(discount, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var reloaded = await discountAssignmentRepo.GetByStudentAndYearAsync(studentId, request.AcademicYearId, ct);
        var saved = reloaded.First(d => d.Id == discount.Id);
        return ToDiscountDto(saved);
    }

    public async Task RemoveStudentDiscountAsync(Guid id, CancellationToken ct = default)
    {
        var discount = await discountAssignmentRepo.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("Discount assignment not found.");
        discountAssignmentRepo.Remove(discount);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<GenerateInvoicesResult> GenerateInvoicesAsync(
        GenerateInvoicesRequest request, CancellationToken ct = default)
    {
        _ = await gradeRepo.GetByIdAsync(request.GradeId, ct)
            ?? throw new NotFoundException("Grade not found.");
        var academicYear = await yearRepo.GetByIdAsync(request.AcademicYearId, ct)
            ?? throw new NotFoundException("Academic year not found.");

        var assignments = await assignmentRepo.GetByGradeAndYearAsync(
            request.GradeId, request.AcademicYearId, ct);

        var dueDateLookup = request.InstallmentDueDates
            .ToDictionary(d => d.TemplateInstallmentId, d => d.DueDate);

        var generated = 0;
        var skipped = 0;

        foreach (var assignment in assignments)
        {
            var activeInvoice = await invoiceRepo.GetActiveForStudentAndYearAsync(
                assignment.StudentId, request.AcademicYearId, ct);
            if (activeInvoice is not null)
            {
                skipped++;
                continue;
            }

            var template = await templateRepo.GetByIdWithChildrenAsync(assignment.FeeTemplateId, ct);
            if (template is null)
            {
                skipped++;
                continue;
            }

            var studentDiscounts = await discountAssignmentRepo.GetByStudentAndYearAsync(
                assignment.StudentId, request.AcademicYearId, ct);

            var applicableRuleIds = studentDiscounts
                .Select(sd => sd.DiscountRuleId)
                .ToHashSet();

            var lineItems = new List<FeeInvoiceLineItem>();
            foreach (var li in template.LineItems)
            {
                var applicableRules = template.DiscountRules
                    .Where(dr => applicableRuleIds.Contains(dr.Id)
                        && (dr.FeeLineItemId == li.Id || dr.FeeLineItemId == null))
                    .ToList();

                var discountSum = applicableRules.Sum(dr => dr.RuleType == DiscountRuleType.Percentage
                    ? li.Amount * dr.Value / 100m
                    : Math.Min(dr.Value, li.Amount));

                var discountAmount = Math.Min(discountSum, li.Amount);
                var finalAmount = li.Amount - discountAmount;

                lineItems.Add(new FeeInvoiceLineItem
                {
                    SourceLineItemId = li.Id,
                    Name = li.Name,
                    OriginalAmount = li.Amount,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    DisplayOrder = li.DisplayOrder,
                });
            }

            var totalAmount = lineItems.Sum(li => li.FinalAmount);

            var installments = template.Installments.Select(inst => new FeeInvoiceInstallment
            {
                SourceInstallmentId = inst.Id,
                Name = inst.Name,
                Percentage = inst.Percentage,
                DueDate = dueDateLookup.TryGetValue(inst.Id, out var dd) ? dd : null,
                Amount = Math.Round(totalAmount * inst.Percentage / 100m, 2),
                Status = InstallmentStatus.Pending,
                DisplayOrder = inst.DisplayOrder,
            }).ToList();

            var invoice = new FeeInvoice
            {
                StudentId = assignment.StudentId,
                FeeTemplateId = assignment.FeeTemplateId,
                AcademicYearId = request.AcademicYearId,
                TotalAmount = totalAmount,
                Status = InvoiceStatus.Draft,
            };

            foreach (var li in lineItems)
                li.FeeInvoiceId = invoice.Id;
            foreach (var inst in installments)
                inst.FeeInvoiceId = invoice.Id;

            var invoiceSaved = false;
            for (var attempt = 0; attempt < _maxRetries; attempt++)
            {
                invoice.InvoiceCode = await invoiceRepo.GetNextInvoiceCodeAsync(
                    academicYear.StartDate.Year, ct);
                await invoiceRepo.AddAsync(invoice, ct);
                foreach (var li in lineItems)
                    await lineItemRepo.AddAsync(li, ct);
                foreach (var inst in installments)
                    await installmentRepo.AddAsync(inst, ct);

                try
                {
                    await unitOfWork.SaveChangesAsync(ct);
                    invoiceSaved = true;
                    break;
                }
                catch (ConflictException) when (attempt < _maxRetries - 1)
                {
                    unitOfWork.Detach(invoice);
                    foreach (var li in lineItems) unitOfWork.Detach(li);
                    foreach (var inst in installments) unitOfWork.Detach(inst);
                }
            }

            if (!invoiceSaved)
            {
                skipped++;
                continue;
            }

            generated++;
        }

        return new GenerateInvoicesResult(generated, skipped);
    }

    public async Task<PagedResult<FeeInvoiceSummaryDto>> GetInvoicesAsync(
        InvoiceStatus? status, Guid? gradeId, Guid? academicYearId, Guid? studentId,
        int page, int pageSize, CancellationToken ct = default)
    {
        var (items, total) = await invoiceRepo.GetPagedAsync(
            status, gradeId, academicYearId, studentId, page, pageSize, ct);
        return new PagedResult<FeeInvoiceSummaryDto>(
            items.Select(ToSummaryDto).ToList(), total, page, pageSize);
    }

    public async Task<FeeInvoiceDto> GetInvoiceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await invoiceRepo.GetByIdWithDetailsAsync(id, ct)
            ?? throw new NotFoundException("Invoice not found.");
        return ToDetailDto(invoice);
    }

    // Server-owned balance rollup for one student+year: Billed/Paid/Outstanding/Next-Due plus
    // on-the-fly overdue (Status is never mutated). The single authoritative money rule — the
    // parent portal (and future pay-online/dashboard) consume this, never recompute it (spec 20).
    public async Task<StudentFeeOverviewDto> GetStudentFeeOverviewAsync(
        Guid studentId, Guid academicYearId, CancellationToken ct = default)
    {
        var invoice = await invoiceRepo.GetIssuedForStudentAndYearWithDetailsAsync(
            studentId, academicYearId, ct);

        if (invoice is null)
            return new StudentFeeOverviewDto(
                new StudentFeeSummaryDto(false, 0m, 0m, 0m, null, null, 0m, 0, []),
                null);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);

        // "Unpaid" = remaining > 0; robust to future partial payments.
        static decimal Remaining(FeeInvoiceInstallment i) => i.Amount - (i.AmountPaid ?? 0m);

        var totalBilled = invoice.TotalAmount;
        var totalPaid = invoice.Installments.Sum(i => i.AmountPaid ?? 0m);

        var overdue = invoice.Installments
            .Where(i => Remaining(i) > 0m && i.DueDate.HasValue && i.DueDate.Value < today)
            .ToList();

        // Next due = earliest unpaid installment by DueDate (nulls last). Surfaces an overdue
        // installment first if one exists; null when everything is paid / has no due date.
        var nextDue = invoice.Installments
            .Where(i => Remaining(i) > 0m && i.DueDate.HasValue)
            .OrderBy(i => i.DueDate!.Value)
            .FirstOrDefault();

        var summary = new StudentFeeSummaryDto(
            HasInvoice: true,
            TotalBilled: totalBilled,
            TotalPaid: totalPaid,
            Outstanding: totalBilled - totalPaid,
            NextDueDate: nextDue?.DueDate,
            NextDueAmount: nextDue is null ? null : Remaining(nextDue),
            OverdueAmount: overdue.Sum(Remaining),
            OverdueCount: overdue.Count,
            OverdueInstallmentIds: overdue.Select(i => i.Id).ToList());

        return new StudentFeeOverviewDto(summary, ToDetailDto(invoice));
    }

    public async Task<FeeInvoiceDto> IssueInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await invoiceRepo.GetByIdWithDetailsAsync(id, ct)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status != InvoiceStatus.Draft)
            throw new DomainException("Only Draft invoices can be issued.");

        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAt = dateTimeProvider.UtcNow.UtcDateTime;
        invoiceRepo.Update(invoice);

        var template = await templateRepo.GetByIdAsync(invoice.FeeTemplateId, ct);
        if (template is not null && !template.IsFrozen)
        {
            template.IsFrozen = true;
            templateRepo.Update(template);
        }

        await unitOfWork.SaveChangesAsync(ct);

        var updated = await invoiceRepo.GetByIdWithDetailsAsync(id, ct);
        return ToDetailDto(updated!);
    }

    public async Task<BulkIssueResult> BulkIssueAsync(List<Guid> ids, CancellationToken ct = default)
    {
        await unitOfWork.BeginTransactionAsync(ct);

        var issued = 0;
        var skipped = 0;

        try
        {
            foreach (var id in ids)
            {
                try
                {
                    await IssueInvoiceAsync(id, ct);
                    issued++;
                }
                catch (DomainException)
                {
                    skipped++;
                }
            }

            await unitOfWork.CommitAsync(ct);
        }
        catch
        {
            await unitOfWork.RollbackAsync(ct);
            throw;
        }

        return new BulkIssueResult(issued, skipped);
    }

    public async Task<FeeInvoiceDto> CancelInvoiceAsync(Guid id, CancellationToken ct = default)
    {
        var invoice = await invoiceRepo.GetByIdWithDetailsAsync(id, ct)
            ?? throw new NotFoundException("Invoice not found.");

        if (invoice.Status == InvoiceStatus.Cancelled)
            throw new DomainException("Invoice is already cancelled.");

        invoice.Status = InvoiceStatus.Cancelled;
        invoice.CancelledAt = dateTimeProvider.UtcNow.UtcDateTime;
        invoiceRepo.Update(invoice);

        await unitOfWork.SaveChangesAsync(ct);

        var updated = await invoiceRepo.GetByIdWithDetailsAsync(id, ct);
        return ToDetailDto(updated!);
    }

    private static StudentFeeAssignmentDto ToAssignmentDto(
        StudentFeeAssignment a, Student student, FeeTemplate template, AcademicYear year) => new(
        a.Id,
        student.Id,
        $"{student.FirstName} {student.LastName}",
        student.StudentCode,
        template.Id,
        template.Name,
        year.Id,
        year.Name);

    private static StudentDiscountAssignmentDto ToDiscountDto(StudentDiscountAssignment d) => new(
        d.Id,
        d.StudentId,
        d.DiscountRuleId,
        d.DiscountRule.Name,
        d.DiscountRule.RuleType.ToString(),
        d.DiscountRule.Value,
        d.AcademicYearId);

    private static FeeInvoiceSummaryDto ToSummaryDto(FeeInvoice i) => new(
        i.Id,
        i.InvoiceCode,
        i.StudentId,
        $"{i.Student.FirstName} {i.Student.LastName}",
        i.Student.StudentCode,
        i.AcademicYearId,
        i.AcademicYear.Name,
        i.FeeTemplateId,
        i.FeeTemplate.Name,
        i.TotalAmount,
        i.Status.ToString(),
        i.IssuedAt,
        i.CreatedAt.UtcDateTime);

    private static FeeInvoiceDto ToDetailDto(FeeInvoice i) => new(
        i.Id,
        i.InvoiceCode,
        i.StudentId,
        $"{i.Student.FirstName} {i.Student.LastName}",
        i.Student.StudentCode,
        i.AcademicYearId,
        i.AcademicYear.Name,
        i.FeeTemplateId,
        i.FeeTemplate.Name,
        i.TotalAmount,
        i.Status.ToString(),
        i.IssuedAt,
        i.CancelledAt,
        i.CreatedAt.UtcDateTime,
        i.UpdatedAt?.UtcDateTime,
        i.LineItems
            .OrderBy(li => li.DisplayOrder)
            .Select(li => new FeeInvoiceLineItemDto(
                li.Id, li.Name,
                li.OriginalAmount, li.DiscountAmount, li.FinalAmount,
                li.DisplayOrder))
            .ToList(),
        i.Installments
            .OrderBy(inst => inst.DisplayOrder)
            .Select(inst => new FeeInvoiceInstallmentDto(
                inst.Id, inst.Name, inst.Percentage,
                inst.DueDate, inst.Amount, inst.Status.ToString(), inst.DisplayOrder))
            .ToList());
}
