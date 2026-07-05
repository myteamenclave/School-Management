namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record FeeLineItemDto(
    Guid Id,
    string Name,
    decimal Amount,
    int DisplayOrder
);
