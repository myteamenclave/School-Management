namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record UpdateFeeTemplateRequest(
    string Name,
    bool IsActive
);
