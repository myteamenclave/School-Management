namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record FeeInstallmentDto(
    Guid Id,
    string Name,
    decimal Percentage,
    string? DueLabel,
    int DisplayOrder
);
