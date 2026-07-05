namespace SchoolMgmt.Application.FeeTemplates.Dtos;

public record ReplaceInstallmentsRequest(
    IReadOnlyList<InstallmentInput> Items
);

public record InstallmentInput(
    string Name,
    decimal Percentage,
    int DisplayOrder
);
